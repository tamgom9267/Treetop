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
    private readonly Dictionary<string, Chest> chests = new Dictionary<string, Chest>();
    private readonly Dictionary<string, MultiplayerWorldMessage> chestStates = new Dictionary<string, MultiplayerWorldMessage>();
    private readonly Dictionary<string, MultiplayerEnemy> enemies = new Dictionary<string, MultiplayerEnemy>();
    private readonly HashSet<string> removed = new HashSet<string>();
    private readonly Dictionary<int, Vector3> positions = new Dictionary<int, Vector3>();
    private readonly Dictionary<int, double> poseTimes = new Dictionary<int, double>();
    private readonly Dictionary<int, MultiplayerAttackWindow> attacks = new Dictionary<int, MultiplayerAttackWindow>();
    private sealed class SpellCastWindow
    {
        public int number, damage;
        public string spell;
        public Vector3 origin;
        public double started;
        public bool hit;
    }
    private readonly Dictionary<string, SpellCastWindow> spellCasts = new Dictionary<string, SpellCastWindow>();
    private readonly Dictionary<int, int> lastSpellNumbers = new Dictionary<int, int>();
    private readonly Dictionary<string, double> spellCooldowns = new Dictionary<string, double>();
    private readonly HashSet<string> shownSpells = new HashSet<string>();
    private readonly List<MultiplayerWorldMessage> enemyFrames = new List<MultiplayerWorldMessage>();
    private readonly List<MultiplayerSlimeMotion> ownedFrames = new List<MultiplayerSlimeMotion>();
    private MultiplayerRunSession session;
    private MultiplayerPlayers players;
    private PlayerController player;
    private string floorKey;
    private int revision, localAttack, localSpellCast;
    private double lastPresence = double.NegativeInfinity, lastSnapshot = double.NegativeInfinity;
    private MultiplayerPlayers.TrafficClock frameClock;
    private MultiplayerPlayers.TrafficClock ownedMotionClock;
    private bool suspended = true;
    public MultiplayerLootSession Loot { get; private set; }
    public MultiplayerInventoryAdapter Inventory { get; private set; }
    public MultiplayerPlayers.TrafficRates Traffic => players != null ? players.Traffic : MultiplayerPlayers.TrafficRates.Defaults();
    public bool Playing => !suspended && session != null && session.IsPlaying && !Loot.Settling;
    public bool Initialize(MultiplayerRunSession run, MultiplayerPlayers sync, out string error)
    {
        if (Application.isPlaying && (!PersistentScenePolicy.IsAllowed(UnityEngine.SceneManagement.SceneManager.GetActiveScene()) ||
            PhotonPartyService.IsSceneExitInProgress))
        { error = "La escena actual no admite una sesion multijugador."; return false; }
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
        spellCasts.Clear(); lastSpellNumbers.Clear(); spellCooldowns.Clear(); shownSpells.Clear(); localSpellCast = 0;
        chests.Clear(); chestStates.Clear();
        enemies.Clear(); positions.Clear(); poseTimes.Clear(); lastPresence = lastSnapshot = double.NegativeInfinity;
        frameClock = ownedMotionClock = default;
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
    public void Suspend()
    {
        suspended = true; attacks.Clear(); spellCasts.Clear();
        foreach (MultiplayerEnemy enemy in enemies.Values) if (enemy != null) enemy.SuspendMotion();
    }
    public void Resume()
    {
        suspended = false; Loot.Resume(); lastPresence = lastSnapshot = double.NegativeInfinity;
        foreach (MultiplayerEnemy enemy in enemies.Values) if (enemy != null)
        {
            bool delegated = enemy.Authority && enemy.MotionActor != MultiplayerGameplayProtocol.HostActor;
            enemy.ResumeMotion();
            if (delegated) PublishEnemyState(enemy);
        }
    }
    public void Shutdown()
    {
        Suspend();
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
        for (int i = 0; i < room.enemies.Length; i++) RegisterPlacedEnemy(room, i);
        if (roomStates.TryGetValue(room.RoomId, out var state)) room.ApplyState(state.stage, state.revision);
        Chest[] placedChests = room.GetComponentsInChildren<Chest>(true);
        Array.Sort(placedChests, (a, b) => string.CompareOrdinal(ChestPath(a.transform, room.transform), ChestPath(b.transform, room.transform)));
        for (int i = 0; i < placedChests.Length; i++)
        {
            string id = room.RoomId + ":chest:" + i;
            chests[id] = placedChests[i];
            if (chestStates.ContainsKey(id)) placedChests[i].ApplySharedOpen();
        }
    }
    private static string ChestPath(Transform item, Transform room)
    {
        string path = "";
        while (item != null && item != room)
        { path = item.name + ":" + item.GetSiblingIndex() + "/" + path; item = item.parent; }
        return path;
    }
    public void RequestChestOpen(Chest chest)
    {
        if (!Playing || chest == null || !chest.gameObject.activeInHierarchy || chest.chestInteracted) return;
        foreach (var pair in chests)
        {
            if (pair.Value != chest) continue;
            var request = new MultiplayerWorldMessage { op = "openChest", id = pair.Key };
            if (MultiplayerGameplayProtocol.Host) ReceiveRequest(MultiplayerGameplayProtocol.LocalActor, request);
            else MultiplayerGameplayProtocol.Send(MultiplayerGameplayProtocol.WorldRequest, request, true, MultiplayerGameplayProtocol.HostActor, flush: true);
            return;
        }
    }
    private void OpenChest(int actor, string id)
    {
        if (string.IsNullOrEmpty(id) || chestStates.ContainsKey(id) || !chests.TryGetValue(id, out var chest) ||
            chest == null || !chest.gameObject.activeInHierarchy || !TryPosition(actor, out Vector3 origin)) return;
        Collider body = chest.GetComponent<Collider>();
        Vector3 closest = body != null ? body.ClosestPoint(origin) : chest.transform.position;
        if (Vector3.Distance(origin, closest) > 3f) return;
        if (!Loot.SpawnChestReward(id, chest)) return;
        var state = new MultiplayerWorldMessage { op = "chest", id = id, revision = ++revision };
        Apply(state); MultiplayerGameplayProtocol.Send(MultiplayerGameplayProtocol.WorldState, state, true, flush: true);
    }
    private MultiplayerEnemy RegisterPlacedEnemy(MultiplayerRoomController room, int index)
    {
        MultiplayerEnemy enemy = room.enemies[index];
        if (enemy == null) return null;
        string id = room.RoomId + ":placed:" + index;
        if (removed.Contains(id))
        { enemy.gameObject.SetActive(false); Destroy(enemy.gameObject); return null; }
        if (enemies.TryGetValue(id, out var registered)) return registered;
        enemy.Initialize(this, id, room.RoomId, 0);
        enemy.SetCombatActive(room.Stage == 1);
        enemies[id] = enemy;
        return enemy;
    }
    public void ObservePose(int actor, Vector3 position)
    {
        positions[actor] = position; poseTimes[actor] = Time.realtimeSinceStartupAsDouble;
    }
    public bool TryPosition(int actor, out Vector3 position)
    {
        if (MultiplayerDeathSession.Instance != null && !MultiplayerDeathSession.Instance.IsAlive(actor))
        { position = default; return false; }
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
            if ((MultiplayerDeathSession.Instance == null || MultiplayerDeathSession.Instance.IsAlive(actor)) &&
                occupants.Contains(actor, Time.realtimeSinceStartupAsDouble) && TryPosition(actor, out Vector3 position))
            {
                float distance = (origin - position).sqrMagnitude;
                if (distance < best) { best = distance; target = position; }
            }
        return !float.IsPositiveInfinity(best);
    }
    private void Update()
    {
        if (!Playing) return;
        double now = Time.realtimeSinceStartupAsDouble;
        MultiplayerPlayers.TrafficRates traffic = Traffic;
        if (MultiplayerGameplayProtocol.Host)
            foreach (MultiplayerEnemy enemy in enemies.Values) if (enemy != null &&
                enemy.NeedsHostRecovery(now, MultiplayerGameplayProtocol.Participant(enemy.MotionActor)))
            { enemy.RecoverMotionToHost(); PublishEnemyState(enemy); }
        if (!MultiplayerGameplayProtocol.Host && ownedMotionClock.Tick(now, traffic.ownedEnemyMotionSendHz))
        {
            foreach (MultiplayerEnemy enemy in enemies.Values)
                if (enemy != null && enemy.PublishesMotion) ownedFrames.Add(enemy.CaptureMotion());
            MultiplayerGameplayProtocol.SendMotionFrames(MultiplayerGameplayProtocol.SlimeMotion, ownedFrames);
            ownedFrames.Clear();
        }
        if (player == null) return;
        bool localAlive = MultiplayerDeathSession.Instance == null ||
            MultiplayerDeathSession.Instance.IsAlive(MultiplayerGameplayProtocol.LocalActor);
        if (localAlive && now - lastPresence >= 1d / traffic.roomPresenceSendHz)
        {
            lastPresence = now;
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
            if (now - lastSnapshot >= 1d / traffic.worldSnapshotRequestHz)
            {
                lastSnapshot = now;
                MultiplayerGameplayProtocol.Send(MultiplayerGameplayProtocol.WorldRequest,
                    new MultiplayerWorldMessage { op = "snapshot" }, true, MultiplayerGameplayProtocol.HostActor);
            }
            return;
        }
        var actors = MultiplayerGameplayProtocol.Participants();
        if (MultiplayerDeathSession.Instance != null)
            actors.RemoveAll(actor => !MultiplayerDeathSession.Instance.IsAlive(actor));
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
        if (frameClock.Tick(now, traffic.enemyStateSendHz))
        {
            foreach (MultiplayerEnemy enemy in enemies.Values)
                if (enemy != null && enemy.CombatActive && enemy.MotionActor == MultiplayerGameplayProtocol.HostActor)
                    enemyFrames.Add(enemy.Capture(++revision));
            MultiplayerGameplayProtocol.SendMotionFrames(MultiplayerGameplayProtocol.EnemyFrames, enemyFrames);
            enemyFrames.Clear();
        }
    }
    private void Activate(MultiplayerRoomController room)
    {
        CommitRoom(room, 1);
        foreach (MultiplayerEnemy enemy in room.enemies)
            if (enemy != null) PublishEnemyState(enemy);
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
        if (!rooms.TryGetValue(state.room ?? "", out var room)) return null;
        // Las copias del jefe ya existen en ambos mapas; no se instancian de nuevo.
        if (state.type == 0)
        {
            for (int i = 0; i < room.enemies.Length; i++)
                if (state.id == room.RoomId + ":placed:" + i) return RegisterPlacedEnemy(room, i);
            return null;
        }
        if (state.type < 1 || state.type > 2) return null;
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
        WeaponObject weapon = player.EquippedWeaponInstance;
        if (!Playing || weapon == null || !Loot.CanUse(player.weapon)) return;
        var identity = player.weapon.GetComponent<MultiplayerItemIdentity>();
        var message = new MultiplayerWorldMessage { op = "attack", attack = ++localAttack, item = identity.Id,
            damage = Mathf.Clamp(player.playerClass.WD + weapon.WD, 1, 10000), position = player.transform.position };
        if (MultiplayerGameplayProtocol.Host) ReceiveRequest(MultiplayerGameplayProtocol.LocalActor, message);
        else MultiplayerGameplayProtocol.Send(MultiplayerGameplayProtocol.WorldRequest, message, true, MultiplayerGameplayProtocol.HostActor, flush: true);
    }
    public void RequestHit(MultiplayerEnemy enemy, WeaponObject weapon)
    {
        if (!Playing || enemy == null || !enemy.CombatActive || weapon.player != player || localAttack <= 0 || !Loot.CanUse(player.weapon) || !player.RegisterEnemyHit(enemy.gameObject)) return;
        var message = new MultiplayerWorldMessage { op = "hit", id = enemy.Id, attack = localAttack };
        if (MultiplayerGameplayProtocol.Host) ReceiveRequest(MultiplayerGameplayProtocol.LocalActor, message);
        else if (MultiplayerGameplayProtocol.Send(MultiplayerGameplayProtocol.WorldRequest, message, true, MultiplayerGameplayProtocol.HostActor, flush: true) && !enemy.Anchored)
            enemy.BeginLocalKnockback(MultiplayerGameplayProtocol.LocalActor, localAttack, player.transform.position);
    }
    public int RequestSpellCast(SpellObject spell, int damage, Vector3 origin, Quaternion rotation)
    {
        if (!Playing || spell == null || spell.player != player || !Loot.CanUse(spell.gameObject) ||
            !MultiplayerGameplayProtocol.Finite(origin) || !MultiplayerGameplayProtocol.Rotation(rotation)) return 0;
        MultiplayerItemIdentity identity = spell.GetComponent<MultiplayerItemIdentity>();
        if (identity == null || string.IsNullOrEmpty(identity.Id)) return 0;
        int number = ++localSpellCast;
        var request = new MultiplayerWorldMessage { op = "spellCast", item = identity.Id, id = spell.Objectname,
            actor = MultiplayerGameplayProtocol.LocalActor, attack = number, damage = Mathf.Clamp(damage, 0, 10000),
            position = origin, rotation = rotation };
        if (MultiplayerGameplayProtocol.Host) ReceiveRequest(MultiplayerGameplayProtocol.LocalActor, request);
        else if (!MultiplayerGameplayProtocol.Send(MultiplayerGameplayProtocol.WorldRequest, request, true,
            MultiplayerGameplayProtocol.HostActor, flush: true)) return 0;
        return number;
    }
    public void RequestSpellHit(int cast, MultiplayerEnemy enemy, Vector3 impact)
    {
        if (!Playing || cast <= 0 || enemy == null || !enemy.CombatActive || !MultiplayerGameplayProtocol.Finite(impact)) return;
        var request = new MultiplayerWorldMessage { op = "spellHit", id = enemy.Id, attack = cast, position = impact };
        if (MultiplayerGameplayProtocol.Host) ReceiveRequest(MultiplayerGameplayProtocol.LocalActor, request);
        else MultiplayerGameplayProtocol.Send(MultiplayerGameplayProtocol.WorldRequest, request, true,
            MultiplayerGameplayProtocol.HostActor, flush: true);
    }
    private void AcceptSpellCast(int actor, MultiplayerWorldMessage message, double now)
    {
        if (message.attack <= 0 || message.damage <= 0 || message.damage > 10000 ||
            !MultiplayerGameplayProtocol.Finite(message.position) || !MultiplayerGameplayProtocol.Rotation(message.rotation) ||
            !Loot.TryGetOwnedSpellName(message.item, actor, out string spell) || spell != "FireBall" || message.id != spell ||
            lastSpellNumbers.TryGetValue(actor, out int previous) && message.attack <= previous ||
            !TryPosition(actor, out Vector3 position) || Vector3.Distance(position, message.position) > 3f) return;
        string cooldownKey = actor + ":" + spell;
        if (spellCooldowns.TryGetValue(cooldownKey, out double last) && now - last < 4.85d) return;
        spellCooldowns[cooldownKey] = now;
        lastSpellNumbers[actor] = message.attack;
        spellCasts[actor + ":" + message.attack] = new SpellCastWindow { number = message.attack, spell = spell,
            damage = message.damage, origin = message.position, started = now };
        var state = new MultiplayerWorldMessage { op = "spellCast", actor = actor, attack = message.attack,
            id = spell, position = message.position, rotation = message.rotation };
        Apply(state);
        MultiplayerGameplayProtocol.Send(MultiplayerGameplayProtocol.WorldState, state, true, flush: true);
    }
    private void AcceptSpellHit(int actor, MultiplayerWorldMessage message, double now)
    {
        if (!spellCasts.TryGetValue(actor + ":" + message.attack, out var cast) || cast.spell != "FireBall" ||
            cast.number != message.attack || now - cast.started > 3d ||
            !MultiplayerGameplayProtocol.Finite(message.position) ||
            Vector3.Distance(cast.origin, message.position) > 4f + 22f * (float)(now - cast.started) ||
            !enemies.TryGetValue(message.id ?? "", out var enemy) || enemy == null || !enemy.CombatActive ||
            Vector3.Distance(message.position, enemy.MotionPosition) > enemy.AttackReach || cast.hit ||
            !TryPosition(actor, out _)) return;
        cast.hit = true;
        enemy.TakeAuthoritativeHit(cast.damage, message.position, actor, message.attack);
        PublishEnemyState(enemy);
        enemy.ApplyBufferedMotion();
    }
    private void ReceiveRequest(int actor, MultiplayerWorldMessage message)
    {
        if (message.op == "snapshot") { SendSnapshot(actor); return; }
        if (!Playing) { RejectHit(actor, message); return; }
        double now = Time.realtimeSinceStartupAsDouble;
        if (message.op == "openChest") OpenChest(actor, message.id);
        else if (message.op == "spellCast") AcceptSpellCast(actor, message, now);
        else if (message.op == "spellHit") AcceptSpellHit(actor, message, now);
        else if (message.op == "presence" && rooms.TryGetValue(message.room ?? "", out var room) && MultiplayerGameplayProtocol.Finite(message.position))
        {
            ObservePose(actor, message.position);
            presence[message.room].Set(actor, message.inside && room.NearFloor(message.position) &&
                (MultiplayerDeathSession.Instance == null || MultiplayerDeathSession.Instance.IsAlive(actor)), now);
        }
        else if (message.op == "attack" && message.attack > 0 && message.damage > 0 && message.damage <= 10000 &&
            Loot.TryGetWeaponData(message.item, actor, out ObjectData weaponData) &&
            (!attacks.TryGetValue(actor, out var prior) || message.attack > prior.Number))
            attacks[actor] = new MultiplayerAttackWindow(message.attack, message.damage, now + 1.5d, weaponData);
        else if (message.op == "hit")
        {
            if (attacks.TryGetValue(actor, out var attack) && attack.Number == message.attack && now <= attack.Until &&
                enemies.TryGetValue(message.id ?? "", out var enemy) && enemy != null && enemy.CombatActive &&
                TryPosition(actor, out Vector3 origin) && Vector3.Distance(origin, enemy.MotionPosition) <= enemy.AttackReach && attack.TryHit(message.attack, message.id, now))
            {
                enemy.TakeAuthoritativeHit(attack.Damage, origin, actor, message.attack);
                if (enemy.Health > 0 && attack.WeaponData != null)
                    enemy.ApplyWeaponEffects(attack.WeaponData.additionalEffects);
                PublishEnemyState(enemy);
                enemy.ApplyBufferedMotion();
            }
            else RejectHit(actor, message);
        }
    }
    private void RejectHit(int actor, MultiplayerWorldMessage message)
    {
        if (message.op != "hit" || actor == MultiplayerGameplayProtocol.LocalActor || message.attack <= 0 || string.IsNullOrEmpty(message.id)) return;
        MultiplayerGameplayProtocol.Send(MultiplayerGameplayProtocol.WorldState,
            new MultiplayerWorldMessage { op = "hitRejected", id = message.id, actor = actor, attack = message.attack }, true, actor, flush: true);
        if (enemies.TryGetValue(message.id, out var enemy) && enemy != null)
        {
            // Si un segundo golpe del controlador se rechaza, no conservar su impulso provisional.
            if (enemy.MotionActor == actor) { enemy.RecoverMotionToHost(); PublishEnemyState(enemy); }
            else MultiplayerGameplayProtocol.Send(MultiplayerGameplayProtocol.WorldState, enemy.Capture(++revision), true, actor, flush: true);
        }
    }
    internal void PublishEnemyState(MultiplayerEnemy enemy) =>
        MultiplayerGameplayProtocol.Send(MultiplayerGameplayProtocol.WorldState, enemy.Capture(++revision), true, flush: true);
    public void PublishStatusDamage(MultiplayerEnemy enemy)
    {
        if (MultiplayerGameplayProtocol.Host && enemy != null)
            PublishEnemyState(enemy);
    }
    public void PublishSlimeLanding(MultiplayerSlimeMotion final)
    {
        if (!Playing || final == null) return;
        final.finished = true;
        MultiplayerGameplayProtocol.Send(MultiplayerGameplayProtocol.SlimeMotion,
            new MultiplayerSlimeMotionBatch { entries = new[] { final } }, true, flush: true);
    }
    public void CompleteSlimeMotion(MultiplayerEnemy enemy, MultiplayerSlimeMotion final, bool landed)
    {
        if (!MultiplayerGameplayProtocol.Host || !Playing || enemy == null || final == null || enemy.Health <= 0 ||
            !enemies.TryGetValue(enemy.Id, out var current) || current != enemy ||
            final.actor != enemy.MotionActor || final.attack != enemy.MotionAttack) return;
        enemy.ReturnMotionToHost(final, landed); PublishEnemyState(enemy);
    }
    public void Despawn(MultiplayerEnemy enemy)
    {
        if (!MultiplayerGameplayProtocol.Host || !enemies.ContainsKey(enemy.Id)) return;
        var state = new MultiplayerWorldMessage { op = "remove", id = enemy.Id, revision = ++revision };
        Apply(state); MultiplayerGameplayProtocol.Send(MultiplayerGameplayProtocol.WorldState, state, true, flush: true);
    }
    private void SendSnapshot(int actor)
    {
        var entries = new List<MultiplayerWorldMessage>(roomStates.Values);
        entries.AddRange(chestStates.Values);
        foreach (MultiplayerEnemy enemy in enemies.Values) if (enemy != null) entries.Add(enemy.Capture(++revision));
        foreach (string id in removed) entries.Add(new MultiplayerWorldMessage { op = "remove", id = id, revision = ++revision });
        MultiplayerGameplayProtocol.Send(MultiplayerGameplayProtocol.WorldSnapshot,
            new MultiplayerWorldBatch { op = "snapshot", entries = entries.ToArray() }, true, actor);
        foreach (MultiplayerEnemy enemy in enemies.Values)
            if (enemy != null && enemy.Anchored)
            {
                MultiplayerTreeRootSync tree = enemy.GetComponentInParent<MultiplayerTreeRootSync>();
                if (tree != null) tree.Publish(true, actor);
            }
    }
    private void Apply(MultiplayerWorldMessage message)
    {
        if (message.op == "spellCast")
        {
            if (message.id != "FireBall" || message.actor <= 0 || message.attack <= 0 ||
                !MultiplayerGameplayProtocol.Finite(message.position) || !MultiplayerGameplayProtocol.Rotation(message.rotation) ||
                !shownSpells.Add(message.actor + ":" + message.attack) || message.actor == MultiplayerGameplayProtocol.LocalActor) return;
            ShowRemoteSpell(message);
        }
        else if (message.op == "chest")
        {
            if (string.IsNullOrEmpty(message.id) || message.revision <= 0 ||
                chestStates.TryGetValue(message.id, out var priorChest) && priorChest.revision >= message.revision) return;
            chestStates[message.id] = message;
            if (chests.TryGetValue(message.id, out var chest) && chest != null) chest.ApplySharedOpen();
        }
        else if (message.op == "hitRejected")
        {
            if (message.actor == MultiplayerGameplayProtocol.LocalActor && !string.IsNullOrEmpty(message.id) &&
                enemies.TryGetValue(message.id, out var rejected) && rejected != null)
                rejected.RejectLocalKnockback(message.actor, message.attack);
        }
        else if (message.op == "room")
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
            { enemy.SpawnDeathEffect(); enemy.SuspendMotion(); GameObject target = enemy.DespawnTarget; target.SetActive(false); Destroy(target); }
            enemies.Remove(message.id);
        }
        else if (message.op == "enemy" && !string.IsNullOrEmpty(message.id) && !removed.Contains(message.id) &&
            MultiplayerGameplayProtocol.Finite(message.position) && MultiplayerGameplayProtocol.Rotation(message.rotation))
        {
            if (!enemies.TryGetValue(message.id, out var enemy) || enemy == null) enemy = CreateEnemy(message);
            if (enemy != null && !MultiplayerGameplayProtocol.Host) enemy.Receive(message);
        }
    }
    private static void ShowRemoteSpell(MultiplayerWorldMessage message)
    {
        GameObject prefab = Resources.Load<GameObject>("GeneracionDeCuartos/Hechizos/Bola de Fuego Activa");
        if (prefab == null) return;
        GameObject projectile = Instantiate(prefab, message.position, message.rotation);
        SpellProjectile component = projectile.GetComponent<SpellProjectile>();
        if (component != null) component.InitializeRemoteVisual();
        Rigidbody body = projectile.GetComponent<Rigidbody>();
        if (body != null) body.AddForce(message.rotation * Vector3.forward * 15f, ForceMode.VelocityChange);
        Destroy(projectile, 4f);
    }
    public void OnEvent(EventData ev)
    {
        if (session == null || !session.IsRunning) return;
        if (ev.Code == MultiplayerGameplayProtocol.SlimeMotion)
        {
            if (!Playing || !MultiplayerGameplayProtocol.Read(ev, out MultiplayerSlimeMotionBatch motion) ||
                motion.entries == null || motion.entries.Length > 512) return;
            foreach (var frame in motion.entries)
                if (frame != null && !string.IsNullOrEmpty(frame.id) && enemies.TryGetValue(frame.id, out var enemy) && enemy != null)
                    enemy.ReceiveMotion(ev.Sender, frame);
        }
        else if (ev.Code == MultiplayerGameplayProtocol.WorldRequest && MultiplayerGameplayProtocol.Host &&
            MultiplayerGameplayProtocol.Read(ev, out MultiplayerWorldMessage request)) ReceiveRequest(ev.Sender, request);
        else if (ev.Sender == MultiplayerGameplayProtocol.HostActor && !MultiplayerGameplayProtocol.Host)
        {
            if (ev.Code == MultiplayerGameplayProtocol.WorldState &&
                MultiplayerGameplayProtocol.Read(ev, out MultiplayerWorldMessage state)) Apply(state);
            else if ((ev.Code == MultiplayerGameplayProtocol.EnemyFrames || ev.Code == MultiplayerGameplayProtocol.WorldSnapshot) &&
                MultiplayerGameplayProtocol.Read(ev, out MultiplayerWorldBatch batch) && batch.entries != null && batch.entries.Length <= 512)
            {
                foreach (var entry in batch.entries) if (entry != null) Apply(entry);
            }
        }
    }
}
