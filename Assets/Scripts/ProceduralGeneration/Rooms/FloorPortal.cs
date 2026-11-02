using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class FloorPortal : MonoBehaviour
{
    private const string VillageSceneName = "VillageMenuScene";
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

        if (generator.FloorNumber >= 5)
        {
            if (!Application.CanStreamedLevelBeLoaded(VillageSceneName))
            {
                Debug.LogError("Falta VillageMenuScene en el perfil de compilacion.", this);
                return;
            }

            activated = true;
            try { SceneManager.LoadScene(VillageSceneName); }
            catch (Exception exception)
            {
                activated = false;
                Debug.LogError("No se pudo regresar a la aldea: " + exception.Message, this);
            }
            return;
        }

        // El portal anterior se destruye al regenerar el mundo.
        activated = true;
        generator.RegenerateWorld();
    }
}
