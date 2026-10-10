using UnityEngine;

public partial class WorldGenerator : MonoBehaviour
{
    // Genera el piso de todos los cuartos en la posicion adecuada
    private void GenerateFloor()
    {
        foreach (RoomData room in floorData.rooms)
        {
            // Obtiene el prefab usando el room ID.
            GameObject roomPrefab = Resources.Load<GameObject>("GeneracionDeCuartos/Cuartos/" + room.roomID);

            // Verifica que existe el prefab
            if (roomPrefab == null)
            {
                Debug.LogError("No se encontro el prefab de el cuarto: " + room.roomID);
            }

            // Instancia el prefab de el cuarto en la posicion definida.
            GameObject newRoom = Instantiate(roomPrefab, room.gridPosition, Quaternion.identity, generatedWorld.transform);
        }
    }
}
