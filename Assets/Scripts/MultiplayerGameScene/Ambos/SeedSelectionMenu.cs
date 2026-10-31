using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Colocar fuera del panel de seed, sobre un objeto que permanezca activo en la aldea.</summary>
[DefaultExecutionOrder(-90)]
public class SeedSelectionMenu : MonoBehaviour
{
    [SerializeField] private VillageMultiplayerMenu villageMenu;
    [SerializeField] private GameObject seedPanel;
    [SerializeField] private TMP_InputField seedInput;
    [SerializeField] private TMP_Text errorText;
    [SerializeField] private Button randomButton;
    [SerializeField] private Button customButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private string soloSceneName = "GameScene";

    private PhotonPartyService party;
    private string originalRoomCode;
    private bool multiplayer;
    private bool confirming;
    public bool IsOpen { get; private set; }

    private void Awake()
    {
        if (seedInput != null)
        {
            seedInput.characterLimit = 5;
            seedInput.lineType = TMP_InputField.LineType.SingleLine;
            seedInput.contentType = TMP_InputField.ContentType.Custom;
            seedInput.characterValidation = TMP_InputField.CharacterValidation.Digit;
        }
        SetPanel(false);
    }

    public bool TryOpen(VillageDungeonPortal source)
    {
        if (source == null || IsOpen || Time.timeScale <= 0f) return false;
        party = PhotonPartyService.Instance;
        if (party != null && party.IsBusy) return false;
        if (party != null && party.IsInParty && (!party.IsHost || !party.IsPartyLocked || party.Members.Count < 2))
        {
            if (villageMenu != null) villageMenu.ShowExternalNotice(party.IsHost
                ? "Reune al menos 2 jugadores y pulsa Listo antes de entrar al portal."
                : "Espera a que el host elija la seed e inicie la partida.");
            return false;
        }
        if (seedPanel == null || seedPanel == gameObject || transform.IsChildOf(seedPanel.transform) || villageMenu == null)
        {
            Debug.LogError("Asigna Seed Panel y Village Menu; SeedSelectionMenu debe estar fuera de su panel.", this);
            return false;
        }
        if (!villageMenu.TryOpenExternalMenu()) return false;
        multiplayer = party != null && party.IsInParty;
        originalRoomCode = multiplayer ? party.RoomCode : string.Empty;
        confirming = false;
        IsOpen = true;
        if (errorText != null) errorText.text = string.Empty;
        SetPanel(true);
        RefreshButtons();
        return true;
    }

    private void Update()
    {
        if (!IsOpen) return;
        if (multiplayer && !PartyStillValid()) { Close(); return; }
        RefreshButtons();
    }

    private bool PartyStillValid()
    {
        return party != null && party.IsInParty && party.IsHost && party.IsPartyLocked &&
               party.RoomCode == originalRoomCode && party.Members.Count >= 2;
    }

    public void StartWithRandomSeed()
    {
        if (!IsOpen || confirming) return;
        Begin(Random.Range(RunSeedSettings.MinSeed, RunSeedSettings.MaxSeed + 1));
    }

    public void StartWithCustomSeed()
    {
        if (!IsOpen || confirming) return;
        if (seedInput == null || !RunSeedSettings.TryParse(seedInput.text, out int seed))
        {
            Error("Escribe una seed de 0 a 99999, usando de 1 a 5 digitos.");
            return;
        }
        Begin(seed);
    }

    private void Begin(int seed)
    {
        if (multiplayer && !PartyStillValid()) { Close(); return; }
        if (!multiplayer && party != null && (party.IsInParty || party.IsBusy))
        {
            Error("La party cambio. Cierra el panel y vuelve a tocar el portal.");
            return;
        }
        confirming = true;
        RefreshButtons();
        if (multiplayer)
        {
            MultiplayerRunSession session = MultiplayerRunSession.Instance;
            string message = "Agrega MultiplayerRunSession al objeto persistente MultiplayerSession.";
            if (session == null || !session.BeginRun(seed, out message))
            {
                confirming = false;
                Error(message);
                RefreshButtons();
                return;
            }
            Close();
        }
        else
        {
            if (!Application.CanStreamedLevelBeLoaded(soloSceneName))
            {
                confirming = false;
                Error("Agrega " + soloSceneName + " al perfil de compilacion.");
                RefreshButtons();
                return;
            }
            RunSeedSettings.SelectSoloSeed(seed);
            Close();
            SceneManager.LoadScene(soloSceneName);
        }
    }

    public void CancelSeedSelection()
    {
        if (!confirming) Close();
    }

    private void Close()
    {
        bool wasOpen = IsOpen;
        IsOpen = false;
        confirming = false;
        SetPanel(false);
        if (wasOpen && villageMenu != null) villageMenu.CloseExternalMenu();
    }

    private void OnDisable() { Close(); }
    private void Error(string message) { if (errorText != null) errorText.text = message; else Debug.LogWarning(message, this); }
    private void SetPanel(bool active)
    {
        if (seedPanel != null && !transform.IsChildOf(seedPanel.transform)) seedPanel.SetActive(active);
    }
    private void RefreshButtons()
    {
        if (randomButton != null) randomButton.interactable = IsOpen && !confirming;
        if (customButton != null) customButton.interactable = IsOpen && !confirming;
        if (cancelButton != null) cancelButton.interactable = IsOpen && !confirming;
        if (seedInput != null) seedInput.interactable = IsOpen && !confirming;
    }
}
