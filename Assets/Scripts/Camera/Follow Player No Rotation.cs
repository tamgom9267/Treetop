using UnityEngine;

public class FollowPlayerNoRotation : MonoBehaviour
{
    // Camara que queremos mover
    [SerializeField] private Camera cameraToFollow;

    // Personaje que la camara debe seguir
    [SerializeField] private Transform player;

    // Distancia y rotacion que queremos mantener entre la camara y el personaje
    [SerializeField] private Vector3 offset = new Vector3(0f, 10f, -8f);
    [SerializeField] private Vector3 cameraRotation = new Vector3(45f, 0f, 0f);

    private void Start()
    {
        cameraToFollow.transform.rotation = Quaternion.Euler(cameraRotation);
    }

    void LateUpdate()
    {
        // Calculamos la posicion donde deberia de estar la camara.
        Vector3 newPosition = player.position + offset;

        // Cambiamos la posicion de la camara
        cameraToFollow.transform.position = newPosition;
    }
}
