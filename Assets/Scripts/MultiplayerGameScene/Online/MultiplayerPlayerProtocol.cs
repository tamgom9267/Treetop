using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Solo datos serializables por PUN. Los actores se identifican por EventData.Sender.</summary>
public static class MultiplayerPlayerProtocol
{
    public const byte PoseEvent = 20;
    public const byte EquipmentEvent = 21;
    public const byte AttackEvent = 22;
    public const byte RequestStateEvent = 23;
    public const int Version = 1;
    public const int MaxItems = 128;

    public struct Pose
    {
        public int sequence;
        public double time;
        public Vector3 position;
        public Quaternion rotation;
        public float speed;
        public float playbackRate;
    }

    public struct Item
    {
        public int id;
        public string type;
        public Item(int id, string type) { this.id = id; this.type = type; }
    }

    public sealed class Equipment
    {
        public Pose pose;
        public Item[] items = Array.Empty<Item>();
        public int equippedId;
    }

    public struct Attack
    {
        public Pose pose;
        public Item item;
    }

    public static object[] PackPose(string run, int floor, Pose pose)
    {
        return new object[] { Version, run, floor, pose.sequence, pose.time,
            pose.position, pose.rotation, pose.speed, pose.playbackRate };
    }

    public static object[] PackEquipment(string run, int floor, Equipment state)
    {
        object[] result = new object[12];
        Array.Copy(PackPose(run, floor, state.pose), result, 9);
        int[] ids = new int[state.items.Length];
        string[] types = new string[state.items.Length];
        for (int i = 0; i < ids.Length; i++) { ids[i] = state.items[i].id; types[i] = state.items[i].type; }
        result[9] = ids; result[10] = types; result[11] = state.equippedId;
        return result;
    }

    public static object[] PackAttack(string run, int floor, Attack attack)
    {
        object[] result = new object[11];
        Array.Copy(PackPose(run, floor, attack.pose), result, 9);
        result[9] = attack.item.id; result[10] = attack.item.type ?? string.Empty;
        return result;
    }

    public static object[] PackRequest(string run, int floor, int sequence, double time)
    {
        return new object[] { Version, run, floor, sequence, time };
    }

    public static bool TryReadHeader(object data, string run, int floor, int length, out object[] values)
    {
        values = data as object[];
        return values != null && values.Length == length && values[0] is int version && version == Version &&
            values[1] is string id && id == run && !string.IsNullOrEmpty(run) &&
            values[2] is int number && number == floor && floor > 0 &&
            values[3] is int sequence && sequence > 0 && values[4] is double time && Finite(time);
    }

    public static bool TryReadPose(object[] values, out Pose pose)
    {
        pose = default;
        if (values == null || values.Length < 9 || !(values[3] is int sequence) ||
            !(values[4] is double time) || !(values[5] is Vector3 position) ||
            !(values[6] is Quaternion rotation) || !(values[7] is float speed) ||
            !(values[8] is float rate) || !Finite(time) ||
            !Finite(position.x) || !Finite(position.y) || !Finite(position.z) ||
            !Finite(rotation.x) || !Finite(rotation.y) || !Finite(rotation.z) || !Finite(rotation.w) ||
            !Finite(speed) || speed < 0f || speed > 2f || !Finite(rate) || rate < 0f || rate > 10f) return false;
        float norm = rotation.x * rotation.x + rotation.y * rotation.y + rotation.z * rotation.z + rotation.w * rotation.w;
        if (norm < 0.5f || norm > 1.5f) return false;
        pose = new Pose { sequence = sequence, time = time, position = position,
            rotation = rotation.normalized, speed = speed, playbackRate = rate };
        return true;
    }

    public static bool TryReadEquipment(object[] values, out Equipment state)
    {
        state = null;
        if (values == null || values.Length != 12 || !TryReadPose(values, out Pose pose) ||
            !(values[9] is int[] ids) || !(values[10] is string[] types) ||
            ids.Length != types.Length || ids.Length > MaxItems || !(values[11] is int equipped) || equipped < 0) return false;
        Item[] items = new Item[ids.Length];
        HashSet<int> unique = new HashSet<int>();
        for (int i = 0; i < ids.Length; i++)
        {
            if (ids[i] <= 0 || !ValidType(types[i]) || !unique.Add(ids[i])) return false;
            items[i] = new Item(ids[i], types[i]);
        }
        if (equipped != 0 && !unique.Contains(equipped)) return false;
        state = new Equipment { pose = pose, items = items, equippedId = equipped };
        return true;
    }

    public static bool TryReadAttack(object[] values, out Attack attack)
    {
        attack = default;
        if (values == null || values.Length != 11 || !TryReadPose(values, out Pose pose) ||
            !(values[9] is int id) || id < 0 || !(values[10] is string type) ||
            (id == 0 ? type.Length != 0 : !ValidType(type))) return false;
        attack = new Attack { pose = pose, item = new Item(id, type) };
        return true;
    }

    public static bool IsNewer(int sequence, int previous) => sequence > previous;
    private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    private static bool ValidType(string type) => !string.IsNullOrEmpty(type) && type.Length <= 64;
}

[Serializable]
public sealed class MultiplayerVisualDefinition
{
    public string typeId;
    public WeaponData localWeapon;
    public MultiplayerWeaponVisual remotePrefab;
}
