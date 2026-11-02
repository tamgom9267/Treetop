#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Pruebas nativas de Unity, en memoria. No guarda ni sustituye ninguna escena.</summary>
public static class MultiplayerPlayerVerification
{
    private static readonly List<string> passed = new List<string>();
    private const string Run = "11111111111111111111111111111111";

    [MenuItem("Tools/Multiplayer/Probar fase 3 (sin red)")]
    public static void RunChecks()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Ejecuta las pruebas fuera de Play.");
        passed.Clear();
        try
        {
            MultiplayerPlayerPrefabBuilder.ValidatePrefabs();
            Check(true, "Prefabs completos, sin fisica, scripts de gameplay ni tags Player/Weapon");
            CheckProtocol();
            CheckReplicas();
            CheckWeaponReplicas();
            WriteReport(true, string.Empty);
            Debug.Log("FASE 3: " + passed.Count + " pruebas nativas correctas. Informe: Temp/phase3-verification/editor-tests.json");
        }
        catch (Exception error)
        {
            WriteReport(false, error.ToString());
            Debug.LogException(error);
            throw;
        }
    }

    private static MultiplayerPlayerProtocol.Pose Pose(int sequence = 1, double time = 100d, float x = 0f, float speed = 0f)
    {
        return new MultiplayerPlayerProtocol.Pose { sequence = sequence, time = time, position = new Vector3(x, 0f, 0f),
            rotation = Quaternion.identity, speed = speed, playbackRate = 1f };
    }

    private static MultiplayerPlayerProtocol.Equipment State(int sequence, int selected, params int[] ids)
    {
        var items = new MultiplayerPlayerProtocol.Item[ids.Length];
        for (int i = 0; i < ids.Length; i++) items[i] = new MultiplayerPlayerProtocol.Item(ids[i], "sword");
        return new MultiplayerPlayerProtocol.Equipment { pose = Pose(sequence), equippedId = selected, items = items };
    }

    private static void CheckProtocol()
    {
        object[] pose = MultiplayerPlayerProtocol.PackPose(Run, 1, Pose());
        Check(MultiplayerPlayerProtocol.TryReadHeader(pose, Run, 1, 9, out _) &&
            MultiplayerPlayerProtocol.TryReadPose(pose, out var decoded) && decoded.rotation == Quaternion.identity, "Posicion y rotacion conservadas por el protocolo");
        Check(!MultiplayerPlayerProtocol.TryReadHeader(pose, "otra partida", 1, 9, out _), "Mensajes de otra partida rechazados");
        Check(!MultiplayerPlayerProtocol.TryReadHeader(pose, Run, 2, 9, out _), "Mensajes de otro piso rechazados");
        Check(!MultiplayerPlayerProtocol.TryReadHeader(null, Run, 1, 9, out _), "Paquete nulo rechazado");
        Check(!MultiplayerPlayerProtocol.TryReadHeader(pose, Run, 1, 12, out _), "Paquete incompleto rechazado");
        object[] corrupt = (object[])pose.Clone(); corrupt[0] = 99;
        Check(!MultiplayerPlayerProtocol.TryReadHeader(corrupt, Run, 1, 9, out _), "Version incompatible rechazada");
        corrupt = (object[])pose.Clone(); corrupt[3] = 0;
        Check(!MultiplayerPlayerProtocol.TryReadHeader(corrupt, Run, 1, 9, out _), "Secuencia invalida rechazada");
        corrupt = (object[])pose.Clone(); corrupt[4] = double.NaN;
        Check(!MultiplayerPlayerProtocol.TryReadHeader(corrupt, Run, 1, 9, out _), "Tiempo no finito rechazado");
        corrupt = (object[])pose.Clone(); corrupt[5] = new Vector3(float.NaN, 0, 0);
        Check(!MultiplayerPlayerProtocol.TryReadPose(corrupt, out _), "Coordenadas no finitas rechazadas");
        corrupt = (object[])pose.Clone(); corrupt[6] = new Quaternion(0, 0, 0, 0);
        Check(!MultiplayerPlayerProtocol.TryReadPose(corrupt, out _), "Rotacion invalida rechazada");
        corrupt = (object[])pose.Clone(); corrupt[8] = -1f;
        Check(!MultiplayerPlayerProtocol.TryReadPose(corrupt, out _), "Velocidad de reproduccion invalida rechazada");
        var state = MultiplayerPlayerProtocol.PackEquipment(Run, 1, State(1, 2, 1, 2));
        Check(MultiplayerPlayerProtocol.TryReadEquipment(state, out var equipment) && equipment.items.Length == 2 &&
            equipment.equippedId == 2, "Dos espadas del mismo tipo conservan identidades distintas");
        Check(MultiplayerPlayerProtocol.TryReadEquipment(MultiplayerPlayerProtocol.PackEquipment(Run, 1, State(1, 0)), out _), "Inventario vacio valido");
        corrupt = (object[])state.Clone(); corrupt[9] = new[] { 1, 1 };
        Check(!MultiplayerPlayerProtocol.TryReadEquipment(corrupt, out _), "IDs de inventario repetidos rechazados");
        corrupt = (object[])state.Clone(); corrupt[11] = 8;
        Check(!MultiplayerPlayerProtocol.TryReadEquipment(corrupt, out _), "Equipo inexistente rechazado");
        corrupt = (object[])state.Clone(); corrupt[10] = new[] { "sword" };
        Check(!MultiplayerPlayerProtocol.TryReadEquipment(corrupt, out _), "Listas de inventario incompatibles rechazadas");
        var attack = MultiplayerPlayerProtocol.PackAttack(Run, 1, new MultiplayerPlayerProtocol.Attack
            { pose = Pose(), item = new MultiplayerPlayerProtocol.Item(2, "sword") });
        Check(MultiplayerPlayerProtocol.TryReadAttack(attack, out var action) && action.item.id == 2, "Ataque incluye su propio objeto visual");
        corrupt = (object[])attack.Clone(); corrupt[9] = 0;
        Check(!MultiplayerPlayerProtocol.TryReadAttack(corrupt, out _), "Ataque con objeto inconsistente rechazado");
    }

    private static void CheckReplicas()
    {
        Scene preview = EditorSceneManager.NewPreviewScene();
        try
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MultiplayerPlayerPrefabBuilder.RemotePath);
            var sword = AssetDatabase.LoadAssetAtPath<GameObject>(MultiplayerPlayerPrefabBuilder.SwordPath).GetComponent<MultiplayerWeaponVisual>();
            var catalog = new[] { new MultiplayerVisualDefinition { typeId = "sword", remotePrefab = sword } };
            var first = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, preview)).GetComponent<MultiplayerRemotePlayer>();
            var second = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, preview)).GetComponent<MultiplayerRemotePlayer>();
            first.Initialize(catalog, 0.1f); second.Initialize(catalog, 0.1f);
            first.SetPlaying(true);
            Check(!first.gameObject.activeSelf, "Replica oculta hasta recibir estado inicial");
            Check(first.ReceiveEquipment(State(1, 1, 1, 2)), "Primer estado aceptado");
            second.ReceiveEquipment(State(1, 0));
            Check(first.Equipment.CachedCount == 2 && VisibleWeapons(first) == 1 && VisibleWeapons(second) == 0,
                "Cache por jugador y una sola arma seleccionada");
            Check(!first.ReceiveEquipment(State(1, 0)) && VisibleWeapons(first) == 1, "Estado repetido no cambia el equipo");
            Check(first.ReceiveEquipment(State(2, 2, 1, 2)) && VisibleWeapons(first) == 1, "Cambio de espada sin modelos acumulados");
            first.ReceiveEquipment(State(3, 0, 1, 2));
            Check(VisibleWeapons(first) == 0 && first.Equipment.CachedCount == 2, "Desequipar oculta y conserva la cache");
            first.ReceiveEquipment(State(4, 1, 1));
            Check(first.Equipment.CachedCount == 1 && VisibleWeapons(first) == 1, "Retirar del inventario elimina su copia");
            first.Equipment.BeginAction(new MultiplayerPlayerProtocol.Item(3, "sword"));
            Check(first.Equipment.CachedCount == 2 && VisibleWeapons(first) == 1, "Ataque recibido antes de su actualizacion de equipo");
            first.ReceiveEquipment(State(5, 1, 1));
            Check(first.Equipment.CachedCount == 2, "Actualizar inventario conserva el objeto de una accion activa");
            first.EndAttack();
            Check(first.Equipment.CachedCount == 1 && VisibleWeapons(first) == 1, "Fin de accion vuelve al arma equipada");
            first.ResetForFloor();
            Check(!first.HasInitialState && !first.gameObject.activeSelf, "Cambio de piso oculta y descarta estado anterior");
            first.ReceiveEquipment(State(10, 1, 1));
            first.ReceivePose(Pose(11, 100.2d, 2f, 1f));
            Check(!first.ReceivePose(Pose(9, 100.1d, 9f)), "Paquete de movimiento atrasado descartado");
            first.SetPlaying(true);
            first.RenderFrame(100.2d, 0.01f);
            Check(Mathf.Abs(first.transform.position.x - 1f) < 0.01f, "Interpolacion de movimiento con 100 ms de retraso");
            Check(first.Animator.updateMode == AnimatorUpdateMode.UnscaledTime, "Animador remoto independiente de la pausa local");
            first.Animator.Update(0.4f);
            Check(first.Animator.GetCurrentAnimatorStateInfo(0).IsName("Armature_Walk") ||
                first.Animator.GetNextAnimatorStateInfo(0).IsName("Armature_Walk"), "Speed activa la animacion de caminar existente");
            var attack = new MultiplayerPlayerProtocol.Attack { pose = Pose(12, 100.25d, 2f, 1f),
                item = new MultiplayerPlayerProtocol.Item(1, "sword") };
            Check(first.ReceiveAttack(attack) && !first.ReceiveAttack(attack), "Ataque fiable se procesa una sola vez");
            // interpolationDelay es float: avanzar mas alla del limite evita redondeo float/double.
            first.RenderFrame(100.36d, 0.01f);
            first.Animator.Update(0.02f);
            Check(first.Animator.GetCurrentAnimatorStateInfo(1).IsName("Armature_Attack_Sword") ||
                first.Animator.GetNextAnimatorStateInfo(1).IsName("Armature_Attack_Sword"), "Trigger activa el ataque de espada existente");
            first.EnableWeaponCollider();
            Check(HasActiveEffect(first),
                "Evento de ataque activa la estela visual");
            first.EndAttack();
            Check(!HasActiveEffect(first),
                "Fin de ataque limpia todas las estelas");
            first.SetPlaying(false);
            Check(!first.gameObject.activeSelf, "Transicion oculta la replica");
            Check(second.Equipment.CachedCount == 0, "Estado y equipo de otro jugador permanecen independientes");
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
    }

    private static int VisibleWeapons(MultiplayerRemotePlayer remote)
    {
        int count = 0;
        foreach (MultiplayerWeaponVisual visual in remote.Equipment.Hand.GetComponentsInChildren<MultiplayerWeaponVisual>(true))
            if (visual.gameObject.activeSelf) count++;
        return count;
    }

    private static bool HasActiveEffect(MultiplayerRemotePlayer remote) =>
        Array.Exists(remote.Equipment.Hand.GetComponentsInChildren<TrailRenderer>(true), trail => trail.emitting) ||
        Array.Exists(remote.Equipment.Hand.GetComponentsInChildren<ParticleSystem>(true), particle => particle.isEmitting);

    private static void CheckWeaponReplicas()
    {
        Scene preview = EditorSceneManager.NewPreviewScene();
        try
        {
            var types = MultiplayerPlayerPrefabBuilder.WeaponTypes;
            var catalog = new MultiplayerVisualDefinition[types.Length];
            for (int i = 0; i < types.Length; i++) catalog[i] = new MultiplayerVisualDefinition
            {
                typeId = types[i], localWeapon = MultiplayerPlayerPrefabBuilder.WeaponData(types[i]),
                remotePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MultiplayerPlayerPrefabBuilder.VisualPath(types[i])).GetComponent<MultiplayerWeaponVisual>()
            };
            foreach (string type in types)
            {
                var remote = ((GameObject)PrefabUtility.InstantiatePrefab(
                    AssetDatabase.LoadAssetAtPath<GameObject>(MultiplayerPlayerPrefabBuilder.RemotePath), preview)).GetComponent<MultiplayerRemotePlayer>();
                remote.Initialize(catalog, 0.1f);
                remote.ReceiveEquipment(new MultiplayerPlayerProtocol.Equipment { pose = Pose(), equippedId = 1,
                    items = new[] { new MultiplayerPlayerProtocol.Item(1, type) } });
                remote.SetPlaying(true);
                remote.Animator.Update(0f);
                var action = new MultiplayerPlayerProtocol.Attack { pose = Pose(2, 100.1d), item = new MultiplayerPlayerProtocol.Item(2, type) };
                Check(remote.ReceiveAttack(action) && !remote.ReceiveAttack(action), type + ": ataque unico antes del estado de equipo");
                remote.RenderFrame(100.21d, 0f);
                remote.Animator.Update(0.02f);
                string state = "Armature_Attack_" + char.ToUpperInvariant(type[0]) + type.Substring(1);
                Check(remote.Animator.GetCurrentAnimatorStateInfo(1).IsName(state) || remote.Animator.GetNextAnimatorStateInfo(1).IsName(state),
                    type + ": reproduce su propia animacion");
                Check(VisibleWeapons(remote) == 1 && remote.Equipment.CachedCount == 2, type + ": el ataque conserva su modelo independiente");
                // Soltar el arma durante la accion no elimina su visual antes del evento final.
                remote.ReceiveEquipment(new MultiplayerPlayerProtocol.Equipment { pose = Pose(3, 100.2d) });
                Check(VisibleWeapons(remote) == 1 && remote.Equipment.CachedCount == 1, type + ": soltar durante ataque conserva solo la accion");
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animation/Player/" + state + ".anim");
                Check(Array.Exists(AnimationUtility.GetAnimationEvents(clip), ev => ev.functionName == "EndAttack") &&
                    Array.Exists(AnimationUtility.GetAnimationEvents(clip), ev => ev.functionName == "EnableWeaponCollider"),
                    type + ": clip conserva ambos eventos de ataque");
                // Unity no despacha Animation Events de gameplay en una escena preview de Edit Mode.
                remote.EndAttack();
                Check(VisibleWeapons(remote) == 0 && remote.Equipment.CachedCount == 0, type + ": evento final limpia el arma soltada");
                remote.ResetForFloor();
                Check(!remote.HasInitialState && !remote.gameObject.activeSelf, type + ": cambio de piso descarta la replica anterior");
                UnityEngine.Object.DestroyImmediate(remote.gameObject);
            }
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("FASE 3: " + message);
        passed.Add(message);
    }

    [Serializable] private sealed class Report
    {
        public bool success;
        public string error;
        public string[] checks;
    }

    private static void WriteReport(bool success, string error)
    {
        Directory.CreateDirectory("Temp/phase3-verification");
        File.WriteAllText("Temp/phase3-verification/editor-tests.json", JsonUtility.ToJson(new Report
            { success = success, error = error, checks = passed.ToArray() }, true));
    }
}
#endif
