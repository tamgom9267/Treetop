using System;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

// Canal independiente de los eventos visuales 20-23 de la fase 3.
public static class MultiplayerGameplayProtocol
{
    public const byte WorldRequest = 30, WorldState = 31, EnemyFrames = 32, WorldSnapshot = 33;
    public const byte LootRequest = 40, LootState = 41, LootSnapshot = 42, Settlement = 43;
    public const int Version = 1;
    public static bool Participant(int actor)
    {
        if (!PhotonNetwork.InRoom || !PhotonNetwork.CurrentRoom.Players.TryGetValue(actor, out Player p) || p.IsInactive) return false;
        PhotonPartyService party = PhotonPartyService.Instance;
        if (party == null || !party.IsInParty) return false;
        foreach (PartyMember member in party.Members)
            if (member.ActorNumber == actor && member.Role != PartyRole.None) return true;
        return false;
    }
    public static List<int> Participants()
    {
        var result = new List<int>();
        if (PhotonNetwork.InRoom) foreach (int actor in PhotonNetwork.CurrentRoom.Players.Keys)
            if (Participant(actor)) result.Add(actor);
        return result;
    }
    public static bool Host => PhotonPartyService.Instance != null && PhotonPartyService.Instance.IsHost &&
        PhotonNetwork.IsMasterClient && PhotonNetwork.LocalPlayer.ActorNumber == PhotonPartyService.Instance.OriginalHostActorNumber;
    public static int LocalActor => PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.ActorNumber : 0;
    public static int HostActor => PhotonPartyService.Instance != null ? PhotonPartyService.Instance.OriginalHostActorNumber : 0;
    public static bool Send<T>(byte code, T message, bool reliable, int target = 0)
    {
        MultiplayerRunSession session = MultiplayerRunSession.Instance;
        if (session == null || !session.IsRunning || !PhotonNetwork.InRoom) return false;
        return PhotonNetwork.RaiseEvent(code, new object[] { Version, session.RunId, session.FloorNumber, JsonUtility.ToJson(message) },
            target > 0 ? new RaiseEventOptions { TargetActors = new[] { target } } : new RaiseEventOptions { Receivers = ReceiverGroup.Others },
            new SendOptions { Reliability = reliable });
    }
    public static bool Read<T>(EventData ev, out T message) where T : class
    {
        message = null;
        MultiplayerRunSession session = MultiplayerRunSession.Instance;
        if (session == null || !session.IsRunning || !Participant(ev.Sender) || !(ev.CustomData is object[] a) || a.Length != 4 ||
            !(a[0] is int version) || version != Version || !(a[1] is string run) || run != session.RunId ||
            !(a[2] is int floor) || floor != session.FloorNumber || !(a[3] is string json) || json.Length > 2000000) return false;
        try { message = JsonUtility.FromJson<T>(json); return message != null; }
        catch (ArgumentException) { return false; }
    }
    public static bool Finite(Vector3 p) => Finite(p.x) && Finite(p.y) && Finite(p.z);
    public static bool Finite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);
    public static bool Rotation(Quaternion q) => Finite(q.x) && Finite(q.y) && Finite(q.z) && Finite(q.w) &&
        q.x*q.x + q.y*q.y + q.z*q.z + q.w*q.w > 0.5f && q.x*q.x + q.y*q.y + q.z*q.z + q.w*q.w < 1.5f;
}
[Serializable]
public sealed class MultiplayerWorldMessage
{
    public string op, room, id, item;
    public int revision, stage, type, health, hit, animation, attack, damage;
    public float normalizedTime;
    public double time;
    public Vector3 position, velocity;
    public Quaternion rotation = Quaternion.identity;
    public bool inside;
}

// Los grupos contienen mensajes individuales que no pueden contener otros grupos.
[Serializable]
public sealed class MultiplayerWorldBatch
{
    public string op;
    public MultiplayerWorldMessage[] entries;
}
// Logica pura: una presencia vigente por actor, independientemente de sus colliders.
public sealed class MultiplayerRoomPresence
{
    private readonly Dictionary<int, double> occupants = new Dictionary<int, double>();
    public void Set(int actor, bool inside, double now) { if (inside) occupants[actor] = now; else occupants.Remove(actor); }
    public bool Contains(int actor, double now) => occupants.TryGetValue(actor, out double time) && now - time <= 1.5d;
    public bool All(IList<int> actors, double now)
    {
        if (actors.Count < 2) return false;
        foreach (int actor in actors) if (!Contains(actor, now)) return false;
        return true;
    }
}

public sealed class MultiplayerAttackWindow
{
    public readonly int Number, Damage;
    public readonly double Until;
    private readonly HashSet<string> hits = new HashSet<string>();
    public MultiplayerAttackWindow(int number, int damage, double until) { Number = number; Damage = damage; Until = until; }
    public bool TryHit(int attack, string enemy, double now) => attack == Number && now <= Until && !string.IsNullOrEmpty(enemy) && hits.Add(enemy);
}
