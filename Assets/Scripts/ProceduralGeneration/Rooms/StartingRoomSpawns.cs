using UnityEngine;

public class StartingRoomSpawns : MonoBehaviour
{
    [SerializeField] private GameObject hostSpawn;
    [SerializeField] private GameObject clientSpawn1;
    [SerializeField] private GameObject clientSpawn2;

    private void Start()
    {
        if (hostSpawn == null)
        {
            Debug.LogWarning("Asigna el Host Spawn en el cuarto inicial.", this);
            return;
        }

        NewestSingleton singleton = NewestSingleton.Instance;
        if (singleton == null)
        {
            Debug.LogWarning("No se encontro el singleton del personaje para ubicarlo en el Host Spawn.", this);
            return;
        }

        PlayerController player = singleton.GetComponentInChildren<PlayerController>();
        if (player == null)
        {
            Debug.LogWarning("El singleton no contiene un PlayerController.", singleton);
            return;
        }

        // El jugador puede haberse desplazado respecto al padre desde el piso anterior.
        Vector3 displacement = hostSpawn.transform.position - player.transform.position;
        singleton.transform.position += displacement;
    }
}
