using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

public enum MultiplayerDirection { North, East, South, West }
public enum MultiplayerRunPhase { Idle, LoadingScene, BuildingFloor, Playing, Ending, Completed }

[Serializable]
public sealed class MultiplayerRoomData
{
    public string roomID;
    public Vector3Int gridPosition;
    public MultiplayerDirection exitDirection;
    public int enemyType;
}

[Serializable]
public sealed class MultiplayerFloorData
{
    public string runId;
    public int baseSeed;
    public int floorNumber;
    public int finalSeed;
    public List<MultiplayerRoomData> rooms = new List<MultiplayerRoomData>();
}

/// <summary>Geometria y validacion del protocolo multijugador, independiente del solitario.</summary>
public static class MultiplayerMapMath
{
    public static Vector3Int Offset(MultiplayerDirection direction)
    {
        switch (direction)
        {
            case MultiplayerDirection.North: return new Vector3Int(0, 0, 41);
            case MultiplayerDirection.East: return new Vector3Int(57, 0, 0);
            case MultiplayerDirection.South: return new Vector3Int(0, 0, -41);
            case MultiplayerDirection.West: return new Vector3Int(-57, 0, 0);
            default: throw new ArgumentOutOfRangeException(nameof(direction));
        }
    }

    public static MultiplayerDirection Opposite(MultiplayerDirection direction) =>
        (MultiplayerDirection)(((int)direction + 2) % 4);

    public static bool IsHorizontalWall(MultiplayerDirection direction) =>
        direction == MultiplayerDirection.North || direction == MultiplayerDirection.South;

    public static Vector3 WallOffset(MultiplayerDirection direction)
    {
        Vector3 directionVector = Offset(direction);
        return IsHorizontalWall(direction) ? directionVector.normalized * 13.5f : directionVector.normalized * 21.5f;
    }

    public static bool TryFinalSeed(int baseSeed, int floor, out int finalSeed)
    {
        finalSeed = 0;
        return RunSeedSettings.IsValid(baseSeed) && floor > 0 && int.TryParse(
            floor.ToString(CultureInfo.InvariantCulture) + baseSeed.ToString(CultureInfo.InvariantCulture),
            NumberStyles.None, CultureInfo.InvariantCulture, out finalSeed);
    }

    public static string Key(string runId, int floor) => runId + ":" + floor.ToString(CultureInfo.InvariantCulture);

    public static bool Validate(MultiplayerFloorData data, out string error)
    {
        error = "Las instrucciones del piso no son validas.";
        if (data == null || !Guid.TryParse(data.runId, out _) ||
            !TryFinalSeed(data.baseSeed, data.floorNumber, out int finalSeed) || finalSeed != data.finalSeed ||
            data.rooms == null || data.rooms.Count < 2 || data.rooms.Count > 10) return false;

        var occupied = new HashSet<Vector3Int>();
        var normalIDs = new HashSet<string>();
        for (int index = 0; index < data.rooms.Count; index++)
        {
            MultiplayerRoomData room = data.rooms[index];
            if (room == null || (int)room.exitDirection < 0 || (int)room.exitDirection > 3 ||
                room.enemyType < 0 || room.enemyType > 2 || !occupied.Add(room.gridPosition)) return false;
            if (index == 0)
            {
                if (room.roomID != "0" || room.gridPosition != Vector3Int.zero || room.enemyType != 0) return false;
            }
            else if (index == data.rooms.Count - 1)
            {
                if ((room.roomID != "boss1" && room.roomID != "boss2") || room.enemyType != 0) return false;
            }
            else if (!int.TryParse(room.roomID, out int id) || id < 1 || id > 8 ||
                     room.roomID != id.ToString(CultureInfo.InvariantCulture) || !normalIDs.Add(room.roomID)) return false;

            if (index > 0 && room.gridPosition != data.rooms[index - 1].gridPosition +
                Offset(data.rooms[index - 1].exitDirection)) return false;
        }
        MultiplayerRoomData last = data.rooms[data.rooms.Count - 1];
        if (occupied.Contains(last.gridPosition + Offset(last.exitDirection))) return false;
        error = string.Empty;
        return true;
    }

    public static bool TryReadJson(string json, out MultiplayerFloorData data, out string error)
    {
        data = null;
        error = "No se recibieron instrucciones del piso.";
        if (string.IsNullOrEmpty(json)) return false;
        try { data = JsonUtility.FromJson<MultiplayerFloorData>(json); }
        catch (ArgumentException) { error = "El JSON del piso esta incompleto o tiene un formato invalido."; return false; }
        return Validate(data, out error);
    }
}
