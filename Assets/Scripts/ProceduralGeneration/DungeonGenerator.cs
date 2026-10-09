using UnityEngine;
using System.Collections.Generic;
using System.IO;

public class DungeonGenerator : MonoBehaviour
{
    [SerializeField] private int seed; // Seed for the dungeon generation
    [SerializeField] private int roomCount = 6; // Number of rooms to generate
    [SerializeField] private int numberOfRoomPrefabs  = 7; // Number of room prefabs available

    string[] bossRooms = { "boss1", "boss2", "boss3" }; // List of boss room IDs

    HashSet<Vector3Int> occupiedPositions = new HashSet<Vector3Int>(); // To keep track of occupied positions in the grid

    private FloorData floorData; // Data for the generated floor

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        GenerateFloor();
    }

    private void GenerateFloor()
    {
        Random.InitState(seed); // Initialize the random number generator with the seed

        occupiedPositions.Clear(); // Clear the set of occupied positions for a new floor generation
        occupiedPositions.Add(Vector3Int.zero); // Mark the starting position as occupied

        floorData = new FloorData(seed); // Create a new FloorData object with the seed

        Vector3Int currentPosition = Vector3Int.zero; // Start at the origin

        for (int i = 0; i < roomCount; i++)
        {
            string roomID; // Randomly select a room
            int enemyType = 0; // Default enemy type (0 means no enemy)

            if (i == 0)
            {
                roomID = "0"; // First room is always the starting room
            }
            else if (i == roomCount - 1) // Spawn boss room at the end of the floor
            {
                int randomBossIndex = Random.Range(0, bossRooms.Length); // Randomly select a boss room
                roomID = bossRooms[randomBossIndex];
            }
            else
            {
                int randomRoomID = Random.Range(1, numberOfRoomPrefabs - 1); // Randomly select a room ID for intermediate rooms
                roomID = randomRoomID.ToString();

                if (randomRoomID > 1) // Room ID 1 is for the store/rest point so no enemies should be placed there
                {
                    enemyType = Random.Range(1, 4); // Randomly select an enemy type (1-3) for rooms with enemies
                }
            }


            Direction direction = GetRandomDirection(currentPosition);

            RoomData newRoom = new RoomData(
                roomID,
                currentPosition,
                direction,
                enemyType
            );

            floorData.rooms.Add(newRoom); // Add the new room to the floor data

            currentPosition += DirectionToVector(direction); // Move to the next position based on the exit direction
        }

        PrintFloor(); // Print the generated floor data to the console
        TurnToJSON(); // Convert the floor data to JSON and print it to the console
    }

    private Direction GetRandomDirection(Vector3Int currentPosition) // Randomly select a direction for the room exit that won't repeat
    {

        Direction direction = (Direction)Random.Range(0, 4);
        
        Vector3Int nextPosition = currentPosition + DirectionToVector(direction);

        if (occupiedPositions.Contains(nextPosition))
        {
            direction = GetOppositeDirection(direction);

            nextPosition = currentPosition + DirectionToVector(direction);
            
        }

        occupiedPositions.Add(nextPosition); // Add the new position to the occupied positions set to prevent future overlaps

        return direction; // Return the selected direction
    }

    private Vector3Int DirectionToVector(Direction direction) // Convert a direction to a vector for grid movement
    {
        switch (direction)
        {
            case Direction.North:
                return new Vector3Int(0, 0, 57);
            case Direction.East:
                return new Vector3Int(41, 0, 0);
            case Direction.South:
                return new Vector3Int(0, 0, -57);
            case Direction.West:
                return new Vector3Int(-41, 0, 0);
            default:
                return Vector3Int.zero;
        }
    }

    private Direction GetOppositeDirection(Direction direction) // Get opposite direction to use when the random direction gives a 
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

    private void PrintFloor()
    {
        foreach(RoomData room in floorData.rooms)
        {
            Debug.Log(
                $"Room: {room.roomID} | " +
                $"Position: {room.gridPosition} | " +
                $"Exit Direction: {room.exitDirection} | " +
                $"Enemy Type: {room.enemyType}"
            );
        }
    }

    private void TurnToJSON()
    {
        string json = JsonUtility.ToJson(floorData, true);

        string path = Path.Combine(
            Application.persistentDataPath,
            "floorData.json"
        );

        File.WriteAllText(path, json);

        Debug.Log("Floor JSON saved to: " + path);
    }
}
