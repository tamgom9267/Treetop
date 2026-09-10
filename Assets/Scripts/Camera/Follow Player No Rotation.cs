using UnityEngine;

public class FollowPlayerNoRotation : MonoBehaviour
{
    // Camara que queremos mover
    [SerializeField] private Camera cameraToFollow;

    // Personaje que la camara debe seguir
    [SerializeField] private Transform player;

    // Distancia que queremos mantener entre la camara y el personaje
    [SerializeField] private Vector3 offset;


    void LateUpdate()
    {
        // Calculamos la posicion donde deberia de estar la camara.
        Vector3 newPosition = player.position + offset;

        // Cambiamos la posicion de la camara
        cameraToFollow.transform.position = newPosition;
    }
}
