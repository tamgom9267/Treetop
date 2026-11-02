using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Colocar fuera de los paneles que se desactivan. Conectar los metodos publicos en OnClick.</summary>
[DefaultExecutionOrder(-100)]
public class VillageMultiplayerMenu : MonoBehaviour
{
    [Header("Paneles existentes")]
    [SerializeField] private GameObject multiplayerPanel;
    [SerializeField] private GameObject createRoomPanel;
    [SerializeField] private GameObject joinRoomPanel;
    [SerializeField] private GameObject roomViewPanel;
    [SerializeField] private GameObject compactPartyPanel;
    [SerializeField] private GameObject confirmLeavePanel;
    [SerializeField] private GameObject noticePanel;
    [SerializeField] private TMP_Text noticeText;
    [SerializeField] private TMP_Text statusText;

    [Header("Campos de entrada")]
    [Tooltip("Usar RoomNameInputField como nombre del host.")]
    [SerializeField] private TMP_InputField hostNameInput;
    [SerializeField] private TMP_InputField clientNameInput;
    [SerializeField] private TMP_InputField roomCodeInput;

    [Header("Vista del cuarto")]
    [SerializeField] private TMP_Text roomCodeText;
    [SerializeField] private TMP_Text roomNameText;
    [SerializeField] private Button createButton;
    [SerializeField] private Button joinButton;
    [SerializeField] private Button readyButton;
    [SerializeField] private Button cancelPartyButton;
    [Tooltip("Botones de navegar, salir y confirmar que deben esperar durante una operacion de red.")]
    [SerializeField] private Button[] navigationButtons = new Button[0];

    private enum Screen { None, Multiplayer, Create, Join, Room, ConfirmLeave }
    private Screen screen;
    private PhotonPartyService service;
    private PlayerController blockedPlayer;
    private PauseController blockedPause;
    private bool wasInParty;
    private bool wasLocked;
    private bool noticeVisible;
    private bool externalMenuOpen;

    public bool IsModalOpen => screen != Screen.None || noticeVisible || externalMenuOpen;
    private static bool IsRunActive => MultiplayerRunSession.Instance != null && MultiplayerRunSession.Instance.IsRunning;

    public bool TryOpenExternalMenu()
    {
        AttachService();
        PlayerController player = MultiplayerStartingRoomSpawns.LocalPlayer;
        if (IsRunActive || IsModalOpen || Time.timeScale <= 0f || player != null && player.IsInventoryOpen ||
            service != null && service.IsBusy) return false;
        externalMenuOpen = true;
        RenderPanels();
        UpdateInputBlock();
        return true;
    }

    public void CloseExternalMenu()
    {
        externalMenuOpen = false;
        RenderPanels();
        UpdateInputBlock();
    }

    public void ShowExternalNotice(string message)
    {
        ShowNotice(message);
    }

    private void Start()
    {
        if (MultiplayerRunSession.TryConsumeVillageNotice(out string message)) ShowNotice(message);
    }

    private void Awake()
    {
        ConfigureNameField(hostNameInput);
        ConfigureNameField(clientNameInput);
        if (roomCodeInput != null)
        {
            roomCodeInput.characterLimit = 5;
            roomCodeInput.lineType = TMP_InputField.LineType.SingleLine;
            roomCodeInput.contentType = TMP_InputField.ContentType.Custom;
            roomCodeInput.characterValidation = TMP_InputField.CharacterValidation.Digit;
        }
        SetScreen(Screen.None);
    }

    private void OnEnable()
    {
        AttachService();
        if (hostNameInput != null) hostNameInput.onValueChanged.AddListener(OnInputChanged);
        if (clientNameInput != null) clientNameInput.onValueChanged.AddListener(OnInputChanged);
        if (roomCodeInput != null) roomCodeInput.onValueChanged.AddListener(OnInputChanged);
    }

    private void OnDisable()
    {
        if (service != null)
        {
            service.PartyChanged -= OnPartyChanged;
            service.OperationFailed -= ShowNotice;
            service.PartyEnded -= OnPartyEnded;
        }
        service = null;
        if (hostNameInput != null) hostNameInput.onValueChanged.RemoveListener(OnInputChanged);
        if (clientNameInput != null) clientNameInput.onValueChanged.RemoveListener(OnInputChanged);
        if (roomCodeInput != null) roomCodeInput.onValueChanged.RemoveListener(OnInputChanged);
        noticeVisible = false;
        externalMenuOpen = false;
        SetScreen(Screen.None);
        SetPanel(compactPartyPanel, false);
    }

    private void Update()
    {
        AttachService();
        RefreshControls(); // El cooldown avanza en tiempo real aunque no haya callbacks.
        if (IsModalOpen && blockedPlayer == null)
            UpdateInputBlock();
    }

    private void AttachService()
    {
        PhotonPartyService available = PhotonPartyService.AvailableInstance;
        if (service == available) return;
        if (service != null)
        {
            service.PartyChanged -= OnPartyChanged;
            service.OperationFailed -= ShowNotice;
            service.PartyEnded -= OnPartyEnded;
        }
        service = available;
        wasInParty = wasLocked = false;
        if (service == null)
        {
            SetScreen(Screen.None);
            return;
        }
        service.PartyChanged += OnPartyChanged;
        service.OperationFailed += ShowNotice;
        service.PartyEnded += OnPartyEnded;
        OnPartyChanged();
    }

    public void OpenFromInteraction()
    {
        if (IsModalOpen || IsRunActive || PhotonPartyService.IsSceneExitInProgress) return;
        AttachService();
        if (service == null)
        {
            ShowNotice("Agrega PhotonPartyService a un objeto independiente de la aldea.");
            return;
        }
        if (service.IsBusy) return;
        if (service.IsInParty)
        {
            if (service.IsHost)
            {
                service.ReopenParty();
                SetScreen(Screen.Room);
            }
            else
                ShowLeaveConfirmation();
        }
        else
        {
            SetScreen(Screen.Multiplayer);
            service.ConnectIfNeeded();
        }
    }

    public void ShowCreateRoom()
    {
        if (CanNavigate() && !service.IsInParty) SetScreen(Screen.Create);
    }

    public void ShowJoinRoom()
    {
        if (CanNavigate() && !service.IsInParty) SetScreen(Screen.Join);
    }

    public void BackToMultiplayer()
    {
        if (CanNavigate() && !service.IsInParty) SetScreen(Screen.Multiplayer);
    }

    public void CreateParty()
    {
        if (CanNavigate()) service.CreateParty(hostNameInput != null ? hostNameInput.text : string.Empty);
    }

    public void JoinParty()
    {
        if (CanNavigate()) service.JoinParty(clientNameInput != null ? clientNameInput.text : string.Empty,
            roomCodeInput != null ? roomCodeInput.text : string.Empty);
    }

    public void ReadyParty()
    {
        if (CanNavigate()) service.ReadyParty();
    }

    public void CancelParty()
    {
        if (CanNavigate()) service.LeaveParty();
    }

    public void CloseMenus()
    {
        if (!CanNavigate()) return;
        noticeVisible = false;
        SetScreen(Screen.None);
    }

    public void ShowLeaveConfirmation()
    {
        if (!CanNavigate() || !service.IsInParty || service.IsHost) return;
        if (confirmLeavePanel == null)
        {
            ShowNotice("Asigna el panel de confirmacion de abandono en VillageMultiplayerMenu.");
            return;
        }
        SetScreen(Screen.ConfirmLeave);
    }

    public void ConfirmLeaveParty()
    {
        if (CanNavigate() && screen == Screen.ConfirmLeave && service.IsInParty && !service.IsHost)
            service.LeaveParty();
    }

    public void DeclineLeaveParty()
    {
        if (CanNavigate() && screen == Screen.ConfirmLeave) SetScreen(Screen.None);
    }

    public void DismissNotice()
    {
        noticeVisible = false;
        RenderPanels();
        UpdateInputBlock();
    }

    private bool CanNavigate()
    {
        AttachService();
        return service != null && !service.IsBusy && !IsRunActive;
    }

    private void OnPartyChanged()
    {
        if (service == null || service.IsShuttingDown) { AttachService(); return; }
        bool inParty = service.IsInParty;
        bool locked = inParty && service.IsPartyLocked;
        if (inParty && !wasInParty)
        {
            noticeVisible = false;
            screen = locked ? Screen.None : Screen.Room;
        }
        if (locked && !wasLocked || !inParty && wasInParty)
        {
            noticeVisible = false;
            screen = Screen.None;
        }
        wasInParty = inParty;
        wasLocked = locked;
        if (roomCodeText != null) roomCodeText.text = service.RoomCode;
        if (roomNameText != null)
        {
            roomNameText.richText = false;
            roomNameText.text = service.RoomDisplayName;
        }
        RenderPanels();
        RefreshControls();
        UpdateInputBlock();
    }

    private void OnPartyEnded(PartyEndReason reason)
    {
        screen = Screen.None;
        ShowNotice(PartyRules.EndMessage(reason));
    }

    private void ShowNotice(string message)
    {
        if (noticeText != null) noticeText.text = message;
        if (noticePanel != null)
            noticeVisible = true;
        else
        {
            Debug.LogWarning(message, this);
            if (statusText != null) statusText.text = message;
        }
        RenderPanels();
        UpdateInputBlock();
    }

    private void SetScreen(Screen value)
    {
        screen = value;
        RenderPanels();
        RefreshControls();
        UpdateInputBlock();
    }

    private void RenderPanels()
    {
        SetPanel(multiplayerPanel, screen == Screen.Multiplayer);
        SetPanel(createRoomPanel, screen == Screen.Create);
        SetPanel(joinRoomPanel, screen == Screen.Join);
        SetPanel(roomViewPanel, screen == Screen.Room);
        SetPanel(confirmLeavePanel, screen == Screen.ConfirmLeave);
        SetPanel(noticePanel, noticeVisible);
        SetPanel(compactPartyPanel, service != null && service.IsInParty && service.IsPartyLocked && !IsModalOpen);
    }

    private void SetPanel(GameObject panel, bool active)
    {
        if (panel == null || panel.activeSelf == active) return;
        if (transform.IsChildOf(panel.transform))
        {
            Debug.LogError("VillageMultiplayerMenu debe estar fuera de los paneles que controla.", this);
            return;
        }
        panel.SetActive(active);
    }

    private void RefreshControls()
    {
        bool idle = service != null && !service.IsBusy;
        bool canEnter = idle && !service.IsInParty && service.IsConnected && service.RemainingJoinCooldown <= 0f;
        if (createButton != null) createButton.interactable = canEnter && hostNameInput != null &&
            PartyRules.TryNormalizeName(hostNameInput.text, out _);
        if (joinButton != null) joinButton.interactable = canEnter && clientNameInput != null &&
            PartyRules.TryNormalizeName(clientNameInput.text, out _) && roomCodeInput != null &&
            PartyRules.IsValidRoomCode(roomCodeInput.text);
        // Permitir pulsarlo con 1 jugador para mostrar el aviso solicitado.
        if (readyButton != null) readyButton.interactable = idle && service.IsHost && !service.IsPartyLocked;
        if (cancelPartyButton != null) cancelPartyButton.interactable = idle && service.IsInParty;
        foreach (Button button in navigationButtons)
            if (button != null) button.interactable = idle;
        if (hostNameInput != null) hostNameInput.interactable = idle;
        if (clientNameInput != null) clientNameInput.interactable = idle;
        if (roomCodeInput != null) roomCodeInput.interactable = idle;
        if (statusText == null || service == null || noticeVisible) return;
        if (service.IsBusy)
            statusText.text = service.Operation == PartyOperation.Connecting ? "Conectando con Photon..." :
                service.Operation == PartyOperation.Leaving ? "Saliendo de la sala..." : "Esperando confirmacion de Photon...";
        else if (service.RemainingJoinCooldown > 0f)
            statusText.text = "Podras crear o unirte en " + Mathf.CeilToInt(service.RemainingJoinCooldown) + " s.";
        else
            statusText.text = service.IsInParty && !service.IsHost ? "Esperando al host." : string.Empty;
    }

    private void UpdateInputBlock()
    {
        if (!IsModalOpen)
        {
            if (blockedPlayer != null) blockedPlayer.SetExternalUIBlocked(false);
            if (blockedPause != null) blockedPause.SetExternalUIBlocked(false);
            blockedPlayer = null;
            blockedPause = null;
            return;
        }
        if (blockedPlayer == null)
        {
            blockedPlayer = NewestSingleton.Instance != null
                ? NewestSingleton.Instance.GetComponentInChildren<PlayerController>()
                : FindFirstObjectByType<PlayerController>();
            if (blockedPlayer != null) blockedPlayer.SetExternalUIBlocked(true);
        }
        if (blockedPause == null)
        {
            blockedPause = FindFirstObjectByType<PauseController>();
            if (blockedPause != null) blockedPause.SetExternalUIBlocked(true);
        }
    }

    private static void ConfigureNameField(TMP_InputField field)
    {
        if (field == null) return;
        field.characterLimit = PartyRules.MaxNameLength;
        field.lineType = TMP_InputField.LineType.SingleLine;
    }

    private void OnInputChanged(string value)
    {
        RefreshControls();
    }
}
