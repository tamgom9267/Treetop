#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Photon.Pun;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Regresion de trafico y combate en Play Mode, sin servidores ni escenas guardadas.</summary>
[InitializeOnLoad]
public static class MultiplayerCombatVerification
{
    private const string Running = "Treetop.CombatVerification";
    private const string SceneSetupKey = Running + ".Scenes";
    private static readonly List<string> checks = new List<string>();
    private static IEnumerator routine;
    private static string failure;
    private static double started;
    private static int lastFrame;
    [Serializable] private sealed class Report { public bool success; public string error; public string[] checks; }
    [Serializable] private sealed class Setup { public SceneSetup[] scenes; }

    static MultiplayerCombatVerification() { EditorApplication.playModeStateChanged += Changed; }

    public static void RunBatch()
    {
        MultiplayerGameplayPrefabBuilder.RestoreClientEnemyVisuals();
        RunChecks();
    }

    [MenuItem("Tools/Multiplayer/Probar trafico fijo y combate (Play Mode)")]
    public static void RunChecks()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Sal de Play antes de comprobar.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Guarda las escenas abiertas antes de comprobar.");
        MultiplayerGameplayVerification.RunChecks();
        SessionState.SetString(SceneSetupKey, JsonUtility.ToJson(new Setup { scenes = EditorSceneManager.GetSceneManagerSetup() }));
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Running, true);
        EditorApplication.isPlaying = true;
    }

    private static void Changed(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Running, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            checks.Clear(); failure = null; started = EditorApplication.timeSinceStartup; lastFrame = -1;
            routine = Verify(); EditorApplication.isPaused = true; EditorApplication.update += Tick;
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Running, false);
            const string output = "Logs/combat-verification";
            Directory.CreateDirectory(output);
            File.WriteAllText(output + "/results.json", JsonUtility.ToJson(new Report
                { success = failure == null, error = failure, checks = checks.ToArray() }, true));
            foreach (string phase in new[] { "phase3", "phase4" })
            {
                string source = "Temp/" + phase + "-verification/editor-tests.json";
                if (File.Exists(source)) File.Copy(source, output + "/" + phase + ".json", true);
            }
            var setup = JsonUtility.FromJson<Setup>(SessionState.GetString(SceneSetupKey, "{}"));
            if (setup.scenes != null && Array.Exists(setup.scenes, s => !string.IsNullOrEmpty(s.path)))
                EditorSceneManager.RestoreSceneManagerSetup(setup.scenes);
            Debug.Log("Combate multijugador: " + checks.Count + " comprobaciones; " + (failure ?? "OK"));
            if (Application.isBatchMode) EditorApplication.Exit(failure == null ? 0 : 1);
        }
    }

    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup - started > 90d) { failure = "Timeout"; Finish(); return; }
        if (!EditorApplication.isPlaying) return;
        if (lastFrame == Time.frameCount) { EditorApplication.Step(); return; }
        lastFrame = Time.frameCount;
        try { if (!routine.MoveNext()) Finish(); }
        catch (Exception error) { failure = error.ToString(); Debug.LogException(error); Finish(); }
    }

    private static void Finish()
    {
        EditorApplication.update -= Tick;
        (routine as IDisposable)?.Dispose();
        EditorApplication.isPaused = false;
        EditorApplication.isPlaying = false;
    }

    private static IEnumerator Verify()
    {
        CheckClocks();
        var scene = SceneManager.CreateScene("MultiplayerGameScene"); SceneManager.SetActiveScene(scene);
        var sessionRoot = new GameObject("Combat verification session"); sessionRoot.SetActive(false);
        var service = sessionRoot.AddComponent<PhotonPartyService>();
        var run = sessionRoot.AddComponent<MultiplayerRunSession>();
        sessionRoot.SetActive(true);
        yield return null;
        PhotonNetwork.OfflineMode = true; service.CreateParty("CombatTest"); run.enabled = false;
        Property(run, "RunId", "22222222222222222222222222222222"); Property(run, "FloorNumber", 1);
        Property(run, "Phase", MultiplayerRunPhase.Playing); Set(run, "activeRun", true); Set(run, "localBuiltKey", run.CurrentKey);
        PhotonNetwork.SendRate = 25;
        var root = Object.Instantiate(Resources.Load<GameObject>(MultiplayerPlayers.ResourcePath));
        var tuner = root.GetComponent<MultiplayerTrafficTuner>(); if (tuner != null) Object.DestroyImmediate(tuner);
        var players = root.GetComponent<MultiplayerPlayers>();
        Check(players.Initialize(run, out string error), "Coordinador inicializa sin medidor: " + error);
        Check(PhotonNetwork.SendRate == 125, "Salida solicitada 120 se representa como 125 Hz (8 ms)");
        Check(players.Traffic.playerStateSendHz == 90f && players.Traffic.enemyStateSendHz == 90f, "Jugadores y enemigos fijos a 90 Hz");
        tuner = root.AddComponent<MultiplayerTrafficTuner>();
        var serialized = new SerializedObject(tuner);
        Check(serialized.FindProperty("rates") == null && serialized.FindProperty("photonSendRateHz") == null &&
            new SerializedObject(players).FindProperty("poseFrequency") == null, "No quedan ajustes de frecuencia serializados");
        Check(tuner.IsMeasuringTraffic && PhotonNetwork.SendRate == 125, "Medidor recopila sin escribir frecuencias");
        tuner.enabled = false;
        Check(PhotonNetwork.SendRate == 125, "Deshabilitar medidor mantiene salida fija");
        tuner.enabled = true; Object.DestroyImmediate(tuner);
        Check(PhotonNetwork.SendRate == 125, "Eliminar medidor mantiene salida fija");
        Check(players.Initialize(run, out error), "Reinicializar coordinador es seguro");
        var world = players.World;
        world.Resume();
        var slimeChecks = MultiplayerSlimeVerification.CheckSlimes(world);
        while (slimeChecks.MoveNext()) yield return slimeChecks.Current;
        Property(run, "FloorNumber", 2); players.BeginFloor(run.RunId, 2);
        Check(players.Traffic.enemyStateSendHz == 90f && PhotonNetwork.SendRate == 125, "Cambiar de piso conserva frecuencias fijas");
        players.Shutdown(); Check(PhotonNetwork.SendRate == 25, "Cerrar coordinador restaura salida previa");
        Object.DestroyImmediate(root); Check(PhotonNetwork.SendRate == 25, "Destruccion repetida no cambia salida restaurada");
        service.LeaveParty(); PhotonNetwork.Disconnect(); Object.DestroyImmediate(sessionRoot);
        foreach (var obj in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (obj.name.StartsWith("CombatVerification", StringComparison.Ordinal)) Object.DestroyImmediate(obj);
        yield return null;
    }

    private static void CheckClocks()
    {
        foreach (int fps in new[] { 30, 60, 90, 120, 144, 240, 569 })
        {
            var clock = new MultiplayerPlayers.TrafficClock(); int count = 0;
            for (int i = 0; i < fps * 10; i++) if (clock.Tick(i / (double)fps, 90f)) count++;
            Check(Math.Abs(count - Math.Min(fps, 90) * 10) <= 1, "Cadencia efectiva a " + fps + " FPS: " + count / 10f + " Hz");
            Check(clock.Tick(1000d, 90f) && !clock.Tick(1000d, 90f), "Tras pausa a " + fps + " FPS no recupera envios en rafaga");
        }
    }

    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); checks.Add(message); }
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static object Get(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    private static void Property(object target, string name, object value) => target.GetType().GetProperty(name).GetSetMethod(true).Invoke(target, new[] { value });
    private static void Call(object target, string name) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
}
#endif
