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
        MultiplayerGameplayPrefabBuilder.UpgradeClientEnemyVisuals();
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
        var template = new GameObject("CombatVerificationHit"); template.SetActive(false);
        var deathTemplate = new GameObject("CombatVerificationDeath"); deathTemplate.SetActive(false);
        for (int type = 1; type <= 2; type++)
        {
            var enemy = SpawnEnemy(type, false, world);
            Set(enemy, "hitEffectPrefab", template); Set(enemy, "deathEffectPrefab", deathTemplate);
            var visual = enemy.transform.Find("CombatVisual");
            Check(visual != null && enemy.GetComponent<MeshRenderer>() == null && visual.GetComponent<MeshRenderer>() != null,
                "Slime " + type + ": malla separada; fisica y Animator en raiz");
            var origin = enemy.transform.position - Vector3.right * 2f;
            Vector3 initialPosition = enemy.transform.position;
            Vector3 colliderCenter = enemy.GetComponent<Collider>().bounds.center;
            int effects = Effects("CombatVerificationHit");
            Check(enemy.PredictHit(2, 10, origin) && !enemy.PredictHit(2, 10, origin), "Slime " + type + ": prediccion unica por ataque");
            Check(enemy.Health == 10 && Effects("CombatVerificationHit") == effects + 1, "Prediccion muestra un efecto sin aplicar dano");
            Set(enemy, "recoilStartedAt", Time.realtimeSinceStartupAsDouble - 0.1d); Call(enemy, "LateUpdate");
            Vector3 offset = visual.position - enemy.transform.position;
            Check(Mathf.Abs(offset.x - 0.5f) < 0.02f && Mathf.Abs(offset.y - 0.15f) < 0.02f, "Retroceso visual respeta limites en unidades de mundo");
            Check(enemy.transform.position == initialPosition && enemy.GetComponent<Collider>().bounds.center == colliderCenter &&
                enemy.GetComponent<Rigidbody>().isKinematic, "Prediccion no mueve raiz, collider ni Rigidbody");
            enemy.transform.localScale *= 0.5f; Call(enemy, "LateUpdate");
            Check(Vector3.Distance(visual.position - enemy.transform.position, offset) < 0.03f, "Escala animada no multiplica el retroceso");
            var state = State(enemy, 1, 1, 2, 10, 5);
            enemy.Receive(state); enemy.Receive(state);
            Check(Effects("CombatVerificationHit") == effects + 1 && enemy.Health == 5, "Confirmaciones repetidas aplican vida sin duplicar impacto");
            Set(enemy, "reconcileStartedAt", Time.realtimeSinceStartupAsDouble - 0.11d); Call(enemy, "LateUpdate");
            Check(visual.localPosition == Vector3.zero, "Confirmacion absorbe desplazamiento en 0,10 segundos");
            Check(enemy.PredictHit(2, 11, origin), "Ataque posterior puede volver a anticiparse");
            effects = Effects("CombatVerificationHit");
            enemy.Receive(State(enemy, 3, 3, 3, 20, 2));
            enemy.Receive(State(enemy, 2, 2, 2, 11, 3));
            Check(Effects("CombatVerificationHit") == effects + 1 && enemy.Health == 2, "Otro jugador conserva feedback; confirmacion antigua propia se deduplica");
            Check((bool)((IDictionary)Get(enemy, "predictedHits"))[((long)2 << 32) | 11L], "Correlacion se resuelve antes del filtro de revision");
            Check(enemy.PredictHit(2, 12, origin), "Nuevo golpe antes del rechazo");
            enemy.RejectPredictedHit(2, 12); effects = Effects("CombatVerificationHit");
            enemy.Receive(State(enemy, 4, 4, 2, 12, 1));
            Check(Effects("CombatVerificationHit") == effects && enemy.Health == 1, "Rechazo y confirmacion tardia no repiten efectos");
            Check(enemy.PredictHit(2, 13, origin), "Golpe local pendiente antes de muerte confirmada");
            enemy.Receive(State(enemy, 5, 5, 2, 13, 0));
            var animator = enemy.GetComponent<Animator>(); animator.Update(0f);
            int death = Animator.StringToHash("Base Layer.SlimeDeath");
            Check(enemy.Health == 0 && animator.GetCurrentAnimatorStateInfo(0).fullPathHash == death && !animator.IsInTransition(0),
                "Muerte confirmada inicia directamente sin transicion");
            animator.Update(0.1f); float progress = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
            enemy.Receive(State(enemy, 6, 4, 3, 20, 1)); Call(enemy, "Update"); animator.Update(0f);
            Check(enemy.Health == 0 && animator.GetCurrentAnimatorStateInfo(0).fullPathHash == death &&
                animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= progress, "Estado vivo posterior no revive ni reinicia animacion de muerte");
            Check(!enemy.PredictHit(2, 14, origin), "No se predicen golpes sobre un enemigo muerto");
            enemy.Receive(State(enemy, 7, 5, 2, 13, 0));
            Check(Effects("CombatVerificationDeath") == 1, "Efecto de muerte se reproduce una vez por slime");
            Object.DestroyImmediate(enemy.gameObject);

            var host = SpawnEnemy(type, true, world); host.GetComponent<HostSlimeMovement>().enabled = false;
            Set(host, "hitEffectPrefab", template); Set(host, "deathEffectPrefab", deathTemplate);
            host.TakeAuthoritativeHit(10, origin, 2, 15); var captured = host.Capture(1);
            host.GetComponent<Animator>().Update(0f);
            Check(captured.health == 0 && captured.actor == 2 && captured.attack == 15 && captured.animation == death,
                "Host publica muerte y correlacion en la misma operacion");
            Check(!host.PredictHit(2, 16, origin), "Host nunca aplica prediccion cliente");
            host.GetComponent<Animator>().Update(0.7f);
            Check(host.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).normalizedTime >= 1f, "Clip de muerte conserva su duracion completa");
            Object.DestroyImmediate(host.gameObject);
            // Mantener separado el recuento de muertes cliente del siguiente tipo.
            foreach (var obj in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (obj.name == "CombatVerificationDeath(Clone)") Object.DestroyImmediate(obj);
        }
        var pending = SpawnEnemy(1, false, world);
        pending.PredictHit(2, 99, pending.transform.position - Vector3.right);
        var enemies = (IDictionary)Get(world, "enemies"); enemies.Add(pending.Id, pending);
        int localActor = MultiplayerGameplayProtocol.LocalActor;
        pending.PredictHit(localActor, 100, pending.transform.position - Vector3.right);
        var rejection = new MultiplayerWorldMessage { op = "hitRejected", id = pending.Id, actor = localActor, attack = 100 };
        var decoded = JsonUtility.FromJson<MultiplayerWorldMessage>(JsonUtility.ToJson(rejection));
        Check(decoded.actor == localActor && decoded.attack == 100 && decoded.id == pending.Id, "Protocolo conserva correlacion de rechazo al serializar");
        typeof(MultiplayerWorldSession).GetMethod("Apply", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(world, new object[] { decoded });
        Check((bool)((IDictionary)Get(pending, "predictedHits"))[((long)localActor << 32) | 100L], "Rechazo del mundo resuelve la prediccion del destinatario");
        pending.PredictHit(localActor + 10, 101, pending.transform.position - Vector3.right);
        rejection.actor = localActor + 10; rejection.attack = 101;
        typeof(MultiplayerWorldSession).GetMethod("Apply", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(world, new object[] { rejection });
        Check(!(bool)((IDictionary)Get(pending, "predictedHits"))[((long)(localActor + 10) << 32) | 101L], "Rechazo de otro destinatario no cancela la prediccion local");
        world.Suspend();
        Check(((IDictionary)Get(pending, "predictedHits")).Count == 0, "Suspender limpia predicciones");
        Property(run, "FloorNumber", 2); players.BeginFloor(run.RunId, 2);
        Check(players.Traffic.enemyStateSendHz == 90f && PhotonNetwork.SendRate == 125, "Cambiar de piso conserva frecuencias fijas");
        Object.DestroyImmediate(pending.gameObject);
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

    private static MultiplayerEnemy SpawnEnemy(int type, bool host, MultiplayerWorldSession world)
    {
        var root = Object.Instantiate(Resources.Load<GameObject>("Multiplayer/" + (host ? "Host" : "Client") + "/Enemigos/Enemigo" + type));
        var enemy = root.GetComponent<MultiplayerEnemy>(); enemy.Initialize(world, "test:" + type, "test", type); return enemy;
    }
    private static MultiplayerWorldMessage State(MultiplayerEnemy enemy, int revision, int hit, int actor, int attack, int health) =>
        new MultiplayerWorldMessage { op = "enemy", id = enemy.Id, revision = revision, hit = hit, actor = actor, attack = attack, health = health,
            position = enemy.transform.position, rotation = enemy.transform.rotation, animation = Animator.StringToHash("Base Layer.SlimeJumpCharge"),
            time = PhotonNetwork.Time - 0.2d };
    private static int Effects(string name)
    {
        int count = 0;
        foreach (var obj in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (obj.name == name + "(Clone)") count++;
        return count;
    }
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); checks.Add(message); }
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static object Get(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    private static void Property(object target, string name, object value) => target.GetType().GetProperty(name).GetSetMethod(true).Invoke(target, new[] { value });
    private static void Call(object target, string name) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
}
#endif
