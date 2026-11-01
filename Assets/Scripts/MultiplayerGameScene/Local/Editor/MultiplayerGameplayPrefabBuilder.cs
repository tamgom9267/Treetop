#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class MultiplayerGameplayPrefabBuilder
{
    public const string Root = "Assets/Resources/Multiplayer";
    private const string Source = "Assets/Resources/GeneracionDeCuartos";
    public static readonly string[] RoomIds = { "0", "1", "2", "3", "4", "5", "6", "7", "8", "boss1", "boss2" };
    [InitializeOnLoadMethod]
    private static void Schedule()
    {
        if (!File.Exists(Root + "/Host/Cuartos/0.prefab") || !File.Exists(Root + "/Client/Enemigos/Enemigo2.prefab"))
            EditorApplication.delayCall += BuildWhenIdle;
    }
    private static void BuildWhenIdle()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorApplication.isUpdating || EditorApplication.isCompiling) { EditorApplication.delayCall += BuildWhenIdle; return; }
        try { CreatePrefabs(); MultiplayerGameplayVerification.RunChecks(); }
        catch (Exception error) { Debug.LogException(error); }
    }
    [MenuItem("Tools/Multiplayer/Crear prefabs de fase 4")]
    public static void CreatePrefabs()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Sal de Play para preparar los prefabs.");
        foreach (string role in new[] { "Host", "Client" })
        {
            Folder(Root + "/" + role + "/Cuartos"); Folder(Root + "/" + role + "/Enemigos");
            foreach (string id in RoomIds)
            {
                string destination = Root + "/" + role + "/Cuartos/" + id + ".prefab";
                if (!File.Exists(destination)) BuildRoom(id, role == "Host", destination);
            }
            for (int type = 1; type <= 2; type++)
            {
                string destination = Root + "/" + role + "/Enemigos/Enemigo" + type + ".prefab";
                if (!File.Exists(destination)) BuildEnemy(type, role == "Host", destination);
            }
        }
        UpgradeClientEnemyVisuals();
        var definitions = BuildLoot();
        ConfigureCoordinator(definitions);
        ValidatePrefabs();
        Debug.Log("FASE 4: 22 cuartos, 4 slimes, catalogo de loot y coordinador preparados. No se guardaron escenas.");
    }
    private static void BuildRoom(string id, bool host, string destination)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(Source + "/Cuartos/" + id + ".prefab");
        try
        {
            Unpack(root);
            var source = root.GetComponent<GeneratedRoomController>();
            MultiplayerRoomController room = host ? (MultiplayerRoomController)root.AddComponent<HostGeneratedRoomController>() : root.AddComponent<ClientGeneratedRoomController>();
            room.spawners = source != null ? source.spawners : Array.Empty<GameObject>(); room.Configure(0, null, id);
            foreach (RoomTriggerForwarder old in root.GetComponentsInChildren<RoomTriggerForwarder>(true)) Object.DestroyImmediate(old);
            foreach (GeneratedRoomController old in root.GetComponentsInChildren<GeneratedRoomController>(true)) Object.DestroyImmediate(old);
            foreach (StartingRoomSpawns old in root.GetComponentsInChildren<StartingRoomSpawns>(true)) Object.DestroyImmediate(old);
            if (id == "0") root.AddComponent<MultiplayerStartingRoomSpawns>().FindLegacyPoints();
            if (!host)
            {
                foreach (NavMeshSurface surface in root.GetComponentsInChildren<NavMeshSurface>(true)) Object.DestroyImmediate(surface);
                foreach (NavMeshLink link in root.GetComponentsInChildren<NavMeshLink>(true)) Object.DestroyImmediate(link);
                foreach (NavMeshModifier modifier in root.GetComponentsInChildren<NavMeshModifier>(true)) Object.DestroyImmediate(modifier);
                foreach (NavMeshModifierVolume modifier in root.GetComponentsInChildren<NavMeshModifierVolume>(true)) Object.DestroyImmediate(modifier);
            }
            foreach (InventoryObject item in root.GetComponentsInChildren<InventoryObject>(true))
            {
                var identity = item.gameObject.AddComponent<MultiplayerItemIdentity>();
                var serialized = new SerializedObject(identity);
                serialized.FindProperty("embeddedId").stringValue = id + ":loot:" + AnimationUtility.CalculateTransformPath(item.transform, root.transform);
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            Save(root, destination);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    private static void BuildEnemy(int type, bool host, string destination)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(Source + "/Enemigos/Enemigo" + type + ".prefab");
        try
        {
            Unpack(root);
            var combat = root.GetComponent<EnemyCombat>(); var movement = root.GetComponent<SlimeEnemyMovementProcedural>();
            var enemy = root.AddComponent<MultiplayerEnemy>();
            Copy(combat, enemy, "maxHealth", "contactDamage", "knockbackForce", "knockbackUpForce", "weaponKnockbackMultiplier", "rend", "flashColor", "flashDuration");
            var settings = new SerializedObject(enemy); settings.FindProperty("authority").boolValue = host; settings.ApplyModifiedPropertiesWithoutUndo();
            if (host) Copy(movement, root.AddComponent<HostSlimeMovement>(), "minJumpCooldown", "maxJumpCooldown", "horizontalJumpForce", "verticalJumpForce", "turnSpeed", "navMeshCheckDistance");
            if (combat != null) Object.DestroyImmediate(combat); if (movement != null) Object.DestroyImmediate(movement);
            var body = root.GetComponent<Rigidbody>(); body.isKinematic = !host; body.useGravity = host;
            if (!host)
            {
                root.GetComponent<Animator>().updateMode = AnimatorUpdateMode.UnscaledTime;
                EnsureCombatVisual(root);
            }
            Save(root, destination);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    [MenuItem("Tools/Multiplayer/Preparar visuales de combate cliente")]
    public static void UpgradeClientEnemyVisuals()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Sal de Play para preparar los prefabs.");
        for (int type = 1; type <= 2; type++)
        {
            string destination = Root + "/Client/Enemigos/Enemigo" + type + ".prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(destination);
            try { if (EnsureCombatVisual(root)) Save(root, destination); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
    private static bool EnsureCombatVisual(GameObject root)
    {
        MeshRenderer renderer = root.GetComponent<MeshRenderer>();
        if (renderer == null) return false;
        MeshFilter mesh = root.GetComponent<MeshFilter>();
        if (mesh == null) throw new InvalidOperationException("Falta la malla del slime cliente.");
        var visual = new GameObject("CombatVisual", typeof(MeshFilter), typeof(MeshRenderer));
        visual.layer = root.layer;
        visual.transform.SetParent(root.transform, false);
        EditorUtility.CopySerialized(mesh, visual.GetComponent<MeshFilter>());
        EditorUtility.CopySerialized(renderer, visual.GetComponent<MeshRenderer>());
        var settings = new SerializedObject(root.GetComponent<MultiplayerEnemy>());
        settings.FindProperty("rend").objectReferenceValue = visual.GetComponent<MeshRenderer>();
        settings.ApplyModifiedPropertiesWithoutUndo();
        Object.DestroyImmediate(renderer); Object.DestroyImmediate(mesh);
        return true;
    }
    private static List<MultiplayerLootDefinition> BuildLoot()
    {
        Folder(Root + "/Local/Loot");
        var result = new List<MultiplayerLootDefinition>(); var weapons = new HashSet<WeaponData>(); var generic = new HashSet<string>();
        var candidates = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { Source + "/Armas", "Assets/Prefab" })) candidates.Add(AssetDatabase.GUIDToAssetPath(guid));
        candidates.Sort((a,b) => { int order = b.StartsWith(Source, StringComparison.Ordinal).CompareTo(a.StartsWith(Source, StringComparison.Ordinal)); return order != 0 ? order : string.CompareOrdinal(a,b); });
        foreach (string path in candidates)
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            InventoryObject item = source != null ? source.GetComponent<InventoryObject>() : null; if (item == null) continue;
            WeaponObject weapon = item as WeaponObject;
            if (weapon != null ? weapon.weaponData == null || !weapons.Add(weapon.weaponData) : !generic.Add(item.Objectname ?? item.Name)) continue;
            string type = weapon != null && weapon.weaponData.name == "Sword" ? "sword" : AssetDatabase.AssetPathToGUID(path);
            string destination = Root + "/Local/Loot/" + type + ".prefab";
            if (!File.Exists(destination))
            {
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    Unpack(root); if (root.GetComponent<MultiplayerItemIdentity>() == null) root.AddComponent<MultiplayerItemIdentity>();
                    root.transform.position = Vector3.zero; root.transform.rotation = Quaternion.identity;
                    Save(root, destination);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            result.Add(new MultiplayerLootDefinition { type = type, prefab = AssetDatabase.LoadAssetAtPath<GameObject>(destination),
                weapon = weapon != null ? weapon.weaponData : null, objectName = item.Objectname,
                icon = weapon != null ? weapon.weaponData.UIsprite : null });
        }
        if (result.Count == 0) throw new InvalidOperationException("No se encontraron prefabs de objetos de inventario.");
        return result;
    }
    private static void ConfigureCoordinator(List<MultiplayerLootDefinition> definitions)
    {
        string path = Root + "/Local/MultiplayerPlayers.prefab";
        if (!File.Exists(path)) MultiplayerPlayerPrefabBuilder.CreateMissingPrefabs();
        Scene preview = EditorSceneManager.OpenPreviewScene("Assets/Scenes/VillageMenuScene.unity");
        GameObject root = null;
        try
        {
            PlayerInventory inventory = null;
            foreach (GameObject candidate in preview.GetRootGameObjects())
            { inventory = candidate.GetComponentInChildren<PlayerInventory>(true); if (inventory != null) break; }
            if (inventory == null) throw new InvalidOperationException("La aldea no contiene PlayerInventory.");
            root = PrefabUtility.LoadPrefabContents(path);
            if (root.GetComponent<MultiplayerWorldSession>() == null) root.AddComponent<MultiplayerWorldSession>();
            var source = new SerializedObject(inventory);
            var adapter = new SerializedObject(root.GetComponent<MultiplayerInventoryAdapter>());
            adapter.FindProperty("itemPanelPrefab").objectReferenceValue = source.FindProperty("ObjectImagePanel").objectReferenceValue;
            foreach (var mapping in new[] { new[] { "lootImagesPath", "ChestLootImages" }, new[] { "lootSlotsPath", "ChestLoot" } })
            {
                var target = (GameObject)source.FindProperty(mapping[1]).objectReferenceValue;
                adapter.FindProperty(mapping[0]).stringValue = AnimationUtility.CalculateTransformPath(target.transform, inventory.transform);
            }
            adapter.ApplyModifiedPropertiesWithoutUndo();
            var loot = new SerializedObject(root.GetComponent<MultiplayerLootSession>());
            var entries = loot.FindProperty("catalog"); entries.arraySize = definitions.Count;
            for (int i = 0; i < definitions.Count; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i); var definition = definitions[i];
                entry.FindPropertyRelative("type").stringValue = definition.type;
                entry.FindPropertyRelative("prefab").objectReferenceValue = definition.prefab;
                entry.FindPropertyRelative("weapon").objectReferenceValue = definition.weapon;
                entry.FindPropertyRelative("objectName").stringValue = definition.objectName;
                entry.FindPropertyRelative("icon").objectReferenceValue = definition.icon;
            }
            loot.ApplyModifiedPropertiesWithoutUndo(); Save(root, path);
        }
        finally { if (root != null) PrefabUtility.UnloadPrefabContents(root); EditorSceneManager.ClosePreviewScene(preview); }
    }
    private static void Unpack(GameObject root)
    { if (PrefabUtility.IsPartOfPrefabInstance(root)) PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction); }
    private static void Copy(Object from, Object to, params string[] properties)
    {
        if (from == null) return;
        var source = new SerializedObject(from); var destination = new SerializedObject(to);
        foreach (string name in properties) { var property = source.FindProperty(name); if (property != null) destination.CopyFromSerializedProperty(property); }
        destination.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void Save(GameObject root, string path)
    {
        if (!path.StartsWith(Root + "/", StringComparison.Ordinal)) throw new InvalidOperationException("Destino fuera de Multiplayer.");
        if (PrefabUtility.SaveAsPrefabAsset(root, path) == null) throw new InvalidOperationException("No se pudo guardar " + path);
    }
    private static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\','/'); Folder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
    [MenuItem("Tools/Multiplayer/Validar prefabs de fase 4")]
    public static void ValidatePrefabs()
    {
        foreach (string role in new[] { "Host", "Client" }) foreach (string id in RoomIds)
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(Source + "/Cuartos/" + id + ".prefab");
            GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/" + role + "/Cuartos/" + id + ".prefab");
            if (root == null || root.GetComponent<MultiplayerRoomController>() == null || root.GetComponentsInChildren<GeneratedRoomController>(true).Length != 0 ||
                root.GetComponentsInChildren<StartingRoomSpawns>(true).Length != 0 || root.GetComponentsInChildren<RoomTriggerForwarder>(true).Length != 0)
                throw new InvalidOperationException("Controladores incorrectos en " + role + "/" + id);
            int original = source.GetComponentsInChildren<NavMeshSurface>(true).Length;
            int actual = root.GetComponentsInChildren<NavMeshSurface>(true).Length;
            if (actual != (role == "Host" ? original : 0)) throw new InvalidOperationException("NavMesh incorrecto en " + role + "/" + id);
            if (root.GetComponent<MultiplayerRoomController>().spawners.Length != source.GetComponent<GeneratedRoomController>().spawners.Length)
                throw new InvalidOperationException("Spawners diferentes en " + role + "/" + id);
        }
        foreach (string role in new[] { "Host", "Client" }) for (int type = 1; type <= 2; type++)
        {
            GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/" + role + "/Enemigos/Enemigo" + type + ".prefab");
            if (root == null || root.GetComponent<MultiplayerEnemy>() == null || root.GetComponent<EnemyCombat>() != null || root.GetComponent<SlimeEnemyMovementProcedural>() != null ||
                (root.GetComponent<HostSlimeMovement>() != null) != (role == "Host") || root.GetComponent<Rigidbody>().isKinematic != (role == "Client"))
                throw new InvalidOperationException("Slime mal configurado: " + role + "/" + type);
            if (role == "Client" && (root.transform.Find("CombatVisual") == null || root.GetComponent<MeshRenderer>() != null))
                throw new InvalidOperationException("Falta separar el visual de combate: Client/" + type);
        }
        var coordinator = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Local/MultiplayerPlayers.prefab");
        if (coordinator.GetComponent<MultiplayerWorldSession>() == null || coordinator.GetComponent<MultiplayerLootSession>() == null ||
            !coordinator.GetComponent<MultiplayerInventoryAdapter>().Validate(out string error)) throw new InvalidOperationException("Coordinador incompleto.");
    }
}
#endif
