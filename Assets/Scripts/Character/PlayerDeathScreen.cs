using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(PlayerClass))]
public class PlayerDeathScreen : MonoBehaviour
{
    private const string TutorialScene = "TutorialScene";
    private const string SoloScene = "GameScene";
    private const string VillageScene = "VillageMenuScene";
    private const string DeathPanelResource = "Canvas Muerte/Canvas Muerte";

    [Header("Textos de muerte")]
    [SerializeField] private string tutorialMessage = "Has muerto en el tutorial";
    [SerializeField] private string tutorialButtonText = "Reiniciar";
    [SerializeField] private string soloMessage = "Has muerto en la mazmorra";
    [SerializeField] private string soloButtonText = "Volver";

    private PlayerClass playerClass;
    private PlayerController playerController;
    private bool showing;

    private void Awake()
    {
        playerClass = GetComponent<PlayerClass>();
        playerController = GetComponent<PlayerController>();
    }

    private void OnEnable()
    {
        playerClass.OnDied += Show;
    }

    private void OnDisable()
    {
        playerClass.OnDied -= Show;
    }

    private void Show()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        if (showing || (sceneName != TutorialScene && sceneName != SoloScene)) return;

        DeathPanelView prefab = Resources.Load<DeathPanelView>(DeathPanelResource);
        if (prefab == null || !prefab.IsConfigured)
        {
            /*Debug.LogError("Configura los textos y el boton en el prefab Canvas Muerte.", this)*/;
            return;
        }

        bool tutorial = sceneName == TutorialScene;
        DeathPanelView panel = Instantiate(prefab);
        panel.Configure(tutorial ? tutorialMessage : soloMessage,
            tutorial ? tutorialButtonText : soloButtonText,
            () => Continue(tutorial ? TutorialScene : VillageScene));

        showing = true;
        if (playerController != null) playerController.SetExternalUIBlocked(true);
        PauseController pauseController = FindFirstObjectByType<PauseController>();
        if (pauseController != null) pauseController.SetExternalUIBlocked(true);
        if (tutorial)
        {
            HintController hintController = FindFirstObjectByType<HintController>();
            if (hintController != null) hintController.enabled = false;
        }
        Time.timeScale = 0f;
    }

    private static void Continue(string destination)
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(destination);
    }
}
