#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Photon.Pun;
using Photon.Realtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Regresion en escenas temporales. No guarda escenas ni utiliza servidores de Photon.</summary>
[InitializeOnLoad]
public static class PersistentSessionLifecycleVerification
{
    private const string RunningKey = "Treetop.LifecycleVerification.Running";
    private const string SetupKey = "Treetop.LifecycleVerification.Setup";
    private const string NetworkKey = "Treetop.LifecycleVerification.Network";
    private static string ReportPath => Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/lifecycle-verification/results.json"));
    private static readonly List<string> passed = new List<string>();
    private static readonly Stack<IEnumerator> routines = new Stack<IEnumerator>();
    private static int lastFrame = -1;
    private static string failure;
    private static double startedAt;
    [Serializable] private sealed class Setup { public SceneSetup[] scenes; }
    [Serializable] private sealed class Report { public bool success; public string error; public string[] checks; }

    static PersistentSessionLifecycleVerification()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    [MenuItem("Tools/Multiplayer/Probar ciclo de vida (Play Mode)")]
    public static void RunChecks()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Ejecuta esta verificacion fuera de Play Mode.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("Guarda las escenas abiertas antes de ejecutar la verificacion.");

        // Incluye la regresion de fase 3; cada prueba utiliza sus propias escenas de preview.
        MultiplayerGameplayVerification.RunChecks();
        SessionState.SetString(SetupKey, JsonUtility.ToJson(new Setup { scenes = EditorSceneManager.GetSceneManagerSetup() }));
        SessionState.SetBool(RunningKey, true);
        SessionState.SetBool(NetworkKey, false);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorApplication.isPlaying = true;
    }

    public static void RunBatch() { RunChecks(); }

    [MenuItem("Tools/Multiplayer/Probar ciclo de vida (con red)")]
    public static void RunBatchWithNetwork()
    {
        RunChecks();
        SessionState.SetBool(NetworkKey, true);
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(RunningKey, false)) return;
        Debug.Log("Verificacion de ciclo de vida: " + state);
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            Application.runInBackground = true;
            EditorApplication.isPaused = Application.isBatchMode;
            passed.Clear(); failure = null; routines.Clear(); lastFrame = -1;
            startedAt = EditorApplication.timeSinceStartup;
            routines.Push(CheckLifecycle());
            EditorApplication.update += Tick;
        }
        if (state != PlayModeStateChange.EnteredEditMode) return;
        SessionState.SetBool(RunningKey, false);
        var setup = JsonUtility.FromJson<Setup>(SessionState.GetString(SetupKey, "{}"));
        if (setup.scenes != null && Array.Exists(setup.scenes, scene => !string.IsNullOrEmpty(scene.path)))
            EditorSceneManager.RestoreSceneManagerSetup(setup.scenes);
        if (Application.isBatchMode)
        {
            var report = JsonUtility.FromJson<Report>(File.ReadAllText(ReportPath));
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/lifecycle-verification"));
            Directory.CreateDirectory(output);
            File.Copy(ReportPath, Path.Combine(output, "results.json"), true);
            foreach (string phase in new[] { "phase3", "phase4" })
            {
                string source = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/" + phase + "-verification/editor-tests.json"));
                if (File.Exists(source)) File.Copy(source, Path.Combine(output, phase + ".json"), true);
            }
            string network = Path.Combine(Path.GetDirectoryName(ReportPath), "network.json");
            if (File.Exists(network)) File.Copy(network, Path.Combine(output, "network.json"), true);
            Debug.Log("Resultado de ciclo de vida: correcta=" + report.success + "; " + output);
            EditorApplication.Exit(report.success ? 0 : 1);
        }
    }

    private static void Tick()
    {
        EditorApplication.QueuePlayerLoopUpdate();
        double limit = SessionState.GetBool(NetworkKey, false) ? 180d : 60d;
        if (EditorApplication.timeSinceStartup - startedAt > limit)
        { failure = "La verificacion excedio " + limit + " segundos."; Finish(); return; }
        if (!EditorApplication.isPlaying) return;
        if (lastFrame == Time.frameCount)
        {
            if (Application.isBatchMode) EditorApplication.Step();
            return;
        }
        lastFrame = Time.frameCount;
        try
        {
            while (routines.Count > 0)
            {
                IEnumerator current = routines.Peek();
                if (!current.MoveNext()) { routines.Pop(); continue; }
                if (current.Current is IEnumerator nested) { routines.Push(nested); continue; }
                return;
            }
            Finish();
        }
        catch (Exception error) { failure = error.ToString(); Debug.LogException(error); Finish(); }
    }

    private static void Finish()
    {
        EditorApplication.update -= Tick;
        while (routines.Count > 0) (routines.Pop() as IDisposable)?.Dispose();
        Time.timeScale = 1f;
        PhotonNetwork.Disconnect();
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        File.WriteAllText(ReportPath, JsonUtility.ToJson(new Report
            { success = failure == null, error = failure, checks = passed.ToArray() }, true));
        Debug.Log("Ciclo de vida: " + passed.Count + " comprobaciones. Informe: " + ReportPath);
        EditorApplication.isPlaying = false;
    }

    private static IEnumerator CheckLifecycle()
    {
        Scene village = SceneManager.CreateScene("VillageMenuScene");
        Scene game = SceneManager.CreateScene("GameScene");
        Scene multiplayer = SceneManager.CreateScene("MultiplayerGameScene");
        Scene menu = SceneManager.CreateScene("StartMenuScene");
        Scene tutorial = SceneManager.CreateScene("TutorialScene");
        Check(PersistentScenePolicy.IsAllowed(village) && PersistentScenePolicy.IsAllowed(game) &&
            PersistentScenePolicy.IsAllowed(multiplayer) && !PersistentScenePolicy.IsAllowed(menu) &&
            !PersistentScenePolicy.IsAllowed(tutorial) && !PersistentScenePolicy.IsAllowed(default), "Lista exacta de escenas permitidas");

        SceneManager.SetActiveScene(village);
        NewestSingleton player = Player(village, Vector3.zero);
        var inventoryMarker = new GameObject("Inventario conservado"); inventoryMarker.transform.SetParent(player.transform);
        NewestSingleton duplicate = Player(village, Vector3.right * 7);
        Check(NewestSingleton.Instance == player && player.transform.position == Vector3.right * 7 &&
            !duplicate.gameObject.activeSelf, "Duplicado conserva personaje, inventario y posicion de entrada");
        yield return null;
        Check(duplicate == null, "Duplicado del personaje destruido al terminar el frame");
        SceneManager.SetActiveScene(game); yield return null;
        SceneManager.SetActiveScene(multiplayer); yield return null;
        Scene auxiliary = SceneManager.CreateScene("Auxiliar de verificacion"); yield return null;
        Check(NewestSingleton.Instance == player && inventoryMarker != null, "Escenas permitidas y carga aditiva conservan el personaje");
        var tutorialPlayer = new GameObject("Personaje propio del tutorial"); tutorialPlayer.tag = "Player";
        SceneManager.MoveGameObjectToScene(tutorialPlayer, tutorial);
        Time.timeScale = 0f;
        SceneManager.SetActiveScene(menu);
        Check(NewestSingleton.Instance == null && !player.gameObject.activeSelf, "Personaje desaparece inmediatamente aun en pausa");
        yield return null;
        Check(player == null && inventoryMarker == null && tutorialPlayer != null, "Solo se elimina la jerarquia del personaje persistente");
        SceneManager.SetActiveScene(tutorial);
        NewestSingleton rejected = Player(tutorial, Vector3.zero);
        Check(NewestSingleton.Instance == null && !rejected.gameObject.activeSelf, "No se acepta un singleton creado en el tutorial");
        yield return null;
        Time.timeScale = 1f;
        SceneManager.SetActiveScene(village);
        player = Player(village, Vector3.zero);
        NewestSingleton.RequestVillagePlayerReset();
        NewestSingleton fresh = Player(village, Vector3.forward);
        Check(NewestSingleton.Instance == fresh && !player.gameObject.activeSelf, "Completar mazmorra sigue reemplazando al personaje");
        yield return null;
        Check(player == null, "La destruccion del anterior no borra la instancia nueva");

        PhotonPartyService service = Session(village);
        MultiplayerRunSession run = service.GetComponent<MultiplayerRunSession>();
        PhotonPartyService extra = Session(village);
        Check(PhotonPartyService.Instance == service && MultiplayerRunSession.Instance == run &&
            !extra.gameObject.activeSelf, "Los duplicados normales conservan la sesion original");
        yield return null;
        PhotonNetwork.OfflineMode = true;
        service.CreateParty("LifecycleTest");
        Check(service.IsInParty && service.IsHost, "Salida ejercitada con una sala host de Photon en modo offline");
        var sync = Object.Instantiate(Resources.Load<GameObject>(MultiplayerPlayers.ResourcePath), run.transform).GetComponent<MultiplayerPlayers>();
        Check(sync.Initialize(run, out string error), "MultiplayerPlayers inicializa sus recursos: " + error);
        SetField(run, "playerSync", sync);
        SceneManager.SetActiveScene(game); yield return null;
        Check(PhotonPartyService.Instance == service && MultiplayerWorldSession.Instance == sync.World,
            "La sesion y el mundo persisten dentro de las escenas permitidas");
        SceneManager.SetActiveScene(menu); yield return null; yield return null;
        Check(!PhotonNetwork.IsConnected && !PhotonNetwork.InRoom && service == null && sync == null &&
            PhotonPartyService.Instance == null && MultiplayerRunSession.Instance == null && MultiplayerWorldSession.Instance == null,
            "Salir de una sala limpia conexion, gestores, sincronizacion y referencia al mundo");

        foreach (PartyOperation operation in new[] { PartyOperation.Joining, PartyOperation.Creating })
        {
            SceneManager.SetActiveScene(village);
            service = Session(village); run = service.GetComponent<MultiplayerRunSession>();
            PhotonNetwork.OfflineMode = true;
            SetOperation(service, operation);
            SceneManager.SetActiveScene(menu);
            Check(service.IsShuttingDown && service.IsBusy && PhotonPartyService.AvailableInstance == null,
                operation + ": la operacion pendiente se cierra en segundo plano");
            service.BeginSceneExitShutdown();
            service.ConnectIfNeeded(); service.CreateParty("NoDebeCrearse"); service.JoinParty("NoDebeEntrar", "12345");
            Check(service.Operation == operation && !run.enabled && !run.BeginRun(7, out _),
                operation + ": cierre repetido y nuevas operaciones no reactivan la partida");
            MultiplayerRunSession.ReturnInvalidSceneToVillage();
            Check(SceneManager.GetActiveScene() == menu, "Callback tardio no redirige del menu a la aldea");

            SceneManager.SetActiveScene(village);
            PhotonPartyService pending = Session(village);
            MultiplayerRunSession pendingRun = pending.GetComponent<MultiplayerRunSession>();
            fresh = Player(village, Vector3.zero);
            Check(!pending.gameObject.activeSelf && PhotonPartyService.Instance == service && NewestSingleton.Instance == fresh,
                "Reingreso conserva nueva sesion pendiente y activa inmediatamente al personaje");
            var views = new GameObject("Vistas de la aldea");
            var villageMenu = views.AddComponent<VillageMultiplayerMenu>();
            var roster = views.AddComponent<PartyRosterView>();
            var access = views.AddComponent<VillagePartyAccessController>();
            villageMenu.OpenFromInteraction();
            Check(!villageMenu.IsModalOpen && villageMenu.TryOpenExternalMenu(), "Solo el multijugador espera; los menus de juego local pueden abrirse");
            villageMenu.CloseExternalMenu();
            yield return null;
            if (operation == PartyOperation.Joining) service.OnJoinRoomFailed(0, "Verificacion de entrada cancelada");
            else service.OnCreateRoomFailed(0, "Verificacion de creacion cancelada");
            yield return null; yield return null; yield return null;
            Check(service == null && pending.gameObject.activeSelf && PhotonPartyService.Instance == pending &&
                MultiplayerRunSession.Instance == pendingRun && pendingRun.enabled && !PhotonNetwork.IsConnected,
                "Sesion nueva activada despues de desconectar; destruccion vieja no borra referencias nuevas");
            Check(GetField(roster, "service") == pending && GetField(access, "service") == pending &&
                GetField(villageMenu, "service") == pending, "Listado, acceso y menu se enlazan al servicio nuevo");
            Object.Destroy(views);
            SceneManager.SetActiveScene(menu); yield return null; yield return null;
        }

        SceneManager.SetActiveScene(village);
        service = Session(village);
        PhotonNetwork.OfflineMode = true; SetOperation(service, PartyOperation.Creating);
        SceneManager.SetActiveScene(menu);
        SceneManager.SetActiveScene(village);
        PhotonPartyService abandoned = Session(village);
        SceneManager.SetActiveScene(tutorial);
        AsyncOperation unload = SceneManager.UnloadSceneAsync(village);
        while (!unload.isDone) yield return null;
        service.OnCreateRoomFailed(0, "Verificacion de escena abandonada");
        yield return null; yield return null;
        Check(abandoned == null && PhotonPartyService.Instance == null, "No se activa una sesion pendiente cuya escena ya se descargo");

        village = SceneManager.CreateScene("VillageMenuScene");
        SceneManager.SetActiveScene(village);
        service = Session(village);
        float dispatch = PhotonNetwork.MinimalTimeScaleToDispatchInFixedUpdate;
        PhotonNetwork.OfflineMode = true; SetOperation(service, PartyOperation.Joining);
        Time.timeScale = 0f;
        double started = Time.realtimeSinceStartupAsDouble;
        SceneManager.SetActiveScene(menu);
        Check(PhotonNetwork.MinimalTimeScaleToDispatchInFixedUpdate >= 0f, "Callbacks de Photon habilitados durante la pausa");
        while (service != null && Time.realtimeSinceStartupAsDouble - started < 8d) yield return null;
        Check(service == null && !PhotonNetwork.IsConnected && Time.realtimeSinceStartupAsDouble - started >= 5d,
            "Timeout de cinco segundos usa tiempo real y fuerza desconexion de una entrada pendiente");
        Check(PhotonNetwork.MinimalTimeScaleToDispatchInFixedUpdate == dispatch, "Configuracion de despacho restaurada despues del cierre");
        Time.timeScale = 1f;
        Check(tutorialPlayer != null, "Los ciclos completos no eliminan al personaje propio del tutorial");
        if (SessionState.GetBool(NetworkKey, false)) yield return PersistentSessionNetworkVerification.RunChecks();
        yield return CheckProjectScenes();
    }

    private static IEnumerator CheckProjectScenes()
    {
        AsyncOperation load = SceneManager.LoadSceneAsync("VillageMenuScene");
        while (!load.isDone) yield return null;
        yield return null;
        NewestSingleton player = NewestSingleton.Instance;
        PhotonPartyService service = PhotonPartyService.AvailableInstance;
        Check(player != null && service != null && MultiplayerRunSession.Instance != null,
            "La escena real de aldea crea personaje y gestores con sus referencias del Inspector");
        PlayerController controller = player.GetComponentInChildren<PlayerController>(true);
        PlayerInventory inventory = controller.Inventory;
        load = SceneManager.LoadSceneAsync("GameScene");
        while (!load.isDone) yield return null;
        yield return null;
        Check(NewestSingleton.Instance == player && PhotonPartyService.AvailableInstance == service &&
            controller.Inventory == inventory, "La mazmorra real conserva personaje, inventario y servicio");
        PauseController pause = player.GetComponentInChildren<PauseController>(true);
        Check(pause != null, "El personaje real conserva su controlador de pausa");
        pause.GoToMainMenu();
        yield return null; yield return null;
        Check(SceneManager.GetActiveScene().name == "StartMenuScene" && player == null &&
            PhotonPartyService.Instance == null && MultiplayerRunSession.Instance == null,
            "El boton existente de pausa carga el menu real y elimina los objetos persistentes");
        load = SceneManager.LoadSceneAsync("TutorialScene");
        while (!load.isDone) yield return null;
        yield return null;
        Check(NewestSingleton.Instance == null && PhotonPartyService.Instance == null &&
            Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None).Length == 1,
            "El tutorial real funciona con su propio personaje y sin singletons persistentes");
        load = SceneManager.LoadSceneAsync("VillageMenuScene");
        while (!load.isDone) yield return null;
        yield return null;
        player = NewestSingleton.Instance; service = PhotonPartyService.AvailableInstance;
        Check(player != null && service != null, "Salir del tutorial recrea los objetos reales de la aldea");
        NewestSingleton.RequestVillagePlayerReset();
        load = SceneManager.LoadSceneAsync("VillageMenuScene");
        while (!load.isDone) yield return null;
        yield return null;
        Check(player == null && NewestSingleton.Instance != null && PhotonPartyService.AvailableInstance == service,
            "El reinicio al completar mazmorra sigue reemplazando solo al personaje en la escena real");
        load = SceneManager.LoadSceneAsync("StartMenuScene");
        while (!load.isDone) yield return null;
        yield return null;
        Check(NewestSingleton.Instance == null && PhotonPartyService.Instance == null,
            "El ultimo regreso al menu real termina sin instancias persistentes propias");
    }

    private static NewestSingleton Player(Scene scene, Vector3 position)
    {
        var root = new GameObject("Personaje persistente de verificacion"); root.SetActive(false);
        SceneManager.MoveGameObjectToScene(root, scene); root.transform.position = position;
        var player = root.AddComponent<NewestSingleton>(); root.SetActive(true); return player;
    }

    private static PhotonPartyService Session(Scene scene)
    {
        var root = new GameObject("MultiplayerSession de verificacion"); root.SetActive(false);
        SceneManager.MoveGameObjectToScene(root, scene);
        var service = root.AddComponent<PhotonPartyService>(); root.AddComponent<MultiplayerRunSession>();
        root.SetActive(true); return service;
    }

    private static void SetOperation(PhotonPartyService service, PartyOperation operation)
    {
        typeof(PhotonPartyService).GetProperty("Operation").GetSetMethod(true).Invoke(service, new object[] { operation });
    }
    private static void SetField(object target, string name, object value)
    {
        target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
    }
    private static Object GetField(object target, string name)
    {
        return (Object)target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Ciclo de vida: " + message);
        passed.Add(message);
    }
}
#endif
