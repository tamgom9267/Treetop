using System;
using System.Collections;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using Hashtable = ExitGames.Client.Photon.Hashtable;

/// <summary>Agregar junto a PhotonPartyService en MultiplayerSession, fuera de los paneles.</summary>
[DefaultExecutionOrder(-150)]
public class MultiplayerRunSession : MonoBehaviourPunCallbacks
{
    public static MultiplayerRunSession Instance { get; private set; }
    [SerializeField] private string multiplayerSceneName = "MultiplayerGameScene";
    [SerializeField] private string villageSceneName = "VillageMenuScene";
    [SerializeField, Min(5f)] private float transitionTimeout = 60f;
    [Header("UI hija del objeto persistente")]
    [SerializeField] private GameObject loadingPanel;
    [SerializeField] private TMP_Text loadingText;

    private const string RunKey = "world.run";
    private const string SeedKey = "world.seed";
    private const string FloorKey = "world.floor";
    private const string PhaseKey = "world.phase";
    private const string JsonKey = "world.json";
    private const string ErrorKey = "world.error";
    private const string ReadyKey = "world.ready";
    private const string FailureKey = "world.failed";
    private const string ReturnedKey = "world.returned";

    private static string pendingVillageNotice;
    private static bool invalidRedirectPending;
    private PhotonPartyService party;
    private HostWorldGenerator hostGenerator;
    private ClientWorldGenerator clientGenerator;
    private MultiplayerFloorData floorData;
    private string floorJson = string.Empty;
    private string buildRequestedKey;
    private string localBuiltKey;
    private string reportedFailureKey;
    private string runError;
    private bool activeRun;
    private bool ending;
    private bool publicationPending;
    private bool publishingFirstFloor;
    private bool publishingPlaying;
    private AsyncOperation sceneLoad;
    private double deadline;
    private Coroutine returnRoutine;
    private Vector3 returnPosition;
    private Quaternion returnRotation;
    private bool hasReturnPose;
    private PlayerController frozenPlayer;
    private Rigidbody frozenBody;
    private PauseController frozenPause;
    private bool playerWasEnabled;
    private bool bodyWasKinematic;
    private bool pauseWasEnabled;
    private bool frozen;
    private MultiplayerPlayers playerSync;
    private string readyRequestedKey;
    private bool settlementRequested;
    private bool completedRun;
    private bool completionClearRequested;
    private bool initialized;
    private bool sceneExitPrepared;

    public string RunId { get; private set; }
    public int BaseSeed { get; private set; }
    public int FloorNumber { get; private set; }
    public MultiplayerRunPhase Phase { get; private set; }
    // Los menus siguen bloqueados hasta que toda la party termine de regresar.
    public bool IsRunning => !sceneExitPrepared && activeRun && (!ending || completedRun);
    public bool IsPlaying => IsRunning && Phase == MultiplayerRunPhase.Playing && !publicationPending &&
        localBuiltKey == CurrentKey;
    public string CurrentKey => MultiplayerMapMath.Key(RunId, FloorNumber);
    public bool IsInMultiplayerScene => SceneManager.GetActiveScene().name == multiplayerSceneName;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
        pendingVillageNotice = null;
        invalidRedirectPending = false;
    }

    public static bool TryConsumeVillageNotice(out string message)
    {
        message = pendingVillageNotice;
        pendingVillageNotice = null;
        return !string.IsNullOrEmpty(message);
    }

    private void Awake()
    {
        InitializeSingleton();
    }

    internal bool InitializeSingleton()
    {
        if (initialized) return Instance == this && !sceneExitPrepared;
        ShowLoading(false, string.Empty);
        PhotonPartyService owner = GetComponent<PhotonPartyService>();
        if (owner != null)
        {
            if (!owner.InitializeSingleton()) return false;
        }
        else if (!PersistentScenePolicy.IsAllowed(gameObject.scene))
        {
            gameObject.SetActive(false);
            Destroy(gameObject);
            return false;
        }
        if (Instance != null && Instance != this && !Instance.sceneExitPrepared)
        { enabled = false; Destroy(this); return false; }
        initialized = true;
        Instance = this;
        DontDestroyOnLoad(gameObject);
        return true;
    }

    public override void OnEnable()
    {
        if (Instance != this || sceneExitPrepared) return;
        base.OnEnable();
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
        AttachParty();
    }

    public override void OnDisable()
    {
        base.OnDisable();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        if (party != null)
        {
            party.PartyChanged -= OnPartyChanged;
            party.PartyEnded -= OnPartyEnded;
        }
        party = null;
        StopPlayerSync();
        ReleasePlayer();
        if (completedRun) NewestSingleton.CancelVillagePlayerReset();
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    private void Start()
    {
        if (Instance == this) OnActiveSceneChanged(default, SceneManager.GetActiveScene());
    }

    private void OnActiveSceneChanged(Scene previous, Scene current)
    {
        if (PersistentScenePolicy.IsAllowed(current)) return;
        PhotonPartyService owner = GetComponent<PhotonPartyService>();
        if (owner != null) owner.BeginSceneExitShutdown();
        else
        {
            PrepareForSceneExit();
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }

    public void PrepareForSceneExit()
    {
        if (sceneExitPrepared) return;
        sceneExitPrepared = true;
        StopAllCoroutines();
        returnRoutine = null;
        pendingVillageNotice = null;
        invalidRedirectPending = false;
        NewestSingleton.CancelVillagePlayerReset();
        ReleasePlayer();
        ShowLoading(false, string.Empty);
        ResetRunState();
        enabled = false;
    }

    private void AttachParty()
    {
        if (sceneExitPrepared || party != null || PhotonPartyService.AvailableInstance == null) return;
        party = PhotonPartyService.AvailableInstance;
        party.PartyChanged += OnPartyChanged;
        party.PartyEnded += OnPartyEnded;
        OnPartyChanged();
    }

    private void Update()
    {
        if (sceneExitPrepared) return;
        AttachParty();
        if (!activeRun || ending) return;
        if (party == null || !party.IsInParty)
        {
            ReturnToVillage(string.IsNullOrEmpty(runError) ? "La party termino. Regresaste a la aldea." : runError);
            return;
        }
        if (!IsPlaying && Time.realtimeSinceStartupAsDouble >= deadline)
        {
            FailRun("Se agoto la espera de construccion del piso. Revisa los componentes y recursos de cada instancia.");
            return;
        }
        DriveWorld();
    }

    public bool BeginRun(int seed, out string error)
    {
        AttachParty();
        error = "Solo el host de una party bloqueada con al menos 2 jugadores puede iniciar.";
        if (sceneExitPrepared || PhotonPartyService.IsSceneExitInProgress || activeRun || ending || !RunSeedSettings.IsValid(seed) || party == null || !party.IsHost ||
            !PhotonNetwork.IsMasterClient || party.IsBusy || !party.IsPartyLocked || party.Members.Count < 2) return false;
        foreach (PartyMember member in party.Members) if (member.Role == PartyRole.None) return false;
        if (!Application.CanStreamedLevelBeLoaded(multiplayerSceneName))
        { error = "Agrega " + multiplayerSceneName + " al perfil de compilacion."; return false; }
        if (!CaptureReturnPose())
        { error = "Asigna Village Return Point en el portal de la aldea."; return false; }
        if (MultiplayerStartingRoomSpawns.LocalPlayer == null)
        { error = "No se encontro el personaje local persistente."; return false; }

        ResetRunState();
        RunId = Guid.NewGuid().ToString("N");
        BaseSeed = seed;
        FloorNumber = 1;
        Phase = MultiplayerRunPhase.LoadingScene;
        activeRun = publicationPending = true;
        BeginTransition("Esperando el inicio de la partida...");
        if (!Publish(Phase, string.Empty, string.Empty))
        {
            activeRun = publicationPending = false;
            ReleasePlayer();
            ShowLoading(false, string.Empty);
            error = "No se pudo enviar el inicio a Photon. Intentalo de nuevo.";
            return false;
        }
        error = string.Empty;
        return true;
    }

    public void RegisterHostGenerator(HostWorldGenerator generator)
    {
        if (party == null || !party.IsHost) return;
        hostGenerator = generator;
        buildRequestedKey = localBuiltKey = null;
        DriveWorld();
    }

    public void RegisterClientGenerator(ClientWorldGenerator generator)
    {
        if (party == null || party.IsHost) return;
        clientGenerator = generator;
        buildRequestedKey = localBuiltKey = null;
        DriveWorld();
    }

    private void DriveWorld()
    {
        if (!activeRun || ending || publicationPending || sceneLoad != null && !sceneLoad.isDone ||
            SceneManager.GetActiveScene().name != multiplayerSceneName) return;
        if (Phase == MultiplayerRunPhase.LoadingScene && party.IsHost && hostGenerator != null && !publishingFirstFloor)
        {
            publishingFirstFloor = true;
            if (!hostGenerator.TryCreateFloor(BaseSeed, FloorNumber, RunId, out MultiplayerFloorData data, out string error))
            { FailRun(error); return; }
            floorJson = JsonUtility.ToJson(data);
            publicationPending = true;
            if (!Publish(MultiplayerRunPhase.BuildingFloor, floorJson, string.Empty)) FailRun("No se pudieron publicar las instrucciones.");
            return;
        }
        if (Phase != MultiplayerRunPhase.BuildingFloor && Phase != MultiplayerRunPhase.Playing) return;
        if (floorData == null || buildRequestedKey == CurrentKey) return;
        if (party.IsHost && hostGenerator == null || !party.IsHost && clientGenerator == null) return;
        buildRequestedKey = CurrentKey;
        if (party.IsHost) hostGenerator.BuildFloor(floorData);
        else clientGenerator.BuildFloor(floorData);
    }

    public bool RequestNextFloor()
    {
        if (!IsPlaying || settlementRequested || party == null || !party.IsHost || !PhotonNetwork.IsMasterClient || hostGenerator == null) return false;
        if (FloorNumber >= 5)
        {
            if (!Application.CanStreamedLevelBeLoaded(villageSceneName))
            {
                Debug.LogError("Falta VillageMenuScene en el perfil de compilacion.", this);
                return false;
            }
            settlementRequested = true;
            playerSync.World.Loot.Settle(success => { if (success && IsRunning) PublishCompletion(); });
            return true;
        }
        if (!hostGenerator.TryCreateFloor(BaseSeed, FloorNumber + 1, RunId, out MultiplayerFloorData data, out string error))
        {
            Debug.LogError("No se pudo preparar el siguiente piso: " + error, this);
            return false;
        }
        settlementRequested = true;
        playerSync.World.Loot.Settle(success => { if (success && IsRunning) PublishNextFloor(data); });
        return true;
    }

    private void PublishCompletion()
    {
        publicationPending = true;
        BeginTransition("Partida completada. Regresando a la aldea...");
        if (!Publish(MultiplayerRunPhase.Completed, string.Empty, string.Empty))
            FailRun("No se pudo publicar el final de la partida.");
    }

    private void PublishNextFloor(MultiplayerFloorData data)
    {
        publicationPending = true;
        BeginTransition("Preparando el piso " + data.floorNumber + "...");
        if (!Publish(MultiplayerRunPhase.BuildingFloor, JsonUtility.ToJson(data), string.Empty, data.floorNumber))
            FailRun("No se pudo publicar el siguiente piso tras confirmar el inventario.");
    }

    private bool Publish(MultiplayerRunPhase phase, string json, string error, int? floor = null)
    {
        if (party == null || !party.IsHost || !PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null) return false;
        return PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable
        {
            { RunKey, RunId }, { SeedKey, BaseSeed }, { FloorKey, floor ?? FloorNumber },
            { PhaseKey, (int)phase }, { JsonKey, json }, { ErrorKey, error }
        });
    }

    public override void OnRoomPropertiesUpdate(Hashtable changed)
    {
        if (!changed.ContainsKey(RunKey) && !changed.ContainsKey(PhaseKey) && !changed.ContainsKey(JsonKey)) return;
        ReadNetworkState();
    }

    private void ReadNetworkState()
    {
        if (sceneExitPrepared || PhotonPartyService.IsSceneExitInProgress || ending || party == null || !party.IsInParty || PhotonNetwork.CurrentRoom == null) return;
        Hashtable properties = PhotonNetwork.CurrentRoom.CustomProperties;
        string id = properties[RunKey] as string;
        if (string.IsNullOrEmpty(id)) return;
        if (!Guid.TryParse(id, out _) || !(properties[SeedKey] is int seed) || !RunSeedSettings.IsValid(seed) ||
            !(properties[FloorKey] is int number) || number < 1 || !(properties[PhaseKey] is int state) ||
            state < 1 || state > (int)MultiplayerRunPhase.Completed)
        { ReportBuildFailed("El estado de la partida recibido no es valido."); return; }
        // Una partida ya terminada nunca debe iniciar otra carga desde la aldea.
        if (!activeRun && state >= (int)MultiplayerRunPhase.Ending) return;
        if (activeRun && (id != RunId || number < FloorNumber)) return;
        if (!activeRun && SceneManager.GetActiveScene().name != villageSceneName) return;
        if (activeRun && number == FloorNumber && state < (int)Phase) return;
        bool firstState = !activeRun;
        bool returnPoseValid = !firstState || CaptureReturnPose();
        bool newFloor = !activeRun || number != FloorNumber;
        MultiplayerRunPhase previousPhase = Phase;
        if (!activeRun) ResetRunState();
        activeRun = true;
        RunId = id;
        BaseSeed = seed;
        FloorNumber = number;
        Phase = (MultiplayerRunPhase)state;
        publicationPending = false;
        runError = properties[ErrorKey] as string ?? string.Empty;
        if (newFloor || Phase == MultiplayerRunPhase.LoadingScene && previousPhase != Phase)
        {
            if (newFloor) { buildRequestedKey = localBuiltKey = reportedFailureKey = null; publishingPlaying = false; }
            BeginTransition("Cargando el piso " + FloorNumber + "...");
        }
        if (Phase == MultiplayerRunPhase.Completed)
        {
            completedRun = true;
            ReturnToVillage(string.Empty);
            return;
        }
        if (Phase == MultiplayerRunPhase.Ending)
        {
            BeginTransition("La partida termino. Regresando a la aldea...");
            return; // PhotonPartyService procesa el evento de disolucion del host.
        }
        if (!returnPoseValid)
        { ReportBuildFailed("Asigna el punto de retorno del portal en esta instancia."); return; }
        floorJson = properties[JsonKey] as string ?? string.Empty;
        if (Phase == MultiplayerRunPhase.BuildingFloor || Phase == MultiplayerRunPhase.Playing)
        {
            if (!MultiplayerMapMath.TryReadJson(floorJson, out MultiplayerFloorData data, out string error) ||
                data.runId != RunId || data.floorNumber != FloorNumber || data.baseSeed != BaseSeed)
            { ReportBuildFailed(string.IsNullOrEmpty(error) ? "Las instrucciones no corresponden al piso actual." : error); return; }
            floorData = data;
        }
        if (SceneManager.GetActiveScene().name != multiplayerSceneName && sceneLoad == null)
        {
            if (!Application.CanStreamedLevelBeLoaded(multiplayerSceneName))
            { ReportBuildFailed("Falta MultiplayerGameScene en el perfil de compilacion de esta instancia."); return; }
            try { sceneLoad = SceneManager.LoadSceneAsync(multiplayerSceneName); }
            catch (Exception exception) { ReportBuildFailed("No se pudo cargar la escena: " + exception.Message); return; }
        }
        if (Phase == MultiplayerRunPhase.Playing && localBuiltKey == CurrentKey) FinishTransition();
        DriveWorld();
    }

    public void ReportWorldReady(string runId, int floor)
    {
        if (!IsRunning || floorData == null || runId != RunId || floor != FloorNumber ||
            Phase != MultiplayerRunPhase.BuildingFloor && Phase != MultiplayerRunPhase.Playing) return;
        if (readyRequestedKey == CurrentKey) return;
        readyRequestedKey = CurrentKey;
        StartCoroutine(ConfirmWorldReady(runId, floor));
    }

    private IEnumerator ConfirmWorldReady(string runId, int floor)
    {
        if (!EnsurePlayerSync(out string syncError) ||
            !playerSync.PrepareGameplay(MultiplayerStartingRoomSpawns.LocalPlayer, out syncError))
        { ReportBuildFailed(syncError); yield break; }
        while (IsRunning && runId == RunId && floor == FloorNumber && !playerSync.GameplayReady) yield return null;
        if (!IsRunning || runId != RunId || floor != FloorNumber) yield break;
        if (!playerSync.PrepareLocalFloor(MultiplayerStartingRoomSpawns.LocalPlayer, out syncError))
        { ReportBuildFailed(syncError); yield break; }
        localBuiltKey = CurrentKey;
        if (!PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { ReadyKey, CurrentKey }, { FailureKey, string.Empty } }))
        { ReportBuildFailed("No se pudo confirmar la construccion del piso."); yield break; }
        Debug.Log("Mapa listo: " + CurrentKey + " | seed " + BaseSeed + " | final " + floorData.finalSeed, this);
        if (Phase == MultiplayerRunPhase.Playing) FinishTransition();
        CheckParticipants();
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changed)
    {
        if (changed.ContainsKey(ReadyKey) || changed.ContainsKey(FailureKey)) CheckParticipants();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer) { CheckParticipants(); }

    private void CheckParticipants()
    {
        if (!IsRunning || ending || party == null || !party.IsHost || !party.IsInParty || PhotonNetwork.CurrentRoom == null) return;
        bool allReady = PhotonNetwork.CurrentRoom.PlayerCount >= 2;
        foreach (Player player in PhotonNetwork.CurrentRoom.Players.Values)
        {
            if (player.IsInactive) continue;
            string failure = player.CustomProperties[FailureKey] as string;
            if (!string.IsNullOrEmpty(failure) && failure.StartsWith(CurrentKey + "|", StringComparison.Ordinal))
            { FailRun("Una instancia no pudo construir el mapa: " + failure.Substring(CurrentKey.Length + 1)); return; }
            if ((player.CustomProperties[ReadyKey] as string) != CurrentKey) allReady = false;
        }
        if (allReady && Phase == MultiplayerRunPhase.BuildingFloor && !publishingPlaying)
        {
            publishingPlaying = true;
            if (!Publish(MultiplayerRunPhase.Playing, floorJson, string.Empty)) FailRun("No se pudo habilitar el piso.");
        }
    }

    public void ReportBuildFailed(string message)
    {
        if (sceneExitPrepared) return;
        Debug.LogError(message, this);
        if (party != null && party.IsHost) { FailRun(message); return; }
        if (reportedFailureKey == CurrentKey) return;
        reportedFailureKey = CurrentKey;
        runError = message;
        if (!PhotonNetwork.InRoom || !PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable
            { { FailureKey, CurrentKey + "|" + message } }))
        {
            PhotonNetwork.Disconnect();
            ReturnToVillage(message);
        }
    }

    private void FailRun(string message)
    {
        if (sceneExitPrepared) return;
        if (ending && !completedRun) return;
        completedRun = false;
        runError = message;
        if (party != null && party.IsHost)
        {
            Publish(MultiplayerRunPhase.Ending, floorJson, message);
            party.LeaveParty();
            ReturnToVillage(message);
        }
        else
        {
            PhotonNetwork.Disconnect();
            ReturnToVillage(message);
        }
    }

    private void OnPartyChanged()
    {
        if (sceneExitPrepared || PhotonPartyService.IsSceneExitInProgress) return;
        // Cubre una entrada que ya estaba en transito al bloquear la sala y el JSON
        // almacenado antes de que esta instancia recibiera su rol.
        if (!activeRun && !ending && party != null && party.IsInParty && party.IsPartyLocked &&
            party.LocalRole != PartyRole.None) ReadNetworkState();
        if (activeRun && !ending && (party == null || !party.IsInParty))
            ReturnToVillage(string.IsNullOrEmpty(runError) ? "La party termino. Regresaste a la aldea." : runError);
    }

    private void OnPartyEnded(PartyEndReason reason)
    {
        if (sceneExitPrepared) return;
        if (!activeRun && !ending) return;
        completedRun = false;
        if (string.IsNullOrEmpty(runError)) runError = PartyRules.EndMessage(reason);
        pendingVillageNotice = runError;
        ReturnToVillage(runError);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (sceneExitPrepared || scene != SceneManager.GetActiveScene()) return;
        if (!PersistentScenePolicy.IsAllowed(scene)) { OnActiveSceneChanged(default, scene); return; }
        if (scene.name == villageSceneName) invalidRedirectPending = false;
        if (scene.name != villageSceneName && scene.name != multiplayerSceneName && (activeRun || ending))
        {
            // Respetar el boton existente para regresar al menu principal.
            if (returnRoutine != null) StopCoroutine(returnRoutine);
            returnRoutine = null;
            NewestSingleton.CancelVillagePlayerReset();
            ReleasePlayer();
            ShowLoading(false, string.Empty);
            pendingVillageNotice = null;
            ResetRunState();
            return;
        }
        if (scene.name != multiplayerSceneName) return;
        if (ending) return;
        if (!activeRun || party == null || !party.IsInParty)
        { ReturnToVillage("Necesitas iniciar una party desde la aldea para entrar a MultiplayerGameScene."); return; }
        if (!EnsurePlayerSync(out string syncError)) { ReportBuildFailed(syncError); return; }
        hostGenerator = null;
        clientGenerator = null;
        buildRequestedKey = localBuiltKey = null;
        FreezePlayer();
    }

    public static void ReturnInvalidSceneToVillage()
    {
        if (PhotonPartyService.IsSceneExitInProgress || Instance != null && Instance.sceneExitPrepared) return;
        if (Instance != null)
        {
            if (!Instance.ending) Instance.ReturnToVillage("Inicia la partida desde el portal de la aldea.");
            return;
        }
        if (invalidRedirectPending) return;
        invalidRedirectPending = true;
        pendingVillageNotice = "Inicia la partida desde el portal de la aldea con una party valida.";
        SceneManager.LoadScene("VillageMenuScene");
    }

    private bool CaptureReturnPose()
    {
        VillageDungeonPortal portal = FindFirstObjectByType<VillageDungeonPortal>();
        hasReturnPose = portal != null && portal.TryGetReturnPose(out returnPosition, out returnRotation);
        return hasReturnPose;
    }

    private void BeginTransition(string message)
    {
        deadline = Time.realtimeSinceStartupAsDouble + transitionTimeout;
        if (playerSync != null) playerSync.BeginFloor(RunId, FloorNumber);
        PlayerController localPlayer = MultiplayerStartingRoomSpawns.LocalPlayer;
        if (localPlayer != null)
        {
            localPlayer.EndAttack();
            if (localPlayer.playerAnimation != null) localPlayer.playerAnimation.ResetTrigger("isAttacking");
        }
        FreezePlayer();
        ShowLoading(true, message);
    }

    private void FinishTransition()
    {
        settlementRequested = false;
        ReleasePlayer();
        if (playerSync != null) playerSync.ResumePlaying();
        ShowLoading(false, string.Empty);
    }

    private bool EnsurePlayerSync(out string error)
    {
        error = string.Empty;
        if (sceneExitPrepared || PhotonPartyService.IsSceneExitInProgress)
        { error = "La sesion multijugador se esta cerrando."; return false; }
        if (playerSync != null) return true;
        GameObject resource = Resources.Load<GameObject>(MultiplayerPlayers.ResourcePath);
        MultiplayerPlayers prefab = resource != null ? resource.GetComponent<MultiplayerPlayers>() : null;
        if (prefab == null)
        {
            error = "Falta el prefab Resources/" + MultiplayerPlayers.ResourcePath + ". Ejecuta Tools > Multiplayer > Crear prefabs de jugadores.";
            return false;
        }
        playerSync = Instantiate(prefab, transform);
        if (playerSync.Initialize(this, out error)) return true;
        StopPlayerSync();
        return false;
    }

    private void StopPlayerSync()
    {
        if (playerSync == null) return;
        playerSync.Shutdown();
        playerSync.gameObject.SetActive(false);
        Destroy(playerSync.gameObject);
        playerSync = null;
    }

    private void FreezePlayer()
    {
        if (frozen) return;
        frozenPlayer = MultiplayerStartingRoomSpawns.LocalPlayer;
        if (frozenPlayer == null) return;
        frozen = true;
        frozenPause = NewestSingleton.Instance.GetComponentInChildren<PauseController>(true);
        if (frozenPause != null)
        {
            pauseWasEnabled = frozenPause.enabled;
            frozenPause.ResumeGame();
            frozenPause.enabled = false;
        }
        PlayerInventory inventory = frozenPlayer.Inventory;
        if (inventory != null && frozenPlayer.IsInventoryOpen)
        {
            if (inventory.Loot.Count > 0) inventory.RemoveWeaponFromChestLootGrid(frozenPlayer.transform.position);
            inventory.gameObject.SetActive(false);
        }
        playerWasEnabled = frozenPlayer.enabled;
        frozenPlayer.enabled = false;
        frozenBody = frozenPlayer.rb;
        if (frozenBody != null)
        {
            bodyWasKinematic = frozenBody.isKinematic;
            if (!bodyWasKinematic) { frozenBody.linearVelocity = Vector3.zero; frozenBody.angularVelocity = Vector3.zero; }
            frozenBody.isKinematic = true;
        }
    }

    private void ReleasePlayer()
    {
        if (!frozen) return;
        if (frozenBody != null) frozenBody.isKinematic = bodyWasKinematic;
        if (frozenPlayer != null) frozenPlayer.enabled = playerWasEnabled;
        if (frozenPause != null) frozenPause.enabled = pauseWasEnabled;
        frozenPlayer = null; frozenBody = null; frozenPause = null; frozen = false;
    }

    private void ReturnToVillage(string message)
    {
        if (sceneExitPrepared || PhotonPartyService.IsSceneExitInProgress) return;
        if (!string.IsNullOrEmpty(message)) pendingVillageNotice = message;
        if (returnRoutine != null) return;
        ending = true;
        StopPlayerSync();
        FreezePlayer();
        ShowLoading(true, "Regresando a la aldea...");
        returnRoutine = StartCoroutine(ReturnRoutine());
    }

    private IEnumerator ReturnRoutine()
    {
        yield return null; // Registrar returnRoutine antes de procesar posibles errores.
        if (sceneLoad != null && !sceneLoad.isDone) yield return sceneLoad;
        double until = Time.realtimeSinceStartupAsDouble + transitionTimeout;
        while (party != null && party.IsBusy && Time.realtimeSinceStartupAsDouble < until) yield return null;
        bool resetPlayer = completedRun;
        NewestSingleton previousPlayer = NewestSingleton.Instance;
        if (SceneManager.GetActiveScene().name != villageSceneName)
        {
            if (!Application.CanStreamedLevelBeLoaded(villageSceneName))
            {
                FailReturn("Falta VillageMenuScene en el perfil de compilacion.");
                yield break;
            }
            if (resetPlayer) NewestSingleton.RequestVillagePlayerReset();
            AsyncOperation villageLoad = null;
            string loadError = null;
            try { villageLoad = SceneManager.LoadSceneAsync(villageSceneName); }
            catch (Exception exception) { loadError = exception.Message; }
            if (villageLoad == null)
            {
                FailReturn("No se pudo regresar a la aldea: " + loadError);
                yield break;
            }
            yield return villageLoad;
        }
        yield return null; // Dejar que termine el descarte del personaje duplicado de la aldea.
        if (resetPlayer) NewestSingleton.CancelVillagePlayerReset();
        PlayerController player = MultiplayerStartingRoomSpawns.LocalPlayer;
        if (resetPlayer && (player == null || ReferenceEquals(previousPlayer, NewestSingleton.Instance)))
        {
            FailReturn("No se encontro el personaje nuevo al regresar a la aldea.");
            yield break;
        }
        if (hasReturnPose && player != null)
        {
            NewestSingleton.Instance.transform.position += returnPosition - player.transform.position;
            player.transform.SetPositionAndRotation(returnPosition, returnRotation);
            if (player.rb != null) { player.rb.position = returnPosition; player.rb.rotation = returnRotation; }
            Physics.SyncTransforms();
        }
        ReleasePlayer();
        if (completedRun)
        {
            // Las referencias congeladas pertenecian al personaje que acaba de destruirse.
            FreezePlayer();
            ShowLoading(true, "Esperando a la party en la aldea...");
            yield return ConfirmVillageReturn();
            ReleasePlayer();
        }
        if (resetPlayer && PhotonNetwork.InRoom)
            PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { ReturnedKey, null } });
        ShowLoading(false, string.Empty);
        if (TryConsumeVillageNotice(out string notice))
        {
            VillageMultiplayerMenu menu = FindFirstObjectByType<VillageMultiplayerMenu>();
            if (menu != null) menu.ShowExternalNotice(notice);
        }
        ResetRunState();
        returnRoutine = null;
        // El host pudo iniciar otra partida mientras esta instancia terminaba el regreso.
        if (resetPlayer) OnPartyChanged();
    }

    private IEnumerator ConfirmVillageReturn()
    {
        if (!PhotonNetwork.InRoom || !PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable
            { { ReturnedKey, RunId }, { ReadyKey, null }, { FailureKey, null } }))
        {
            FailRun("No se pudo confirmar el regreso a la aldea.");
            yield break;
        }
        double until = Time.realtimeSinceStartupAsDouble + transitionTimeout;
        while (completedRun)
        {
            if (Time.realtimeSinceStartupAsDouble >= until)
            {
                FailRun("Se agoto la espera de regreso de la party a la aldea.");
                yield break;
            }
            if (party == null || !party.IsInParty || PhotonNetwork.CurrentRoom == null)
            {
                // OnPartyEnded conserva el motivo original cuando termina la salida de Photon.
                if (party != null && party.IsBusy) { yield return null; continue; }
                FailRun("La party termino durante el regreso a la aldea.");
                yield break;
            }

            string networkRun = PhotonNetwork.CurrentRoom.CustomProperties[RunKey] as string;
            if (!string.IsNullOrEmpty(networkRun) && networkRun != RunId) yield break;
            bool cleared = string.IsNullOrEmpty(networkRun);
            // Conservar la party bloqueada; solo el menu del host puede reabrirla.
            if (cleared && !party.IsBusy) yield break;
            if (party.IsHost && PhotonNetwork.IsMasterClient)
            {
                if (!cleared && !completionClearRequested && AllPlayersReturned())
                {
                    completionClearRequested = true;
                    if (!PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable
                        { { RunKey, null }, { SeedKey, null }, { FloorKey, null },
                          { PhaseKey, null }, { JsonKey, null }, { ErrorKey, null } }))
                    {
                        FailRun("No se pudo limpiar el estado de la partida completada.");
                        yield break;
                    }
                }
            }
            yield return null;
        }
    }

    private bool AllPlayersReturned()
    {
        foreach (Player player in PhotonNetwork.CurrentRoom.Players.Values)
            if (!player.IsInactive && (player.CustomProperties[ReturnedKey] as string) != RunId) return false;
        return true;
    }

    private void FailReturn(string message)
    {
        Debug.LogError(message, this);
        NewestSingleton.CancelVillagePlayerReset();
        if (completedRun) FailRun(message);
        ReleasePlayer();
        ShowLoading(false, string.Empty);
        ResetRunState();
        returnRoutine = null;
    }

    private void ResetRunState()
    {
        StopPlayerSync();
        activeRun = ending = publicationPending = publishingFirstFloor = publishingPlaying = false;
        readyRequestedKey = null; settlementRequested = false;
        completedRun = completionClearRequested = false;
        RunId = string.Empty; BaseSeed = FloorNumber = 0; Phase = MultiplayerRunPhase.Idle;
        floorData = null; floorJson = string.Empty; runError = string.Empty;
        buildRequestedKey = localBuiltKey = reportedFailureKey = null;
        hostGenerator = null; clientGenerator = null; sceneLoad = null;
    }

    private void ShowLoading(bool visible, string message)
    {
        if (loadingText != null) loadingText.text = message;
        if (loadingPanel != null && !transform.IsChildOf(loadingPanel.transform)) loadingPanel.SetActive(visible);
    }
}
