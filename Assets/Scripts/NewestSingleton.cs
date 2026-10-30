using UnityEngine;

public class NewestSingleton : MonoBehaviour
{
    public static NewestSingleton Instance { get; private set; }

    private void Awake()
    {
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

    private void OnDestroy()
    {
        // Destruir la instancia anterior no debe borrar la referencia a la nueva.
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
