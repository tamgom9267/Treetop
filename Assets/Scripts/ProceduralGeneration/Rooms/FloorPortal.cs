using UnityEngine;

public class FloorPortal : MonoBehaviour
{
    private bool activated;

    private void OnTriggerEnter(Collider other)
    {
        if (activated || !other.CompareTag("Player"))
            return;

        WorldGenerator generator = FindFirstObjectByType<WorldGenerator>();
        if (generator == null)
        {
            Debug.LogWarning("No se encontro el generador para crear el siguiente piso.", this);
            return;
        }

        // El portal anterior se destruye al regenerar el mundo.
        activated = true;
        generator.RegenerateWorld();
    }
}
