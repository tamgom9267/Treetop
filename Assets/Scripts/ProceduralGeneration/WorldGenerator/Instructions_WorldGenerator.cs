using System.IO;
using UnityEngine;

public partial class WorldGenerator : MonoBehaviour
{
    // Guarda la informacion de el Json como variable
    private FloorData floorData;

    private void LoadFloorInstructions()
    {
        // define la ruta de el archivo
        string path = Path.Combine( Application.persistentDataPath, "floorData.json");

        // verifica que el Json Existe
        if (!File.Exists(path))
        {
            Debug.LogError("No se encontro el json: " + path);
            return;
        }

        // Lee el archivo json
        string json = File.ReadAllText(path);

        // Convierte el json en FloorData
        floorData = JsonUtility.FromJson<FloorData>(json);
    }
}
