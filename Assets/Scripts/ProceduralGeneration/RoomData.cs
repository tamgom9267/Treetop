using UnityEngine;
using System;

[Serializable]
public class RoomData
{
    public string roomID; // Name of the prefab
    public Vector3Int gridPosition; // Position of the room
    public Direction exitDirection; // Where the exit of the room is located

    public int enemyType; // Type of enemy in the room

    public RoomData(string id, Vector3Int position, Direction direction, int enemyType)
    {
        roomID = id;
        gridPosition = position;
        exitDirection = direction;
        this.enemyType = enemyType;
    }



}
