using UnityEngine;

public partial class WorldGenerator : MonoBehaviour
{
    [SerializeField] private DungeonInstructionsGenerator instructionsGenerator;

    // El objeto padre de el mundo, aqui se van a generar los objetos de el mundo para eliminarlo con mayor facilidad
    private GameObject generatedWorld;

    void Start()
    {
        RegenerateWorld();
    }

    // Genera instrucciones, elimina el mundo, genera el nuevo mundo.
    public void RegenerateWorld()
    {
        // Genera las instrucciones en Json.
        instructionsGenerator.RegenerateFloorInstructions();

        // Lee el Json
        LoadFloorInstructions();

        // Elimina el mundo
        DestroyPreviousWorld();

        // Create a new parent object for the world.
        generatedWorld = new GameObject ("GeneratedWorld");
        
        // Genera el piso de el mundo
        GenerateFloor();

        // Genera los puentes con paredes
        GenerateBridge();
    }

    // Destruir el mundo generado
    private void DestroyPreviousWorld()
    {
        if (generatedWorld != null)
        {
            // Elimina el objeto padre de el mundo generado
            Destroy(generatedWorld);

            generatedWorld = null;
        }
    }
}
