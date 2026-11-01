using UnityEngine;
using UnityEngine.SceneManagement;

public class NewestSingleton : MonoBehaviour
{
    public static NewestSingleton Instance { get; private set; }
    private static bool resetOnVillageLoad;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
        resetOnVillageLoad = false;
    }

    public static void RequestVillagePlayerReset() { resetOnVillageLoad = true; }
    public static void CancelVillagePlayerReset() { resetOnVillageLoad = false; }

    private void Awake()
    {
        if (!PersistentScenePolicy.IsAllowed(gameObject.scene))
        {
            gameObject.SetActive(false);
            Destroy(gameObject);
            return;
        }

        if (resetOnVillageLoad && gameObject.scene.name == "VillageMenuScene")
        {
            resetOnVillageLoad = false;
            NewestSingleton previous = Instance;
            Instance = this;
            if (previous != null && previous != this)
            {
                previous.gameObject.SetActive(false);
                Destroy(previous.gameObject);
            }
            DontDestroyOnLoad(gameObject);
            return;
        }

        // Conserva al personaje y su inventario al cargar otra escena.
        if (Instance != null && Instance != this)
        {
            // Usa la posicion inicial configurada para el personaje en la escena nueva.
            Instance.transform.SetPositionAndRotation(transform.position, transform.rotation);

            // La instancia existente permanece como singleton; se descarta el duplicado.
            gameObject.SetActive(false);
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        if (Instance == this) SceneManager.activeSceneChanged += OnActiveSceneChanged;
    }

    private void Start()
    {
        OnActiveSceneChanged(default, SceneManager.GetActiveScene());
    }

    private void OnDisable()
    {
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
    }

    private void OnActiveSceneChanged(Scene previous, Scene current)
    {
        if (Instance != this || PersistentScenePolicy.IsAllowed(current)) return;
        CancelVillagePlayerReset();
        Instance = null;
        gameObject.SetActive(false);
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        // Destruir la instancia anterior no debe borrar la referencia a la nueva.
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
