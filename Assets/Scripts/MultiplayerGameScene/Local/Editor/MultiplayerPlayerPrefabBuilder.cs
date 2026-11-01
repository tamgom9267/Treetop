#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Lee una escena de vista previa. Nunca guarda escenas ni modifica los assets fuente.</summary>
public static class MultiplayerPlayerPrefabBuilder
{
    public const string Root = "Assets/Resources/Multiplayer";
    public const string LocalPath = Root + "/Local/MultiplayerPlayers.prefab";
    public const string RemotePath = Root + "/Online/RemotePlayer.prefab";
    public const string SwordPath = Root + "/Online/Weapons/SwordVisual.prefab";
    private const string SourceScene = "Assets/Scenes/VillageMenuScene.unity";
    private const string SwordDataPath = "Assets/Scripts/Inventory/Sword.asset";

    [InitializeOnLoadMethod]
    private static void ScheduleMissingPrefabs()
    {
        // La primera importacion deja lista la integracion sin agregar componentes a escenas.
        if (!File.Exists(LocalPath) || !File.Exists(RemotePath) || !File.Exists(SwordPath))
            EditorApplication.delayCall += CreateWhenIdle;
    }

    private static void CreateWhenIdle()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        { EditorApplication.delayCall += CreateWhenIdle; return; }
        try { CreateMissingPrefabs(); }
        catch (Exception exception) { Debug.LogException(exception); }
    }

    [MenuItem("Tools/Multiplayer/Crear prefabs de jugadores")]
    public static void CreateMissingPrefabs()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Sal de Play antes de crear los prefabs multijugador.");
        EnsureFolder(Root);
        foreach (string folder in new[] { "Local", "Online", "Host", "Client", "Online/Weapons" })
            EnsureFolder(Root + "/" + folder);
        WeaponData swordData = AssetDatabase.LoadAssetAtPath<WeaponData>(SwordDataPath);
        if (swordData == null || swordData.prefab == null) throw new InvalidOperationException("Falta Sword.asset o su prefab original.");

        Scene preview = EditorSceneManager.OpenPreviewScene(SourceScene);
        try
        {
            if (!File.Exists(SwordPath)) CreateSword(swordData, preview);
            if (!File.Exists(RemotePath)) CreateRemote(preview);
            if (!File.Exists(LocalPath)) CreateCoordinator(swordData, preview);
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
        ValidatePrefabs();
        Debug.Log("FASE 3: prefabs de jugadores creados y validados. No se guardaron escenas ni se modificaron prefabs originales.");
    }

    private static void CreateSword(WeaponData data, Scene preview)
    {
        GameObject copy = (GameObject)PrefabUtility.InstantiatePrefab(data.prefab, preview);
        copy.name = "SwordVisual";
        copy.SetActive(false);
        if (PrefabUtility.IsPartOfPrefabInstance(copy))
            PrefabUtility.UnpackPrefabInstance(copy, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        foreach (Canvas canvas in copy.GetComponentsInChildren<Canvas>(true)) Object.DestroyImmediate(canvas.gameObject);
        StripGameplay(copy);
        copy.transform.localPosition = Vector3.zero;
        MultiplayerWeaponVisual visual = copy.AddComponent<MultiplayerWeaponVisual>();
        visual.SetTrail(false);
        copy.SetActive(true);
        Save(copy, SwordPath);
    }

    private static void CreateRemote(Scene preview)
    {
        PlayerController source = null;
        foreach (GameObject root in preview.GetRootGameObjects())
        {
            source = root.GetComponentInChildren<PlayerController>(true);
            if (source != null) break;
        }
        if (source == null) throw new InvalidOperationException("La escena de la aldea no tiene PlayerController.");
        SerializedObject serializedSource = new SerializedObject(source);
        Transform sourceHand = serializedSource.FindProperty("handObj").objectReferenceValue as Transform;
        if (sourceHand == null) throw new InvalidOperationException("Falta handObj en el personaje original.");
        string handPath = AnimationUtility.CalculateTransformPath(sourceHand, source.transform);
        GameObject copy = Object.Instantiate(source.gameObject);
        SceneManager.MoveGameObjectToScene(copy, preview);
        copy.name = "RemotePlayer";
        copy.SetActive(false);
        StripGameplay(copy);
        Animator animator = copy.GetComponent<Animator>();
        if (animator == null) throw new InvalidOperationException("Falta el Animator raiz del personaje original.");
        foreach (Animator other in copy.GetComponentsInChildren<Animator>(true))
            if (other != animator) Object.DestroyImmediate(other);
        copy.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        animator.updateMode = AnimatorUpdateMode.UnscaledTime;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.applyRootMotion = false;
        MultiplayerRemoteEquipment equipment = copy.AddComponent<MultiplayerRemoteEquipment>();
        equipment.Configure(copy.transform.Find(handPath), Array.Empty<MultiplayerVisualDefinition>());
        MultiplayerRemotePlayer remote = copy.AddComponent<MultiplayerRemotePlayer>();
        remote.Configure(animator, equipment);
        copy.SetActive(true);
        Save(copy, RemotePath);
    }

    private static void CreateCoordinator(WeaponData swordData, Scene preview)
    {
        GameObject root = new GameObject("MultiplayerPlayers");
        SceneManager.MoveGameObjectToScene(root, preview);
        root.AddComponent<MultiplayerLocalPlayerPublisher>();
        MultiplayerPlayers coordinator = root.AddComponent<MultiplayerPlayers>();
        SerializedObject serialized = new SerializedObject(coordinator);
        serialized.FindProperty("remotePlayerPrefab").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<GameObject>(RemotePath).GetComponent<MultiplayerRemotePlayer>();
        SerializedProperty catalog = serialized.FindProperty("catalog");
        catalog.arraySize = 1;
        SerializedProperty sword = catalog.GetArrayElementAtIndex(0);
        sword.FindPropertyRelative("typeId").stringValue = "sword";
        sword.FindPropertyRelative("localWeapon").objectReferenceValue = swordData;
        sword.FindPropertyRelative("remotePrefab").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<GameObject>(SwordPath).GetComponent<MultiplayerWeaponVisual>();
        serialized.ApplyModifiedPropertiesWithoutUndo();
        Save(root, LocalPath);
    }

    private static void StripGameplay(GameObject root)
    {
        foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(behaviour);
        foreach (Collider collider in root.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
        foreach (Rigidbody body in root.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(body);
        foreach (Camera camera in root.GetComponentsInChildren<Camera>(true)) Object.DestroyImmediate(camera);
        foreach (AudioListener listener in root.GetComponentsInChildren<AudioListener>(true)) Object.DestroyImmediate(listener);
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        { child.gameObject.tag = "Untagged"; child.gameObject.layer = 0; }
    }

    private static void Save(GameObject root, string path)
    {
        if (PrefabUtility.SaveAsPrefabAsset(root, path) == null) throw new InvalidOperationException("No se pudo guardar " + path);
        Object.DestroyImmediate(root);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    [MenuItem("Tools/Multiplayer/Validar prefabs de jugadores")]
    public static void ValidatePrefabs()
    {
        GameObject local = AssetDatabase.LoadAssetAtPath<GameObject>(LocalPath);
        if (local == null || local.GetComponent<MultiplayerPlayers>() == null ||
            !local.GetComponent<MultiplayerPlayers>().ValidateAssets(out string error))
            throw new InvalidOperationException("Prefab Local incompleto. Revisa RemotePlayer y el catalogo visual.");
        ValidateVisual(RemotePath, true);
        ValidateVisual(SwordPath, false);
    }

    private static void ValidateVisual(string path, bool player)
    {
        GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (root == null || root.GetComponentsInChildren<Renderer>(true).Length == 0 ||
            root.GetComponentsInChildren<Collider>(true).Length != 0 || root.GetComponentsInChildren<Rigidbody>(true).Length != 0 ||
            root.GetComponentsInChildren<Camera>(true).Length != 0 || root.GetComponentsInChildren<AudioListener>(true).Length != 0)
            throw new InvalidOperationException("El prefab visual contiene fisica o le falta su modelo: " + path);
        foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            if (behaviour == null || !(player ? behaviour is MultiplayerRemotePlayer || behaviour is MultiplayerRemoteEquipment :
                behaviour is MultiplayerWeaponVisual)) throw new InvalidOperationException("Script no visual en " + path);
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            if (!child.CompareTag("Untagged")) throw new InvalidOperationException("Tag de gameplay en " + path);
        if (player && root.GetComponentsInChildren<Animator>(true).Length != 1)
            throw new InvalidOperationException("RemotePlayer debe tener un unico Animator.");
    }
}
#endif
