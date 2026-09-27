using UnityEngine;

public class Chest : MonoBehaviour
{
    // Estado para definir si se interacuo con el cofre
    public bool chestInteracted;

    private void Start()
    {
        // inicia como falso
        chestInteracted = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        // Solo se activa si el jugador "Player" entra
        if (!other.CompareTag("Player"))
            return;

        // Se activa el estado de interaccion con el cofre
        chestInteracted = true;
    }
}
