using System.Collections.Generic;
using UnityEngine;

/// <summary>Solo el host lo invoca. No escribe floorData.json ni altera el estado aleatorio del combate.</summary>
public class HostDungeonInstructionsGenerator : MonoBehaviour
{
    [SerializeField, Range(2, 10)] private int roomCount = 6;

    public bool TryGenerate(int baseSeed, int floor, string runId, out MultiplayerFloorData data, out string error)
    {
        data = null;
        error = "La seed o el numero de piso esta fuera del rango permitido.";
        if (!MultiplayerMapMath.TryFinalSeed(baseSeed, floor, out int finalSeed)) return false;
        Random.State previousRandomState = Random.state;
        try
        {
            Random.InitState(finalSeed);
            data = new MultiplayerFloorData { runId = runId, baseSeed = baseSeed, floorNumber = floor, finalSeed = finalSeed };
            var available = new List<int>();
            for (int id = 1; id <= 8; id++) available.Add(id);
            var occupied = new HashSet<Vector3Int> { Vector3Int.zero };
            Vector3Int position = Vector3Int.zero;
            for (int index = 0; index < roomCount; index++)
            {
                string id;
                int enemy = 0;
                if (index == 0) id = "0";
                else if (index == roomCount - 1) id = Random.Range(0, 2) == 0 ? "boss1" : "boss2";
                else
                {
                    int selected = Random.Range(0, available.Count);
                    int room = available[selected];
                    available.RemoveAt(selected);
                    id = room.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    if (room > 1) enemy = Random.Range(1, 3);
                }

                MultiplayerDirection exit = (MultiplayerDirection)Random.Range(0, 4);
                if (occupied.Contains(position + MultiplayerMapMath.Offset(exit)))
                    exit = MultiplayerMapMath.Opposite(exit);
                if (occupied.Contains(position + MultiplayerMapMath.Offset(exit)))
                {
                    var free = new List<MultiplayerDirection>();
                    for (int direction = 0; direction < 4; direction++)
                        if (!occupied.Contains(position + MultiplayerMapMath.Offset((MultiplayerDirection)direction)))
                            free.Add((MultiplayerDirection)direction);
                    if (free.Count == 0) { error = "No se encontro una salida libre para el piso."; return false; }
                    exit = free[Random.Range(0, free.Count)];
                }
                data.rooms.Add(new MultiplayerRoomData { roomID = id, gridPosition = position, exitDirection = exit, enemyType = enemy });
                position += MultiplayerMapMath.Offset(exit);
                occupied.Add(position);
            }
            return MultiplayerMapMath.Validate(data, out error);
        }
        finally { Random.state = previousRandomState; }
    }
}
