using UnityEngine;

public partial class WorldGenerator : MonoBehaviour
{
    private void GenerateWallsForRoom(RoomData room, Direction? enterDirection)
    {
        // Obtiene los prefabs
        GameObject paredHorizontal = Resources.Load<GameObject>("GeneracionDeCuartos/Paredes/ParedHorizontal");
        GameObject paredVertical = Resources.Load<GameObject>("GeneracionDeCuartos/Paredes/ParedVertical");

        // Verifica que existe el prefab
        if (paredHorizontal == null || paredVertical == null)
        {
            Debug.LogError("No se encontro el prefab de la pared");
            return;
        }

        if (room.exitDirection != Direction.North && enterDirection != Direction.North)
        {
            Vector3 wallLocation = room.gridPosition + new Vector3(0, 0, 13.5f);
            Instantiate(paredHorizontal, wallLocation, Quaternion.identity, generatedWorld.transform);
        }
        if (room.exitDirection != Direction.East && enterDirection != Direction.East)
        {
            Vector3 wallLocation = room.gridPosition + new Vector3(21.5f, 0, 0);
            Instantiate(paredVertical, wallLocation, Quaternion.identity, generatedWorld.transform);
        }
        if (room.exitDirection != Direction.South && enterDirection != Direction.South)
        {
            Vector3 wallLocation = room.gridPosition + new Vector3(0, 0, -13.5f);
            Instantiate(paredHorizontal, wallLocation, Quaternion.identity, generatedWorld.transform);
        }
        if (room.exitDirection != Direction.West && enterDirection != Direction.West)
        {
            Vector3 wallLocation = room.gridPosition + new Vector3(-21.5f, 0, 0);
            Instantiate(paredVertical, wallLocation, Quaternion.identity, generatedWorld.transform);
        }
    }

    private void GenerateDoorsForRoom(RoomData room, Direction? enterDirection, GeneratedRoomController roomController)
    {
        // Obtiene los prefabs
        GameObject paredDePuertaHorizontal = Resources.Load<GameObject>("GeneracionDeCuartos/Paredes/ParedDePuertaHorizontal");
        GameObject puertaHorizontal = Resources.Load<GameObject>("GeneracionDeCuartos/Puertas/PuertaHorizontal");
        GameObject paredDePuertaVertical = Resources.Load<GameObject>("GeneracionDeCuartos/Paredes/ParedDePuertaVertical");
        GameObject puertaVertical = Resources.Load<GameObject>("GeneracionDeCuartos/Puertas/PuertaVertical");

        // Verifica que existe el prefab
        if (puertaHorizontal == null || paredDePuertaHorizontal == null || puertaVertical == null || paredDePuertaVertical == null)
        {
            Debug.LogError("No se encontro el prefab de el puente");
            return;
        }

        if (room.exitDirection == Direction.North)
        {
            Vector3 wallLocation = room.gridPosition + new Vector3(0, 0, 13.5f);
            Instantiate(paredDePuertaHorizontal, wallLocation, Quaternion.identity, generatedWorld.transform);
            Vector3 doorLocation = wallLocation + new Vector3(0, 2.2f, 0);
            GameObject newDoor = Instantiate(puertaHorizontal, doorLocation, Quaternion.identity, generatedWorld.transform);
            roomController.exitDoor = newDoor;
        }
        else if (room.exitDirection == Direction.East)
        {
            Vector3 wallLocation = room.gridPosition + new Vector3(21.5f, 0, 0);
            Instantiate(paredDePuertaVertical, wallLocation, Quaternion.identity, generatedWorld.transform);
            Vector3 doorLocation = wallLocation + new Vector3(0, 2.2f, 0);
            GameObject newDoor = Instantiate(puertaVertical, doorLocation, Quaternion.identity, generatedWorld.transform);
            roomController.exitDoor = newDoor;
        }
        else if (room.exitDirection == Direction.South)
        {
            Vector3 wallLocation = room.gridPosition + new Vector3(0, 0, -13.5f);
            Instantiate(paredDePuertaHorizontal, wallLocation, Quaternion.identity, generatedWorld.transform);
            Vector3 doorLocation = wallLocation + new Vector3(0, 2.2f, 0);
            GameObject newDoor = Instantiate(puertaHorizontal, doorLocation, Quaternion.identity, generatedWorld.transform);
            roomController.exitDoor = newDoor;
        }
        else if (room.exitDirection == Direction.West)
        {
            Vector3 wallLocation = room.gridPosition + new Vector3(-21.5f, 0, 0);
            Instantiate(paredDePuertaVertical, wallLocation, Quaternion.identity, generatedWorld.transform);
            Vector3 doorLocation = wallLocation + new Vector3(0, 2.2f, 0);
            GameObject newDoor = Instantiate(puertaVertical, doorLocation, Quaternion.identity, generatedWorld.transform);
            roomController.exitDoor = newDoor;
        }

        if (enterDirection.HasValue && room.exitDirection != enterDirection.Value)
        {
            if (enterDirection.Value == Direction.North)
            {
                Vector3 wallLocation = room.gridPosition + new Vector3(0, 0, 13.5f);
                Instantiate(paredDePuertaHorizontal, wallLocation, Quaternion.identity, generatedWorld.transform);

                Vector3 doorLocation = wallLocation + new Vector3(0, 2.2f, 0);
                GameObject newDoor = Instantiate(puertaHorizontal, doorLocation, Quaternion.identity, generatedWorld.transform);
                roomController.enterDoor = newDoor;
            }
            else if (enterDirection.Value == Direction.East)
            {
                Vector3 wallLocation = room.gridPosition + new Vector3(21.5f, 0, 0);
                Instantiate(paredDePuertaVertical, wallLocation, Quaternion.identity, generatedWorld.transform);

                Vector3 doorLocation = wallLocation + new Vector3(0, 2.2f, 0);
                GameObject newDoor = Instantiate(puertaVertical, doorLocation, Quaternion.identity, generatedWorld.transform);
                roomController.enterDoor = newDoor;
            }
            else if (enterDirection.Value == Direction.South)
            {
                Vector3 wallLocation = room.gridPosition + new Vector3(0, 0, -13.5f);
                Instantiate(paredDePuertaHorizontal, wallLocation, Quaternion.identity, generatedWorld.transform);

                Vector3 doorLocation = wallLocation + new Vector3(0, 2.2f, 0);
                GameObject newDoor = Instantiate(puertaHorizontal, doorLocation, Quaternion.identity, generatedWorld.transform);
                roomController.enterDoor = newDoor;
            }
            else if (enterDirection.Value == Direction.West)
            {
                Vector3 wallLocation = room.gridPosition + new Vector3(-21.5f, 0, 0);
                Instantiate(paredDePuertaVertical, wallLocation, Quaternion.identity, generatedWorld.transform);

                Vector3 doorLocation = wallLocation + new Vector3(0, 2.2f, 0);
                GameObject newDoor = Instantiate(puertaVertical, doorLocation, Quaternion.identity, generatedWorld.transform);
                roomController.enterDoor = newDoor;
            }
        }
        
    }

    private void GenerateFinalPortal()
    {
        if (floorData == null || floorData.rooms == null || floorData.rooms.Count == 0)
        {
            Debug.LogWarning("No se puede generar el portal final porque el piso no tiene cuartos.");
            return;
        }

        RoomData finalRoom = floorData.rooms[floorData.rooms.Count - 1];
        Direction nextRoomEntryDirection = GetOppositeDirection(finalRoom.exitDirection);
        Vector3Int nextRoomPosition = finalRoom.gridPosition + GetRoomOffset(finalRoom.exitDirection);

        GameObject portalPrefab = Resources.Load<GameObject>("GeneracionDeCuartos/Portal/Portal");
        Vector3 wallLocation;
        Quaternion portalRotation = Quaternion.identity;

        switch (nextRoomEntryDirection)
        {
            case Direction.North:
                wallLocation = nextRoomPosition + new Vector3(0, 0, 13.5f);
                portalRotation = Quaternion.Euler(0, 90, 0);
                break;
            case Direction.East:
                wallLocation = nextRoomPosition + new Vector3(21.5f, 0, 0);
                break;
            case Direction.South:
                wallLocation = nextRoomPosition + new Vector3(0, 0, -13.5f);
                portalRotation = Quaternion.Euler(0, 90, 0);
                break;
            case Direction.West:
                wallLocation = nextRoomPosition + new Vector3(-21.5f, 0, 0);
                break;
            default:
                Debug.LogError("La direccion de salida del cuarto final no es valida.");
                return;
        }

        if (portalPrefab == null)
        {
            Debug.LogError("No se encontro el prefab del portal final en Resources/GeneracionDeCuartos/Portal.");
            return;
        }

        Vector3 portalLocation = wallLocation + new Vector3(0, 2.2f, 0);
        Instantiate(portalPrefab, portalLocation, portalRotation, generatedWorld.transform);
    }

    private Vector3Int GetRoomOffset(Direction direction)
    {
        switch (direction)
        {
            case Direction.North:
                return new Vector3Int(0, 0, 41);
            case Direction.East:
                return new Vector3Int(57, 0, 0);
            case Direction.South:
                return new Vector3Int(0, 0, -41);
            case Direction.West:
                return new Vector3Int(-57, 0, 0);
            default:
                return Vector3Int.zero;
        }
    }

    private Direction GetOppositeDirection(Direction direction)
    {
        switch (direction)
        {
            case Direction.North:
                return Direction.South;

            case Direction.East:
                return Direction.West;

            case Direction.South:
                return Direction.North;

            case Direction.West:
                return Direction.East;

            default:
                return direction;
        }
    }
}
