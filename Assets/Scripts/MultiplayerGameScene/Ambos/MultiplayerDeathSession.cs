using System;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Hashtable = ExitGames.Client.Photon.Hashtable;

// Vive con la sesion persistente, no con el Player que se desactiva al morir.
public sealed class MultiplayerDeathSession : MonoBehaviourPunCallbacks, IOnEventCallback
{
    public static MultiplayerDeathSession Instance { get; private set; }
    public const string WipeKey = "world.wipe";
    private const string LifeKey = "world.life";
    private const string DeathPrefabPath = "Canvas Muerte/Canvas Muerte";
    private const string SpectatorPrefabPath = "Canvas Espectador/Canvas Espectador";

    private MultiplayerRunSession session;
    private PlayerController localPlayer;
    private PlayerClass localClass;
    private FollowPlayerNoRotation cameraFollow;
    private Transform originalTarget;
    private DeathPanelView deathPanel;
    private SpectatorPanelView spectatorPanel;
    private readonly Dictionary<GameObject, bool> canvasStates = new Dictionary<GameObject, bool>();
    private bool localDead;
    private bool spectating;
    private bool hasWipe;
    private int lastDeadActor;
    private int lastDeathCandidate;
    private int selectedActor;
    private int currentFloor;
    private int deathFloor;

    public bool HasWipe => hasWipe;
    public int LastDeadActor => lastDeadActor;

    private void Awake()
    {
        Instance = this;
        session = GetComponent<MultiplayerRunSession>();
    }

    public override void OnEnable()
    {
        base.OnEnable();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    public override void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        UnbindPlayer();
        base.OnDisable();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        ClearPanels();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "VillageMenuScene") ResetLocalState();
    }

    private void Update()
    {
        if (session == null || !session.IsInMultiplayerScene || !session.IsRunning) return;
        BindPlayer();
        if (spectating) UpdateSpectator();
        if (MultiplayerGameplayProtocol.Host && !hasWipe && lastDeathCandidate > 0) CheckWipe(lastDeathCandidate);
    }

    private void BindPlayer()
    {
        PlayerController player = MultiplayerStartingRoomSpawns.LocalPlayer;
        if (player == localPlayer) return;
        UnbindPlayer();
        localPlayer = player;
        if (player == null) return;
        localClass = player.GetComponent<PlayerClass>();
        if (localClass != null) localClass.OnDied += OnLocalDied;
        cameraFollow = NewestSingleton.Instance.GetComponentInChildren<FollowPlayerNoRotation>(true);
        if (cameraFollow != null) originalTarget = cameraFollow.Target;
    }

    private void UnbindPlayer()
    {
        if (localClass != null) localClass.OnDied -= OnLocalDied;
        localClass = null;
        localPlayer = null;
    }

    public bool IsAlive(int actor)
    {
        if (actor <= 0 || !MultiplayerGameplayProtocol.Participant(actor)) return false;
        if (actor == MultiplayerGameplayProtocol.LocalActor && localDead) return false;
        if (session == null || !session.IsRunning) return true;
        Player player = PhotonNetwork.CurrentRoom.GetPlayer(actor);
        string value = player != null ? player.CustomProperties[LifeKey] as string : null;
        return value != session.CurrentKey + "|0";
    }

    private void OnLocalDied()
    {
        if (localDead || session == null || !session.IsPlaying || !session.IsInMultiplayerScene) return;
        BindPlayer();
        localDead = true;
        deathFloor = session.FloorNumber;
        if (!PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { LifeKey, session.CurrentKey + "|0" } }))
        {
            session.ReportBuildFailed("No se pudo sincronizar la muerte del jugador.");
            return;
        }
        MultiplayerWorldSession world = MultiplayerWorldSession.Instance;
        if (world != null)
        {
            if (world.Inventory != null) world.Inventory.CloseForDeath();
            if (world.Loot != null) world.Loot.DiscardLocalItems();
        }
        HideGameplayCanvases();
        if (localPlayer != null) localPlayer.gameObject.SetActive(false);
        ShowDeath("Has muerto", "Continuar", EnterSpectator);
        CheckWipe(MultiplayerGameplayProtocol.LocalActor);
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        if (!changedProps.ContainsKey(LifeKey) || session == null || !session.IsPlaying) return;
        CheckWipe(targetPlayer.ActorNumber);
        if (spectating) UpdateSpectator();
    }

    private void CheckWipe(int changedActor)
    {
        lastDeathCandidate = changedActor;
        if (!MultiplayerGameplayProtocol.Host || hasWipe || session == null || !session.IsPlaying) return;
        List<int> participants = MultiplayerGameplayProtocol.Participants();
        if (participants.Count == 0) return;
        foreach (int actor in participants) if (IsAlive(actor)) return;
        if (!PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable
            { { WipeKey, session.CurrentKey + "|" + changedActor } })) return;
        hasWipe = true;
        lastDeadActor = changedActor;
        ApplyWipe();
    }

    public override void OnRoomPropertiesUpdate(Hashtable changedProps)
    {
        if (!changedProps.ContainsKey(WipeKey) || session == null || !session.IsInMultiplayerScene) return;
        string value = changedProps[WipeKey] as string;
        string prefix = session.CurrentKey + "|";
        if (value == null || !value.StartsWith(prefix, StringComparison.Ordinal) ||
            !int.TryParse(value.Substring(prefix.Length), out int actor)) return;
        if (hasWipe && lastDeadActor == actor) return;
        hasWipe = true;
        lastDeadActor = actor;
        ApplyWipe();
    }

    private void ApplyWipe()
    {
        if (!localDead) return;
        if (MultiplayerGameplayProtocol.LocalActor == lastDeadActor)
            ShowDeath("Todos han muerto", "Volver", RequestDefeatReturn);
        else
        {
            ClearPanels();
            spectating = true;
        }
    }

    private void ShowDeath(string message, string button, Action action)
    {
        if (deathPanel != null) Destroy(deathPanel.gameObject);
        DeathPanelView prefab = Resources.Load<DeathPanelView>(DeathPrefabPath);
        if (prefab == null || !prefab.IsConfigured)
        {
            /*Debug.LogError("Falta Canvas Muerte configurado.", this)*/;
            if (!hasWipe) EnterSpectator();
            return;
        }
        deathPanel = Instantiate(prefab);
        deathPanel.Configure(message, button, action);
    }

    private void EnterSpectator()
    {
        if (deathPanel != null) Destroy(deathPanel.gameObject);
        deathPanel = null;
        spectating = true;
        UpdateSpectator();
    }

    private void UpdateSpectator()
    {
        List<int> living = LivingTeammates();
        if (living.Count == 0)
        {
            if (spectatorPanel != null) spectatorPanel.gameObject.SetActive(false);
            return;
        }
        if (!living.Contains(selectedActor)) selectedActor = living[0];
        if (living.Count > 1 && Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
            selectedActor = living[(living.IndexOf(selectedActor) + 1) % living.Count];
        MultiplayerPlayers players = GetComponentInChildren<MultiplayerPlayers>(true);
        Transform target = players != null ? players.TryGetLivingRemote(selectedActor) : null;
        if (target != null && cameraFollow != null) cameraFollow.SetTarget(target);
        if (spectatorPanel == null)
        {
            SpectatorPanelView prefab = Resources.Load<SpectatorPanelView>(SpectatorPrefabPath);
            if (prefab != null) spectatorPanel = Instantiate(prefab);
        }
        if (spectatorPanel != null)
        {
            spectatorPanel.gameObject.SetActive(true);
            spectatorPanel.SetMessage(living.Count > 1
                ? "Usa Tab para cambiar de vista entre los jugadores vivos"
                : "Espera al siguiente piso para revivir");
        }
    }

    private List<int> LivingTeammates()
    {
        List<int> result = MultiplayerGameplayProtocol.Participants();
        result.RemoveAll(actor => actor == MultiplayerGameplayProtocol.LocalActor || !IsAlive(actor));
        result.Sort();
        return result;
    }

    private void RequestDefeatReturn()
    {
        if (!hasWipe || lastDeadActor != MultiplayerGameplayProtocol.LocalActor) return;
        if (MultiplayerGameplayProtocol.Host)
        {
            ClearPanels();
            session.PublishDefeat();
        }
        else if (MultiplayerGameplayProtocol.Send(MultiplayerGameplayProtocol.DefeatReturnRequest,
            new MultiplayerDeathRequest(), true, MultiplayerGameplayProtocol.HostActor, flush: true)) ClearPanels();
    }

    public bool RequestFloorAdvance()
    {
        if (session == null || !session.IsPlaying || !IsAlive(MultiplayerGameplayProtocol.LocalActor)) return false;
        return MultiplayerGameplayProtocol.Send(MultiplayerGameplayProtocol.FloorRequest,
            new MultiplayerDeathRequest(), true, MultiplayerGameplayProtocol.HostActor, flush: true);
    }

    public void OnEvent(EventData photonEvent)
    {
        if (photonEvent.Code != MultiplayerGameplayProtocol.FloorRequest &&
            photonEvent.Code != MultiplayerGameplayProtocol.DefeatReturnRequest) return;
        if (!MultiplayerGameplayProtocol.Host || !MultiplayerGameplayProtocol.Read(photonEvent, out MultiplayerDeathRequest _)) return;
        if (photonEvent.Code == MultiplayerGameplayProtocol.FloorRequest)
            session.TryAdvanceFromActor(photonEvent.Sender);
        else if (photonEvent.Code == MultiplayerGameplayProtocol.DefeatReturnRequest &&
            hasWipe && photonEvent.Sender == lastDeadActor) session.PublishDefeat();
    }

    public void OnFloorBegin(int floor)
    {
        ClearPanels();
        spectating = false;
        if (currentFloor == floor) return;
        currentFloor = floor;
        hasWipe = false;
        lastDeadActor = 0;
        lastDeathCandidate = 0;
    }

    public void OnFloorReady(int floor)
    {
        BindPlayer();
        if (localPlayer == null || localClass == null) return;
        if (localDead && floor <= deathFloor) return;
        if (localDead)
        {
            localPlayer.gameObject.SetActive(true);
            localClass.ReviveForNextFloor(5);
            if (MultiplayerWorldSession.Instance != null && MultiplayerWorldSession.Instance.Loot != null)
                MultiplayerWorldSession.Instance.Loot.StopDiscarding();
            RestoreGameplayCanvases();
            if (cameraFollow != null && originalTarget != null) cameraFollow.SetTarget(originalTarget);
            ClearPanels();
            spectating = false;
            selectedActor = 0;
            localDead = false;
            deathFloor = 0;
        }
        if (!PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { LifeKey, session.CurrentKey + "|1" } }))
            session.ReportBuildFailed("No se pudo sincronizar la reaparicion del jugador.");
        MultiplayerPlayers players = GetComponentInChildren<MultiplayerPlayers>(true);
        if (players != null) players.PublishLocalEquipment();
    }

    private void HideGameplayCanvases()
    {
        if (NewestSingleton.Instance == null) return;
        PauseController pause = NewestSingleton.Instance.GetComponentInChildren<PauseController>(true);
        if (pause != null) { pause.ResumeGame(); pause.SetExternalUIBlocked(true); }
        foreach (Canvas canvas in NewestSingleton.Instance.GetComponentsInChildren<Canvas>(true))
        {
            canvasStates[canvas.gameObject] = canvas.gameObject.activeSelf;
            canvas.gameObject.SetActive(false);
        }
    }

    private void RestoreGameplayCanvases()
    {
        foreach (var pair in canvasStates) if (pair.Key != null) pair.Key.SetActive(pair.Value);
        canvasStates.Clear();
        if (NewestSingleton.Instance == null) return;
        PauseController pause = NewestSingleton.Instance.GetComponentInChildren<PauseController>(true);
        if (pause != null) pause.SetExternalUIBlocked(false);
    }

    private void ClearPanels()
    {
        if (deathPanel != null) Destroy(deathPanel.gameObject);
        if (spectatorPanel != null) Destroy(spectatorPanel.gameObject);
        deathPanel = null;
        spectatorPanel = null;
    }

    private void ResetLocalState()
    {
        UnbindPlayer();
        ClearPanels();
        canvasStates.Clear();
        cameraFollow = null;
        originalTarget = null;
        localDead = spectating = hasWipe = false;
        lastDeadActor = lastDeathCandidate = selectedActor = currentFloor = deathFloor = 0;
    }
}

[Serializable]
public sealed class MultiplayerDeathRequest { public int version = 1; }
