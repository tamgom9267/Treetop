using System;
using System.Collections.Generic;
using UnityEngine;

public enum MultiplayerItemLocation { World, Reserved, Inventory, Removed }
[Serializable]
public sealed class MultiplayerItemRow { public int[] cells; }
[Serializable]
public sealed class MultiplayerItemData
{
    public string name, objectName;
    public int weight, health, mp, defense, wd, pd, hb, speed;
    public MultiplayerItemRow[] shape;
    public static MultiplayerItemData Capture(InventoryObject item)
    {
        var data = new MultiplayerItemData { name = item.Name, objectName = item.Objectname, weight = item.weight,
            shape = new MultiplayerItemRow[item.requieredCells.Count] };
        for (int i = 0; i < data.shape.Length; i++) data.shape[i] = new MultiplayerItemRow { cells = item.requieredCells[i].ToArray() };
        if (item is WeaponObject w)
        { data.health = w.health; data.mp = w.MP; data.defense = w.defense; data.wd = w.WD; data.pd = w.PD; data.hb = w.HB; data.speed = w.speed; }
        return data;
    }
    public bool Valid()
    {
        if (shape == null || shape.Length < 1 || shape.Length > 16 || weight < 0 || weight > 10000) return false;
        bool occupied = false;
        foreach (var row in shape)
        {
            if (row == null || row.cells == null || row.cells.Length < 1 || row.cells.Length > 16) return false;
            foreach (int cell in row.cells) { if (cell < 0 || cell > 1) return false; occupied |= cell == 1; }
        }
        return occupied;
    }
    public void Apply(InventoryObject item)
    {
        item.Name = name; item.Objectname = objectName; item.weight = weight;
        item.requieredCells.Clear(); foreach (var row in shape) item.requieredCells.Add(new List<int>(row.cells));
        if (item is WeaponObject w)
        { w.health = health; w.MP = mp; w.defense = defense; w.WD = wd; w.PD = pd; w.HB = hb; w.speed = speed; }
    }
}
[Serializable]
public sealed class MultiplayerLootRecord
{
    public string id, type;
    public int revision, owner, floor;
    public MultiplayerItemLocation location;
    public Vector3 position;
    public Quaternion rotation = Quaternion.identity;
    public MultiplayerItemData data;
    public MultiplayerLootRecord Copy() => JsonUtility.FromJson<MultiplayerLootRecord>(JsonUtility.ToJson(this));
}
[Serializable]
public sealed class MultiplayerLootMessage
{
    public string op, request, barrier;
    public int actor;
    public MultiplayerLootRecord record;
    public MultiplayerLootRecord[] records;
}
[Serializable]
public sealed class MultiplayerLootDefinition
{
    public string type;
    public GameObject prefab;
    public WeaponData weapon;
    public string objectName;
    public Sprite icon;
}
// Transacciones serializadas por el host. No depende de objetos de escena ni del ID de la UI.
public sealed class MultiplayerLootLedger
{
    public readonly Dictionary<string, MultiplayerLootRecord> Records = new Dictionary<string, MultiplayerLootRecord>();
    private readonly HashSet<string> processed = new HashSet<string>();
    private int revision;
    public MultiplayerLootRecord Process(int actor, MultiplayerLootMessage message, int floor)
    {
        var incoming = message.record;
        if (incoming == null || string.IsNullOrEmpty(incoming.id) || incoming.id.Length > 128 ||
            string.IsNullOrEmpty(message.request) || message.request.Length > 128) return null;
        Records.TryGetValue(incoming.id, out var current);
        if (!processed.Add(actor + ":" + message.request)) return current;
        if (message.op == "register")
        {
            if (current != null) return current;
            if (incoming.data == null || !incoming.data.Valid() || string.IsNullOrEmpty(incoming.type) || incoming.type.Length > 128 ||
                !MultiplayerGameplayProtocol.Finite(incoming.position) || !MultiplayerGameplayProtocol.Rotation(incoming.rotation)) return null;
            current = incoming.Copy(); current.owner = incoming.location == MultiplayerItemLocation.World ? 0 : actor;
            current.location = current.owner == 0 ? MultiplayerItemLocation.World : MultiplayerItemLocation.Inventory;
            current.floor = floor; current.revision = ++revision; Records.Add(current.id, current);
            return current;
        }
        if (current == null || current.revision != incoming.revision) return current;
        if (message.op == "claim" && current.location == MultiplayerItemLocation.World && current.floor == floor)
        { current.location = MultiplayerItemLocation.Reserved; current.owner = actor; }
        else if (message.op == "store" && current.location == MultiplayerItemLocation.Reserved && current.owner == actor)
            current.location = MultiplayerItemLocation.Inventory;
        else if (message.op == "drop" && current.owner == actor &&
            (current.location == MultiplayerItemLocation.Inventory || current.location == MultiplayerItemLocation.Reserved) &&
            MultiplayerGameplayProtocol.Finite(incoming.position) && MultiplayerGameplayProtocol.Rotation(incoming.rotation) && incoming.data != null && incoming.data.Valid())
        {
            current.location = MultiplayerItemLocation.World; current.owner = 0; current.floor = floor;
            current.position = incoming.position; current.rotation = incoming.rotation; current.data = incoming.data;
        }
        else return current;
        current.revision = ++revision;
        return current;
    }
    public void NewFloor(int floor)
    {
        foreach (var item in Records.Values)
            if (item.location == MultiplayerItemLocation.World || item.location == MultiplayerItemLocation.Reserved)
            { item.location = MultiplayerItemLocation.Removed; item.owner = 0; item.revision = ++revision; }
    }
    public void Release(int actor, Vector3 position, int floor)
    {
        foreach (var item in Records.Values)
            if (item.owner == actor && item.location == MultiplayerItemLocation.Reserved)
            { item.owner = 0; item.location = MultiplayerItemLocation.World; item.position = position; item.floor = floor; item.revision = ++revision; }
    }
}
