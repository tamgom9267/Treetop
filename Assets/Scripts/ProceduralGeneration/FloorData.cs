using UnityEngine;
using System;
using System.Collections.Generic;

[Serializable]
public class FloorData : MonoBehaviour
{
    public int seed; // Seed for the floor generation
    public List<RoomData> rooms = new List<RoomData>(); // List of rooms in the floor

    public FloorData(int seed)
    {
        this.seed = seed; // Initialize the seed for the floor generation
    }
}
