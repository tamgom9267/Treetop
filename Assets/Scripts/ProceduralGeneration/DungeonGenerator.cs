using UnityEngine;
using System.Collections.Generic;

public class DungeonGenerator : MonoBehaviour
{
    [SerializeField] private int seed; // Seed for the dungeon generation
    [SerializeField] private int roomCount = 6; // Number of rooms to generate
    [SerializeField] private int numberOfRoomPrefabs  = 7; // Number of room prefabs available

    private FloorData floorData; // Data for the generated floor

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        GenerateFloor();
    }

    private void GenerateFloor()
    {
        Random.InitState(seed); // Initialize the random number generator with the seed

        floorData = new FloorData(seed); // Create a new FloorData object with the seed

        Vector3Int currentPosition = Vector3Int.zero; // Start at the origin

        for (int i = 0; i < roomCount; i++)
        {
            int roomID = Random.Range(0, numberOfRoomPrefabs); // Randomly select a room
            
            Direction direction = GetRandomDirection();

            RoomData newRoom = new RoomData(
                roomID,
                currentPosition,
                direction
            );

            floorData.rooms.Add(newRoom); // Add the new room to the floor data

            currentPosition += DirectionToVector(direction); // Move to the next position based on the exit direction
        }

        PrintFloor(); // Print the generated floor data to the console
    }

    private Direction GetRandomDirection()
    {
        return (Direction)Random.Range(0, 4); // Randomly select a direction (0-3)
    }

    private Vector3Int DirectionToVector(Direction direction)
    {
        switch (direction)
        {
            case Direction.North:
                return new Vector3Int(0, 0, 1);
            case Direction.East:
                return new Vector3Int(1, 0, 0);
            case Direction.South:
                return new Vector3Int(0, 0, -1);
            case Direction.West:
                return new Vector3Int(-1, 0, 0);
            default:
                return Vector3Int.zero;
        }
    }

    private void PrintFloor()
    {
        foreach(RoomData room in floorData.rooms)
        {
            Debug.Log(
                $"Room: {room.roomID} " +
                $"Position: {room.gridPosition} " +
                $"Exit Direction: {room.exitDirection}"
            );
        }
    }
}
