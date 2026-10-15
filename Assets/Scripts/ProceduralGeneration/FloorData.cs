using UnityEngine;
using System;
using System.Collections.Generic;

[Serializable]
public class FloorData
{
    public int finalSeed;
    public List<RoomData> rooms = new List<RoomData>();

    public FloorData(int finalSeed)
    {
        this.finalSeed = finalSeed;
    }
}
