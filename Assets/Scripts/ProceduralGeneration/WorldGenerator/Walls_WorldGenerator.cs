using UnityEngine;

public partial class WorldGenerator : MonoBehaviour
{
    private void GenerateWalls()
    {
        // Obtiene los prefabs
        GameObject paredHorizontal = Resources.Load<GameObject>("GeneracionDeCuartos/Paredes/ParedHorizontal");
        GameObject paredVertical = Resources.Load<GameObject>("GeneracionDeCuartos/Paredes/ParedVertical");

        // Verifica que existe el prefab
        if (paredHorizontal == null || paredVertical == null)
        {
            Debug.LogError("No se encontro el prefab de la pared");
        }

        Direction? previousExitDirection = null;

        foreach (RoomData room in floorData.rooms)
        {
            // Si existe una habitacion anterior, su salida opuesta sera la entrada de la habitacion actual. En la primera habitacion no hay salida anterior, por eso enterDirection sera null.
            Direction? enterDirection = previousExitDirection.HasValue? GetOppositeDirection(previousExitDirection.Value): null;
            // Guarda la salida de esta habitacion. Se usara como referencia para calcular el enter de la siguiente habitacion.
            previousExitDirection = room.exitDirection;

            if (room.exitDirection != Direction.North && enterDirection != Direction.North)
            {
                Vector3 wallLocation = room.gridPosition + new Vector3(0, 0, 13.5f);
                GameObject newWall = Instantiate(paredHorizontal, wallLocation, Quaternion.identity, generatedWorld.transform);
            }
            if (room.exitDirection != Direction.East && enterDirection != Direction.East)
            {
                Vector3 wallLocation = room.gridPosition + new Vector3(21.5f, 0, 0);
                GameObject newWall = Instantiate(paredVertical, wallLocation, Quaternion.identity, generatedWorld.transform);
            }
            if (room.exitDirection != Direction.South && enterDirection != Direction.South)
            {
                Vector3 wallLocation = room.gridPosition + new Vector3(0, 0, -13.5f);
                GameObject newWall = Instantiate(paredHorizontal, wallLocation, Quaternion.identity, generatedWorld.transform);
            }
            if (room.exitDirection != Direction.West && enterDirection != Direction.West)
            {
                Vector3 wallLocation = room.gridPosition + new Vector3(-21.5f, 0, 0);
                GameObject newWall = Instantiate(paredVertical, wallLocation, Quaternion.identity, generatedWorld.transform);
            }
        }
    }

    private void GenerateDoors()
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
        }
        
        Direction? previousExitDirection = null;

        foreach (RoomData room in floorData.rooms)
        {
            // Si existe una habitacion anterior, su salida opuesta sera la entrada de la habitacion actual. En la primera habitacion no hay salida anterior, por eso enterDirection sera null.
            Direction? enterDirection = previousExitDirection.HasValue? GetOppositeDirection(previousExitDirection.Value): null;
            // Guarda la salida de esta habitacion. Se usara como referencia para calcular el enter de la siguiente habitacion.
            previousExitDirection = room.exitDirection;

            if (room.exitDirection == Direction.North)
            {
                Vector3 wallLocation = room.gridPosition + new Vector3(0, 0, 13.5f);
                GameObject newWallForDoor = Instantiate(paredDePuertaHorizontal, wallLocation, Quaternion.identity, generatedWorld.transform);
                Vector3 doorLocation = wallLocation + new Vector3(0, 2.2f, 0);
                GameObject newDoor = Instantiate(puertaHorizontal, doorLocation, Quaternion.identity, generatedWorld.transform);
            }
            else if (room.exitDirection == Direction.East)
            {
                Vector3 wallLocation = room.gridPosition + new Vector3(21.5f, 0, 0);
                GameObject newWallForDoor = Instantiate(paredDePuertaVertical, wallLocation, Quaternion.identity, generatedWorld.transform);
                Vector3 doorLocation = wallLocation + new Vector3(0, 2.2f, 0);
                GameObject newDoor = Instantiate(puertaVertical, doorLocation, Quaternion.identity, generatedWorld.transform);
            }
            else if (room.exitDirection == Direction.South)
            {
                Vector3 wallLocation = room.gridPosition + new Vector3(0, 0, -13.5f);
                GameObject newWallForDoor = Instantiate(paredDePuertaHorizontal, wallLocation, Quaternion.identity, generatedWorld.transform);
                Vector3 doorLocation = wallLocation + new Vector3(0, 2.2f, 0);
                GameObject newDoor = Instantiate(puertaHorizontal, doorLocation, Quaternion.identity, generatedWorld.transform);
            }
            else if (room.exitDirection == Direction.West)
            {
                Vector3 wallLocation = room.gridPosition + new Vector3(-21.5f, 0, 0);
                GameObject newWallForDoor = Instantiate(paredDePuertaVertical, wallLocation, Quaternion.identity, generatedWorld.transform);
                Vector3 doorLocation = wallLocation + new Vector3(0, 2.2f, 0);
                GameObject newDoor = Instantiate(puertaVertical, doorLocation, Quaternion.identity, generatedWorld.transform);
            }

            if (enterDirection.HasValue && room.exitDirection != enterDirection.Value)
            {
                if (enterDirection.Value == Direction.North)
                {
                    Vector3 wallLocation = room.gridPosition + new Vector3(0, 0, 13.5f);
                    Instantiate(paredDePuertaHorizontal, wallLocation, Quaternion.identity, generatedWorld.transform);

                    Vector3 doorLocation = wallLocation + new Vector3(0, 2.2f, 0);
                    Instantiate(puertaHorizontal, doorLocation, Quaternion.identity, generatedWorld.transform);
                }
                else if (enterDirection.Value == Direction.East)
                {
                    Vector3 wallLocation = room.gridPosition + new Vector3(21.5f, 0, 0);
                    Instantiate(paredDePuertaVertical, wallLocation, Quaternion.identity, generatedWorld.transform);

                    Vector3 doorLocation = wallLocation + new Vector3(0, 2.2f, 0);
                    Instantiate(puertaVertical, doorLocation, Quaternion.identity, generatedWorld.transform);
                }
                else if (enterDirection.Value == Direction.South)
                {
                    Vector3 wallLocation = room.gridPosition + new Vector3(0, 0, -13.5f);
                    Instantiate(paredDePuertaHorizontal, wallLocation, Quaternion.identity, generatedWorld.transform);

                    Vector3 doorLocation = wallLocation + new Vector3(0, 2.2f, 0);
                    Instantiate(puertaHorizontal, doorLocation, Quaternion.identity, generatedWorld.transform);
                }
                else if (enterDirection.Value == Direction.West)
                {
                    Vector3 wallLocation = room.gridPosition + new Vector3(-21.5f, 0, 0);
                    Instantiate(paredDePuertaVertical, wallLocation, Quaternion.identity, generatedWorld.transform);

                    Vector3 doorLocation = wallLocation + new Vector3(0, 2.2f, 0);
                    Instantiate(puertaVertical, doorLocation, Quaternion.identity, generatedWorld.transform);
                }
            }
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
