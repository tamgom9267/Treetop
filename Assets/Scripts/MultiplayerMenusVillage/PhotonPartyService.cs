using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using Hashtable = ExitGames.Client.Photon.Hashtable;

/// <summary>
/// Agregar a un objeto raiz independiente de los paneles. No crea personajes ni carga escenas.
/// Las propiedades de la sala son el estado compartido; los eventos solo comunican salidas.
/// </summary>
[DefaultExecutionOrder(-200)]
public class PhotonPartyService : MonoBehaviourPunCallbacks, IOnEventCallback
{
    public static PhotonPartyService Instance { get; private set; }
    public static PhotonPartyService AvailableInstance => Instance != null && !Instance.IsShuttingDown ? Instance : null;
    public static bool IsSceneExitInProgress => Instance != null && Instance.IsShuttingDown;
    public bool IsShuttingDown { get; private set; }
    internal bool IsWaitingForPreviousSession { get; private set; }
    private bool initialized;
    private PhotonPartyService pendingReplacement;
    private float previousDispatchThreshold;
    private bool dispatchOverrideApplied;

    [Header("Conexion (iguales en todas las instancias)")]
    [SerializeField] private string fixedRegion = "us";
    [SerializeField] private string gameVersion = "treetop-party-v1";

    private const string HostKey = "party.host";
    private const string Client1Key = "party.c1";
    private const string Client2Key = "party.c2";
    private const string LockedKey = "party.locked";
    private const string ClosingKey = "party.closing";
    private const string NameKey = "party.name";
    private const string SchemaKey = "party.schema";
    private const byte KickEvent = 1;
    private const byte ClosePartyEvent = 2;
    private const int MaxCreateAttempts = 10;
    private const double OperationTimeout = 30d;

    private readonly List<PartyMember> members = new List<PartyMember>();
    private readonly System.Random codeGenerator = new System.Random();
    private ReadOnlyCollection<PartyMember> readOnlyMembers;
    private int hostClient1;
    private int hostClient2;
    private int createAttempts;
    private bool createdByUs;
    private bool hadClients;
    private bool roomJoined;
    private bool leaving;
    private bool cancelPendingEntry;
    private bool applyLeaveCooldown;
    private bool? requestedLock;
    private PartyEndReason leaveReason;
    private double cooldownUntil;
    private double operationDeadline;
    private string pendingName;

    public PartyRole LocalRole { get; private set; }
    public int OriginalHostActorNumber { get; private set; }
    public bool IsInParty => roomJoined && !leaving && PhotonNetwork.InRoom;
    public bool IsPartyLocked { get; private set; }
    public bool IsHost => IsInParty && LocalRole == PartyRole.Host;
    public bool IsConnected => PhotonNetwork.IsConnectedAndReady;
    public PartyOperation Operation { get; private set; }
    public bool IsBusy => IsShuttingDown || Operation != PartyOperation.None;
    public string RoomCode { get; private set; } = string.Empty;
    public string RoomDisplayName { get; private set; } = string.Empty;
    public IReadOnlyList<PartyMember> Members => readOnlyMembers ?? (readOnlyMembers = members.AsReadOnly());
    public float RemainingJoinCooldown => (float)Math.Max(0d, cooldownUntil - Time.realtimeSinceStartupAsDouble);

    public event Action PartyChanged;
    public event Action<string> OperationFailed;
    public event Action<PartyEndReason> PartyEnded;

    private void Awake()
    {
        InitializeSingleton();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Instance = null; }

    internal bool InitializeSingleton()
    {
        if (initialized) return Instance == this && !IsShuttingDown;
        if (!PersistentScenePolicy.IsAllowed(gameObject.scene))
        {
            gameObject.SetActive(false);
            Destroy(gameObject);
            return false;
        }
        if (Instance != null && Instance != this)
        {
            if (Instance.IsShuttingDown)
            {
                IsWaitingForPreviousSession = true;
                if (Instance.pendingReplacement != null && Instance.pendingReplacement != this)
                    Destroy(Instance.pendingReplacement.gameObject);
                Instance.pendingReplacement = this;
                gameObject.SetActive(false);
                return false;
            }
            gameObject.SetActive(false);
            Destroy(gameObject);
            return false;
        }

        initialized = true;
        IsWaitingForPreviousSession = false;
        Instance = this;
        DontDestroyOnLoad(gameObject);
        PhotonNetwork.AutomaticallySyncScene = false;
        return true;
    }

    public override void OnEnable()
    {
        if (Instance == this)
        {
            base.OnEnable();
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }
    }

    public override void OnDisable()
    {
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        base.OnDisable();
    }

    private void Start()
    {
        OnActiveSceneChanged(default, SceneManager.GetActiveScene());
    }

    private void OnActiveSceneChanged(Scene previous, Scene current)
    {
        if (Instance == this && !PersistentScenePolicy.IsAllowed(current)) BeginSceneExitShutdown();
    }

    public void BeginSceneExitShutdown()
    {
        if (Instance != this || IsShuttingDown) return;
        IsShuttingDown = true;
        MultiplayerRunSession run = GetComponent<MultiplayerRunSession>();
        if (run != null) run.PrepareForSceneExit();
        // PUN debe despachar OnLeftRoom/OnDisconnected aunque Time.timeScale sea cero.
        previousDispatchThreshold = PhotonNetwork.MinimalTimeScaleToDispatchInFixedUpdate;
        PhotonNetwork.MinimalTimeScaleToDispatchInFixedUpdate = Mathf.Max(0f, previousDispatchThreshold);
        dispatchOverrideApplied = true;
        LeaveParty();
        StartCoroutine(SceneExitShutdown());
    }

    private IEnumerator SceneExitShutdown()
    {
        // Tiempo real: salir del menu de pausa tambien debe completar la desconexion.
        double until = Time.realtimeSinceStartupAsDouble + 5d;
        while (PhotonNetwork.IsConnected && Time.realtimeSinceStartupAsDouble < until &&
            (PhotonNetwork.InRoom || leaving || Operation == PartyOperation.Joining || Operation == PartyOperation.Creating))
            yield return null;

        PhotonNetwork.Disconnect();
        double warningAt = Time.realtimeSinceStartupAsDouble + 10d;
        bool warned = false;
        while (PhotonNetwork.IsConnected)
        {
            if (!warned && Time.realtimeSinceStartupAsDouble >= warningAt)
            {
                warned = true;
                Debug.LogWarning("Photon sigue cerrando la conexion. El multijugador esperara su confirmacion.", this);
            }
            yield return null;
        }

        PhotonPartyService replacement = pendingReplacement;
        pendingReplacement = null;
        RestoreDispatchThreshold();
        if (Instance == this) Instance = null;
        gameObject.SetActive(false);
        Destroy(gameObject);

        if (replacement == null) yield break;
        if (!replacement.gameObject.scene.isLoaded || !PersistentScenePolicy.IsAllowed(replacement.gameObject.scene) ||
            !PersistentScenePolicy.IsAllowed(SceneManager.GetActiveScene()))
        {
            Destroy(replacement.gameObject);
            yield break;
        }
        if (!replacement.InitializeSingleton()) yield break;
        MultiplayerRunSession replacementRun = replacement.GetComponent<MultiplayerRunSession>();
        if (replacementRun != null) replacementRun.InitializeSingleton();
        replacement.gameObject.SetActive(true);
    }

    private void OnDestroy()
    {
        RestoreDispatchThreshold();
        if (Instance == this)
            Instance = null;
    }

    private void RestoreDispatchThreshold()
    {
        if (!dispatchOverrideApplied) return;
        PhotonNetwork.MinimalTimeScaleToDispatchInFixedUpdate = previousDispatchThreshold;
        dispatchOverrideApplied = false;
    }

    private void Update()
    {
        if (IsShuttingDown || !IsBusy || Time.realtimeSinceStartupAsDouble < operationDeadline)
            return;

        // Desconectar evita que una respuesta tardia cree una party fantasma despues del timeout.
        operationDeadline = double.PositiveInfinity;
        OperationFailed?.Invoke("Photon tardo demasiado en responder. Vuelve a abrir el menu para reintentar.");
        PhotonNetwork.Disconnect();
    }

    public void ConnectIfNeeded()
    {
        if (IsBusy || PhotonNetwork.IsConnected)
            return;

        if (PhotonNetwork.PhotonServerSettings == null ||
            !Guid.TryParse(PhotonNetwork.PhotonServerSettings.AppSettings.AppIdRealtime, out _))
        {
            Fail("Falta un App ID valido en PhotonServerSettings > App Id Realtime.");
            return;
        }

        AppSettings settings = PhotonNetwork.PhotonServerSettings.AppSettings.CopyTo(new AppSettings());
        settings.FixedRegion = string.IsNullOrWhiteSpace(fixedRegion) ? "us" : fixedRegion.Trim().ToLowerInvariant();
        settings.AppVersion = gameVersion;
        SetOperation(PartyOperation.Connecting);
        if (!PhotonNetwork.ConnectUsingSettings(settings))
            Fail("No se pudo iniciar la conexion con Photon. Intentalo de nuevo.");
    }

    public void CreateParty(string playerName)
    {
        if (!CanEnterParty(playerName, out string name))
            return;

        pendingName = name;
        PhotonNetwork.NickName = name;
        createdByUs = true;
        cancelPendingEntry = false;
        createAttempts = 0;
        TryCreateRoom();
    }

    public void JoinParty(string playerName, string roomCode)
    {
        if (!CanEnterParty(playerName, out string name))
            return;
        if (!PartyRules.IsValidRoomCode(roomCode))
        {
            Fail("El codigo debe contener exactamente 5 digitos.");
            return;
        }

        PhotonNetwork.NickName = name;
        createdByUs = false;
        cancelPendingEntry = false;
        SetOperation(PartyOperation.Joining);
        if (!PhotonNetwork.JoinRoom(roomCode))
            Fail("No se pudo enviar la solicitud para unirse. Intentalo de nuevo.");
    }

    private bool CanEnterParty(string playerName, out string name)
    {
        name = string.Empty;
        if (IsBusy || IsInParty)
            return false;
        if (!PartyRules.TryNormalizeName(playerName, out name))
        {
            Fail("Escribe un nombre de 1 a 15 caracteres.");
            return false;
        }
        if (RemainingJoinCooldown > 0f)
        {
            Fail("Espera " + Mathf.CeilToInt(RemainingJoinCooldown) + " segundos para crear o unirte a otra party.");
            return false;
        }
        if (PhotonNetwork.NetworkClientState != ClientState.ConnectedToMasterServer)
        {
            ConnectIfNeeded();
            OperationFailed?.Invoke("Espera a que termine la conexion con Photon antes de continuar.");
            return false;
        }
        return true;
    }

    private void TryCreateRoom()
    {
        createAttempts++;
        string code = codeGenerator.Next(10000, 100000).ToString();
        RoomOptions options = new RoomOptions
        {
            MaxPlayers = PartyRules.MaxPlayers,
            IsOpen = true,
            IsVisible = false,
            PlayerTtl = 0,
            EmptyRoomTtl = 0,
            CleanupCacheOnLeave = true,
            BroadcastPropsChangeToAll = true,
            CustomRoomProperties = new Hashtable
            {
                { SchemaKey, 1 }, { HostKey, 0 }, { Client1Key, 0 }, { Client2Key, 0 },
                { LockedKey, false }, { ClosingKey, false }, { NameKey, "Cuarto de " + pendingName }
            }
        };
        SetOperation(PartyOperation.Creating);
        if (!PhotonNetwork.CreateRoom(code, options))
            Fail("No se pudo enviar la solicitud para crear la sala.");
    }

    public void ReadyParty()
    {
        if (!CanManageParty())
            return;
        if (members.Count < 2)
        {
            Fail("Solo hay 1 jugador. No necesitas una sala multijugador para jugar en solitario.");
            return;
        }
        foreach (PartyMember member in members)
        {
            if (member.Role == PartyRole.None)
            {
                Fail("Espera a que termine de conectarse el resto de los jugadores.");
                return;
            }
        }
        SetRoomLocked(true);
    }

    public void ReopenParty()
    {
        if (CanManageParty() && IsPartyLocked)
            SetRoomLocked(false);
    }

    private bool CanManageParty()
    {
        return IsHost && PhotonNetwork.IsMasterClient && !IsBusy;
    }

    private void SetRoomLocked(bool locked)
    {
        requestedLock = locked;
        SetOperation(PartyOperation.Updating);
        Room room = PhotonNetwork.CurrentRoom;
        bool previousOpen = room.IsOpen;
        // PUN encola ambas operaciones fiables en orden: primero admision, luego estado UI.
        room.IsOpen = !locked;
        if (!room.SetCustomProperties(new Hashtable { { LockedKey, locked } }))
        {
            room.IsOpen = previousOpen;
            requestedLock = null;
            Fail("No se pudo cambiar el bloqueo de la sala.");
        }
    }

    public void LeaveParty()
    {
        if (!roomJoined)
        {
            // Usado tambien al salir al menu principal durante una entrada pendiente.
            if (Operation == PartyOperation.Joining || Operation == PartyOperation.Creating)
                cancelPendingEntry = true;
            return;
        }
        if (!leaving)
            BeginLeave(IsHost ? PartyEndReason.HostCancelled : PartyEndReason.Left);
    }

    public void KickPlayer(int actorNumber)
    {
        if (!CanManageParty() || actorNumber == OriginalHostActorNumber ||
            PhotonNetwork.CurrentRoom.GetPlayer(actorNumber) == null)
            return;

        if (!PhotonNetwork.RaiseEvent(KickEvent, null,
            new RaiseEventOptions { TargetActors = new[] { actorNumber } }, SendOptions.SendReliable))
            Fail("No se pudo enviar la solicitud de expulsion.");
    }

    private void BeginLeave(PartyEndReason reason)
    {
        if (leaving)
            return;

        bool wasHost = OriginalHostActorNumber == PhotonNetwork.LocalPlayer.ActorNumber && createdByUs;
        leaving = true;
        leaveReason = reason;
        applyLeaveCooldown = !wasHost && (reason == PartyEndReason.Left || reason == PartyEndReason.Kicked);
        requestedLock = null;
        SetOperation(PartyOperation.Leaving);

        if (wasHost && PhotonNetwork.InRoom)
        {
            PhotonNetwork.CurrentRoom.IsOpen = false;
            PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { ClosingKey, true } });
            PhotonNetwork.RaiseEvent(ClosePartyEvent, null,
                new RaiseEventOptions { Receivers = ReceiverGroup.Others }, SendOptions.SendReliable);
        }
        if (!PhotonNetwork.InRoom || !PhotonNetwork.LeaveRoom(false))
            PhotonNetwork.Disconnect();
    }

    public override void OnConnectedToMaster()
    {
        if (Operation == PartyOperation.Connecting || Operation == PartyOperation.Leaving)
            SetOperation(PartyOperation.None);
    }

    public override void OnCreatedRoom()
    {
        OriginalHostActorNumber = PhotonNetwork.LocalPlayer.ActorNumber;
        hostClient1 = hostClient2 = 0;
        hadClients = false;
        if (!PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { HostKey, OriginalHostActorNumber } }))
            BeginLeave(PartyEndReason.InvalidRoom);
    }

    public override void OnJoinedRoom()
    {
        roomJoined = true;
        RoomCode = PhotonNetwork.CurrentRoom.Name;
        if (IsShuttingDown)
        {
            BeginLeave(createdByUs ? PartyEndReason.HostCancelled : PartyEndReason.Left);
            return;
        }
        if (ReadInt(SchemaKey) != 1)
        {
            BeginLeave(PartyEndReason.InvalidRoom);
            return;
        }
        RefreshSnapshot();
        if (cancelPendingEntry)
        {
            BeginLeave(createdByUs ? PartyEndReason.HostCancelled : PartyEndReason.Left);
            return;
        }
        if (createdByUs && !leaving)
            ReconcileHostSlots();
        CheckRoomState();
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        if (IsShuttingDown) { SetOperation(PartyOperation.None); return; }
        if (cancelPendingEntry)
        {
            cancelPendingEntry = false;
            Fail("Se cancelo la creacion de la sala.");
        }
        else if (returnCode == ErrorCode.GameIdAlreadyExists && createAttempts < MaxCreateAttempts)
            TryCreateRoom();
        else
            Fail(returnCode == ErrorCode.GameIdAlreadyExists
                ? "No se encontro un codigo libre. Vuelve a intentar crear la sala."
                : "No se pudo crear la sala (Photon " + returnCode + ").");
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        switch (returnCode)
        {
            case ErrorCode.GameDoesNotExist: Fail("No existe una sala con ese codigo."); break;
            case ErrorCode.GameFull: Fail("La sala ya tiene 3 jugadores."); break;
            case ErrorCode.GameClosed: Fail("El host tiene bloqueada la sala."); break;
            default: Fail("No se pudo entrar a la sala (Photon " + returnCode + ")."); break;
        }
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        if (leaving)
            return;
        if (createdByUs)
            ReconcileHostSlots();
        RefreshSnapshot();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        if (leaving)
            return;
        if (otherPlayer.ActorNumber == OriginalHostActorNumber)
        {
            BeginLeave(PartyEndReason.HostDisconnected);
            return;
        }
        if (createdByUs)
        {
            // En preparacion el host puede seguir esperando con el mismo codigo.
            // Usar tambien la solicitud en vuelo evita disolver al reabrir la sala
            // antes de que llegue la confirmacion de Photon.
            bool locked = requestedLock ?? IsPartyLocked;
            if (hadClients && locked && PhotonNetwork.CurrentRoom.PlayerCount == 1)
            {
                BeginLeave(PartyEndReason.HostAlone);
                return;
            }
            ReconcileHostSlots();
        }
        RefreshSnapshot();
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        // Photon elige otro Master, pero nunca convertimos a ese cliente en nuestro Host.
        if (roomJoined && !leaving && newMasterClient.ActorNumber != OriginalHostActorNumber)
            BeginLeave(PartyEndReason.HostDisconnected);
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        if (roomJoined && !leaving)
            RefreshSnapshot();
    }

    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        if (roomJoined && !leaving)
        {
            RefreshSnapshot();
            CheckRoomState();
        }
    }

    private void CheckRoomState()
    {
        if (leaving)
            return;
        if (ReadBool(ClosingKey))
        {
            BeginLeave(PartyEndReason.HostCancelled);
            return;
        }
        if (OriginalHostActorNumber > 0 && PhotonNetwork.CurrentRoom.GetPlayer(OriginalHostActorNumber) == null)
        {
            BeginLeave(PartyEndReason.HostDisconnected);
            return;
        }
        if (LocalRole != PartyRole.None &&
            (Operation == PartyOperation.Joining || Operation == PartyOperation.Creating))
            SetOperation(PartyOperation.None);
        if (requestedLock.HasValue && IsPartyLocked == requestedLock.Value &&
            PhotonNetwork.CurrentRoom.IsOpen == !requestedLock.Value)
        {
            requestedLock = null;
            SetOperation(PartyOperation.None);
        }
    }

    private void ReconcileHostSlots()
    {
        if (!createdByUs || leaving || !PhotonNetwork.IsMasterClient)
            return;
        Room room = PhotonNetwork.CurrentRoom;
        // Las reservas locales incluyen escrituras en vuelo: dos entradas seguidas no comparten lugar.
        if (room.GetPlayer(hostClient1) == null) hostClient1 = 0;
        if (room.GetPlayer(hostClient2) == null) hostClient2 = 0;
        List<int> clients = new List<int>();
        foreach (Player player in room.Players.Values)
            if (player.ActorNumber != OriginalHostActorNumber && !player.IsInactive)
                clients.Add(player.ActorNumber);
        clients.Sort();
        foreach (int actor in clients)
        {
            if (actor == hostClient1 || actor == hostClient2) continue;
            if (hostClient1 == 0) hostClient1 = actor;
            else if (hostClient2 == 0) hostClient2 = actor;
        }
        hadClients |= clients.Count > 0;
        if (!room.SetCustomProperties(new Hashtable { { Client1Key, hostClient1 }, { Client2Key, hostClient2 } }))
            OperationFailed?.Invoke("No se pudo actualizar la lista de integrantes.");
    }

    private void RefreshSnapshot()
    {
        Room room = PhotonNetwork.CurrentRoom;
        if (room == null)
            return;
        int host = ReadInt(HostKey);
        if (host > 0)
            OriginalHostActorNumber = host;
        IsPartyLocked = ReadBool(LockedKey);
        RoomDisplayName = room.CustomProperties[NameKey] as string ?? "Cuarto";
        int client1 = ReadInt(Client1Key);
        int client2 = ReadInt(Client2Key);
        members.Clear();
        LocalRole = PartyRole.None;
        foreach (Player player in room.Players.Values)
        {
            if (player.IsInactive) continue;
            PartyRole role = player.ActorNumber == OriginalHostActorNumber ? PartyRole.Host :
                player.ActorNumber == client1 ? PartyRole.Client1 :
                player.ActorNumber == client2 ? PartyRole.Client2 : PartyRole.None;
            members.Add(new PartyMember(player.ActorNumber, player.NickName, role));
            if (player.IsLocal) LocalRole = role;
        }
        members.Sort((a, b) => a.Role.CompareTo(b.Role));
        PartyChanged?.Invoke();
    }

    public void OnEvent(EventData photonEvent)
    {
        if (!IsInParty || OriginalHostActorNumber <= 0 ||
            photonEvent.Sender != OriginalHostActorNumber || PhotonNetwork.MasterClient == null ||
            PhotonNetwork.MasterClient.ActorNumber != OriginalHostActorNumber)
            return;
        if (photonEvent.Code == KickEvent)
            BeginLeave(PartyEndReason.Kicked);
        else if (photonEvent.Code == ClosePartyEvent)
            BeginLeave(PartyEndReason.HostCancelled);
    }

    public override void OnLeftRoom()
    {
        FinishLeaving(leaving ? leaveReason : PartyEndReason.Left, true);
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        bool hadParty = roomJoined || leaving;
        PartyEndReason reason = leaving ? leaveReason : PartyEndReason.Disconnected;
        if (hadParty)
            FinishLeaving(reason, false);
        else if (!IsShuttingDown)
            Fail("No se pudo conectar con Photon (" + cause + "). Revisa tu conexion y App ID.");
    }

    private void FinishLeaving(PartyEndReason reason, bool returningToMaster)
    {
        bool notify = roomJoined || leaving;
        if (applyLeaveCooldown)
            cooldownUntil = Time.realtimeSinceStartupAsDouble + PartyRules.JoinCooldownSeconds;
        roomJoined = leaving = createdByUs = hadClients = cancelPendingEntry = applyLeaveCooldown = false;
        requestedLock = null;
        LocalRole = PartyRole.None;
        OriginalHostActorNumber = hostClient1 = hostClient2 = 0;
        IsPartyLocked = false;
        RoomCode = RoomDisplayName = string.Empty;
        members.Clear();
        SetOperation(returningToMaster ? PartyOperation.Leaving : PartyOperation.None);
        if (notify && !IsShuttingDown) PartyEnded?.Invoke(reason);
    }

    private int ReadInt(string key)
    {
        object value = PhotonNetwork.CurrentRoom?.CustomProperties[key];
        return value is int number ? number : 0;
    }

    private bool ReadBool(string key)
    {
        return PhotonNetwork.CurrentRoom?.CustomProperties[key] is bool value && value;
    }

    private void SetOperation(PartyOperation operation)
    {
        Operation = operation;
        operationDeadline = Time.realtimeSinceStartupAsDouble + OperationTimeout;
        PartyChanged?.Invoke();
    }

    private void Fail(string message)
    {
        requestedLock = null;
        SetOperation(PartyOperation.None);
        if (!IsShuttingDown) OperationFailed?.Invoke(message);
    }
}
