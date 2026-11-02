using UnityEngine;
using UnityEngine.SceneManagement;

public class NewestSingleton : MonoBehaviour
{
    public static NewestSingleton Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
    }

    private void Awake()
    {
        if (!PersistentScenePolicy.IsAllowed(gameObject.scene))
        {
            gameObject.SetActive(false);
            Destroy(gameObject);
            return;
        }

        // La instancia nueva siempre reemplaza a la anterior, incluido su inventario.
        NewestSingleton previous = Instance;
        Instance = this;
        if (previous != null && previous != this)
        {
            previous.gameObject.SetActive(false);
            Destroy(previous.gameObject);
        }

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
