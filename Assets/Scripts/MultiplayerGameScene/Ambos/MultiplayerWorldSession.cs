using System;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

[RequireComponent(typeof(MultiplayerLootSession), typeof(MultiplayerInventoryAdapter))]
public sealed class MultiplayerWorldSession : MonoBehaviour, IOnEventCallback
{
    public static MultiplayerWorldSession Instance { get; private set; }
    private readonly Dictionary<string, MultiplayerRoomController> rooms = new Dictionary<string, MultiplayerRoomController>();
    private readonly Dictionary<string, MultiplayerRoomPresence> presence = new Dictionary<string, MultiplayerRoomPresence>();
    private readonly Dictionary<string, MultiplayerWorldMessage> roomStates = new Dictionary<string, MultiplayerWorldMessage>();
    private readonly Dictionary<string, MultiplayerEnemy> enemies = new Dictionary<string, MultiplayerEnemy>();
    private readonly HashSet<string> removed = new HashSet<string>();
    private readonly Dictionary<int, Vector3> positions = new Dictionary<int, Vector3>();
    private readonly Dictionary<int, double> poseTimes = new Dictionary<int, double>();
    private readonly Dictionary<int, MultiplayerAttackWindow> attacks = new Dictionary<int, MultiplayerAttackWindow>();
    private MultiplayerRunSession session;
    private MultiplayerPlayers players;
    private PlayerController player;
    private string floorKey;
    private int revision, localAttack;
    private double nextPresence, nextFrames, nextSnapshot;
    private bool suspended = true;
    public MultiplayerLootSession Loot { get; private set; }
    public MultiplayerInventoryAdapter Inventory { get; private set; }
    public bool Playing => !suspended && session != null && session.IsPlaying && !Loot.Settling;
    public bool Initialize(MultiplayerRunSession run, MultiplayerPlayers sync, out string error)
    {
        Instance = this; session = run; players = sync;
        Loot = GetComponent<MultiplayerLootSession>(); Inventory = GetComponent<MultiplayerInventoryAdapter>();
        if (!Inventory.Validate(out error) || !Loot.Initialize(this, out error)) return false;
        return true;
    }
    private void OnEnable() { PhotonNetwork.AddCallbackTarget(this); }
    private void OnDisable() { PhotonNetwork.RemoveCallbackTarget(this); Shutdown(); }
    public void BeginFloor(string run, int floor)
    {
        Suspend();
        string key = MultiplayerMapMath.Key(run, floor);
        if (floorKey == key) return;
        floorKey = key; rooms.Clear(); presence.Clear(); roomStates.Clear(); removed.Clear(); attacks.Clear();
        enemies.Clear(); positions.Clear(); poseTimes.Clear(); nextPresence = nextFrames = nextSnapshot = 0;
        Loot.BeginFloor(floor);
    }
    public bool Bind(PlayerController local, out string error)
    {
        if (!Inventory.Bind(this, local, out error)) return false;
        if (player != local)
        {
            if (player != null) player.AttackStarted -= StartAttack;
            player = local; player.AttackStarted += StartAttack;
        }
        Loot.Bind(local);
        return true;
    }
    public void Suspend() { suspended = true; attacks.Clear(); }
    public void Resume() { suspended = false; Loot.Resume(); nextPresence = nextSnapshot = 0; }
    public void Shutdown()
    {
        suspended = true;
        if (player != null) player.AttackStarted -= StartAttack;
        if (Loot != null) Loot.Shutdown();
        if (Inventory != null) Inventory.Shutdown();
        player = null;
        if (Instance == this) Instance = null;
    }
    public void PublishEquipment() { if (players != null) players.PublishLocalEquipment(); }
    public void RegisterRoom(MultiplayerRoomController room)
    {
        rooms[room.RoomId] = room;
        if (!presence.ContainsKey(room.RoomId)) presence[room.RoomId] = new MultiplayerRoomPresence();
        if (roomStates.TryGetValue(room.RoomId, out var state)) room.ApplyState(state.stage, state.revision);
    }
    public void ObservePose(int actor, Vector3 position)
    {
        positions[actor] = position; poseTimes[actor] = Time.realtimeSinceStartupAsDouble;
    }
    public bool TryPosition(int actor, out Vector3 position)
    {
        if (actor == MultiplayerGameplayProtocol.LocalActor && player != null)
        { position = player.transform.position; return true; }
        position = default;
        return poseTimes.TryGetValue(actor, out double time) && Time.realtimeSinceStartupAsDouble - time < 1.5d &&
            positions.TryGetValue(actor, out position);
    }
    public bool TryLastPosition(int actor, out Vector3 position) => positions.TryGetValue(actor, out position);
    public bool TryNearest(string roomId, Vector3 origin, out Vector3 target)
    {
        target = default; float best = float.PositiveInfinity;
        if (!presence.TryGetValue(roomId, out var occupants)) return false;
        foreach (int actor in MultiplayerGameplayProtocol.Participants())
            if (occupants.Contains(actor, Time.realtimeSinceStartupAsDouble) && TryPosition(actor, out Vector3 position))
            {
                float distance = (origin - position).sqrMagnitude;
                if (distance < best) { best = distance; target = position; }
            }
        return !float.IsPositiveInfinity(best);
    }
    private void Update()
    {
        if (!Playing || player == null) return;
        double now = Time.realtimeSinceStartupAsDouble;
        if (now >= nextPresence)
        {
            nextPresence = now + 0.25d;
            foreach (var pair in rooms)
            {
                var message = new MultiplayerWorldMessage { op = "presence", room = pair.Key,
                    inside = pair.Value.ContainsLocal(player), position = player.transform.position };
                if (MultiplayerGameplayProtocol.Host) ReceiveRequest(MultiplayerGameplayProtocol.LocalActor, message);
                else MultiplayerGameplayProtocol.Send(MultiplayerGameplayProtocol.WorldRequest, message, true, MultiplayerGameplayProtocol.HostActor);
            }
        }
        if (!MultiplayerGameplayProtocol.Host)
        {
            if (now >= nextSnapshot)
            {
                nextSnapshot = now + 3d;
                MultiplayerGameplayProtocol.Send(MultiplayerGameplayProtocol.WorldRequest,
                    new MultiplayerWorldMessage { op = "snapshot" }, true, MultiplayerGameplayProtocol.HostActor);
            }
            return;
        }
        var actors = MultiplayerGameplayProtocol.Participants();
        foreach (var pair in rooms)
        {
            MultiplayerRoomController room = pair.Value;
            if (room.Stage == 0 && presence[pair.Key].All(actors, now)) Activate(room);
            if (room.Stage == 1)
            {
                bool alive = false;
                foreach (MultiplayerEnemy enemy in enemies.Values) if (enemy != null && enemy.RoomId == pair.Key) { alive = true; break; }
                if (!alive) CommitRoom(room, 2);
            }
        }
        if (now >= nextFrames)
        {
            nextFrames = now + 1d / 15d;
            var frames = new List<MultiplayerWorldMessage>();
            foreach (MultiplayerEnemy enemy in enemies.Values) if (enemy != null) frames.Add(enemy.Capture(++revision));
            if (frames.Count > 0) MultiplayerGameplayProtocol.Send(MultiplayerGameplayProtocol.EnemyFrames,
                new MultiplayerWorldMessage { op = "frames", entries = frames.ToArray() }, false);
        }
    }
    private void Activate(MultiplayerRoomController room)
    {
        CommitRoom(room, 1);
        if (room.enemyType == 0) return;
        for (int i = 0; i < room.spawners.Length; i++)
        {
            GameObject point = room.spawners[i]; if (point == null) continue;
            string id = room.RoomId + ":" + i;
            var state = new MultiplayerWorldMessage { op = "enemy", room = room.RoomId, id = id, type = room.enemyType,
                revision = ++revision, position = point.transform.position, rotation = point.transform.rotation, health = -1 };
            MultiplayerEnemy enemy = CreateEnemy(state);
            if (enemy == null) { session.ReportBuildFailed("Falta el prefab de enemigo Host " + room.enemyType); return; }
            state = enemy.Capture(++revision);
            MultiplayerGameplayProtocol.Send(MultiplayerGameplayProtocol.WorldState, state, true);
        }
    }
    private void CommitRoom(MultiplayerRoomController room, int stage)
    {
        var message = new MultiplayerWorldMessage { op = "room", room = room.RoomId, stage = stage, revision = ++revision };
        Apply(message); MultiplayerGameplayProtocol.Send(MultiplayerGameplayProtocol.WorldState, message, true);
    }
    private MultiplayerEnemy CreateEnemy(MultiplayerWorldMessage state)
    {
        if (!rooms.TryGetValue(state.room ?? "", out var room) || state.type < 1 || state.type > 2) return null;
        string role = MultiplayerGameplayProtocol.Host ? "Host" : "Client";
        GameObject prefab = Resources.Load<GameObject>("Multiplayer/" + role + "/Enemigos/Enemigo" + state.type);
        if (prefab == null) return null;
        GameObject copy = Instantiate(prefab, state.position, state.rotation, room.transform);
        MultiplayerEnemy enemy = copy.GetComponent<MultiplayerEnemy>();
        enemy.Initialize(this, state.id, state.room, state.type); enemies[state.id] = enemy;
        return enemy;
    }
    private void StartAttack()
    {
        if (!Playing || !Loot.CanUse(player.weapon)) return;
        var identity = player.weapon.GetComponent<MultiplayerItemIdentity>();
        var message = new MultiplayerWorldMessage { op = "attack", attack = ++localAttack, item = identity.Id,
            damage = Mathf.Clamp(player.playerClass.WD, 1, 10000), position = player.transform.position };
        if (MultiplayerGameplayProtocol.Host) ReceiveRequest(MultiplayerGameplayProtocol.LocalActor, message);
        else MultiplayerGameplayProtocol.Send(MultiplayerGameplayProtocol.WorldRequest, message, true, MultiplayerGameplayProtocol.HostActor);
    }
    public void RequestHit(MultiplayerEnemy enemy, WeaponObject weapon)
    {
        if (!Playing || weapon.player != player || localAttack <= 0 || !Loot.CanUse(player.weapon) || !player.RegisterEnemyHit(enemy.gameObject)) return;
        var message = new MultiplayerWorldMessage { op = "hit", id = enemy.Id, attack = localAttack };
        if (MultiplayerGameplayProtocol.Host) ReceiveRequest(MultiplayerGameplayProtocol.LocalActor, message);
        else MultiplayerGameplayProtocol.Send(MultiplayerGameplayProtocol.WorldRequest, message, true, MultiplayerGameplayProtocol.HostActor);
    }
    private void ReceiveRequest(int actor, MultiplayerWorldMessage message)
    {
        if (message.op == "snapshot") { SendSnapshot(actor); return; }
        if (!Playing) return;
        double now = Time.realtimeSinceStartupAsDouble;
        if (message.op == "presence" && rooms.TryGetValue(message.room ?? "", out var room) && MultiplayerGameplayProtocol.Finite(message.position))
        {
            ObservePose(actor, message.position);
            presence[message.room].Set(actor, message.inside && room.NearFloor(message.position), now);
        }
        else if (message.op == "attack" && message.attack > 0 && message.damage > 0 && message.damage <= 10000 &&
            Loot.OwnedBy(message.item, actor) && (!attacks.TryGetValue(actor, out var prior) || message.attack > prior.Number))
            attacks[actor] = new MultiplayerAttackWindow(message.attack, message.damage, now + 1.5d);
        else if (message.op == "hit" && attacks.TryGetValue(actor, out var attack) && attack.Number == message.attack && now <= attack.Until &&
            enemies.TryGetValue(message.id ?? "", out var enemy) && enemy != null && enemy.Health > 0 &&
            TryPosition(actor, out Vector3 origin) && Vector3.Distance(origin, enemy.transform.position) <= enemy.AttackReach && attack.TryHit(message.attack, message.id, now))
        {
            enemy.TakeAuthoritativeHit(attack.Damage, origin);
            MultiplayerGameplayProtocol.Send(MultiplayerGameplayProtocol.WorldState, enemy.Capture(++revision), true);
        }
    }
    public void Despawn(MultiplayerEnemy enemy)
    {
        if (!MultiplayerGameplayProtocol.Host || !enemies.ContainsKey(enemy.Id)) return;
        var state = new MultiplayerWorldMessage { op = "remove", id = enemy.Id, revision = ++revision };
        Apply(state); MultiplayerGameplayProtocol.Send(MultiplayerGameplayProtocol.WorldState, state, true);
    }
    private void SendSnapshot(int actor)
    {
        var entries = new List<MultiplayerWorldMessage>(roomStates.Values);
        foreach (MultiplayerEnemy enemy in enemies.Values) if (enemy != null) entries.Add(enemy.Capture(++revision));
        foreach (string id in removed) entries.Add(new MultiplayerWorldMessage { op = "remove", id = id, revision = ++revision });
        MultiplayerGameplayProtocol.Send(MultiplayerGameplayProtocol.WorldSnapshot,
            new MultiplayerWorldMessage { op = "snapshot", entries = entries.ToArray() }, true, actor);
    }
    private void Apply(MultiplayerWorldMessage message)
    {
        if (message.op == "room")
        {
            if (string.IsNullOrEmpty(message.room) || message.stage < 0 || message.stage > 2 || message.revision <= 0 ||
                roomStates.TryGetValue(message.room, out var prior) && prior.revision >= message.revision) return;
            roomStates[message.room] = message;
            if (rooms.TryGetValue(message.room, out var room)) room.ApplyState(message.stage, message.revision);
        }
        else if (message.op == "remove" && !string.IsNullOrEmpty(message.id))
        {
            removed.Add(message.id);
            if (enemies.TryGetValue(message.id, out var enemy) && enemy != null)
            { enemy.gameObject.SetActive(false); Destroy(enemy.gameObject); }
            enemies.Remove(message.id);
        }
        else if (message.op == "enemy" && !string.IsNullOrEmpty(message.id) && !removed.Contains(message.id) &&
            MultiplayerGameplayProtocol.Finite(message.position) && MultiplayerGameplayProtocol.Rotation(message.rotation))
        {
            if (!enemies.TryGetValue(message.id, out var enemy) || enemy == null) enemy = CreateEnemy(message);
            if (enemy != null && !MultiplayerGameplayProtocol.Host) enemy.Receive(message);
        }
    }
    public void OnEvent(EventData ev)
    {
        if (session == null || !session.IsRunning) return;
        if (ev.Code == MultiplayerGameplayProtocol.WorldRequest && MultiplayerGameplayProtocol.Host &&
            MultiplayerGameplayProtocol.Read(ev, out MultiplayerWorldMessage request)) ReceiveRequest(ev.Sender, request);
        else if ((ev.Code == MultiplayerGameplayProtocol.WorldState || ev.Code == MultiplayerGameplayProtocol.EnemyFrames ||
            ev.Code == MultiplayerGameplayProtocol.WorldSnapshot) && ev.Sender == MultiplayerGameplayProtocol.HostActor &&
            !MultiplayerGameplayProtocol.Host && MultiplayerGameplayProtocol.Read(ev, out MultiplayerWorldMessage state))
        {
            if (state.entries != null)
            { if (state.entries.Length <= 512) foreach (var entry in state.entries) if (entry != null) Apply(entry); }
            else Apply(state);
        }
    }
}
