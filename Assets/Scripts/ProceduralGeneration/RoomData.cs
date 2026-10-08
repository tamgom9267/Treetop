using UnityEngine;
using System;

[Serializable]
public class RoomData : MonoBehaviour
{
    public int roomID; // Name of the prefab
    public Vector3Int gridPosition; // Position of the room
    public Direction exitDirection; // Where the exit of the room is located

    public RoomData(int id, Vector3Int position, Direction direction)
    {
        roomID = id;
        gridPosition = position;
        exitDirection = direction;
    }



}
