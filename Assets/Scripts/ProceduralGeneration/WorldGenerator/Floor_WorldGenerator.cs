using UnityEngine;

public partial class WorldGenerator : MonoBehaviour
{
    // Genera el piso de todos los cuartos en la posicion adecuada
    private void GenerateFloor()
    {
        Direction? previousExitDirection = null;

        foreach (RoomData room in floorData.rooms)
        {
            // Obtiene el prefab usando el room ID.
            GameObject roomPrefab = Resources.Load<GameObject>("GeneracionDeCuartos/Cuartos/" + room.roomID);

            // Verifica que existe el prefab
            if (roomPrefab == null)
            {
                Debug.LogError("No se encontro el prefab de el cuarto: " + room.roomID);
                return;
            }

            // Instancia el prefab de el cuarto en la posicion definida.
            GameObject newRoom = Instantiate(roomPrefab, room.gridPosition, Quaternion.identity, generatedWorld.transform);

            GeneratedRoomController roomController = newRoom.GetComponent<GeneratedRoomController>();
            if (roomController == null)
            {
                Debug.LogError("El prefab del cuarto '" + room.roomID +"' no tiene GeneratedRoomController.");
                return;
            }

            roomController.enemyType = room.enemyType;
            
            // Si existe una habitacion anterior, su salida opuesta sera la entrada de la habitacion actual. En la primera habitacion no hay salida anterior, por eso enterDirection sera null.
            Direction? enterDirection = previousExitDirection.HasValue? GetOppositeDirection(previousExitDirection.Value): null;
            // Guarda la salida de esta habitacion. Se usara como referencia para calcular el enter de la siguiente habitacion.
            previousExitDirection = room.exitDirection;

            // Genera paredes
            GenerateWallsForRoom(room, enterDirection);

            // Genera puertas con paredes a los lados
            GenerateDoorsForRoom(room, enterDirection, roomController);
        }
    }
}
