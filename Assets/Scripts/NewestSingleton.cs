using UnityEngine;

public class NewestSingleton : MonoBehaviour
{
    public static NewestSingleton Instance { get; private set; }

    private void Awake()
    {
        // La instancia de la escena nueva reemplaza a la que venia de otra escena.
        if (Instance != null && Instance != this)
        {
            Instance.gameObject.SetActive(false);
            Destroy(Instance.gameObject);
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
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
