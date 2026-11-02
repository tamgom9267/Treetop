using System;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class MultiplayerLootSession : MonoBehaviour, IOnEventCallback
{
    [SerializeField] private MultiplayerLootDefinition[] catalog = Array.Empty<MultiplayerLootDefinition>();
    private readonly MultiplayerLootLedger ledger = new MultiplayerLootLedger();
    private readonly Dictionary<string, MultiplayerLootRecord> records = new Dictionary<string, MultiplayerLootRecord>();
    private readonly Dictionary<string, InventoryObject> copies = new Dictionary<string, InventoryObject>();
    private readonly Dictionary<string, MultiplayerLootMessage> pending = new Dictionary<string, MultiplayerLootMessage>();
    private readonly Dictionary<string, double> sentAt = new Dictionary<string, double>();
    private readonly HashSet<int> settlementAcks = new HashSet<int>();
    private readonly HashSet<string> imported = new HashSet<string>();
    private readonly HashSet<int> departed = new HashSet<int>();
    private MultiplayerWorldSession world;
    private PlayerController player;
    private int localActor;
    private string barrier, lastBarrier, claimRequest, claimId;
    private Action<bool> onSettled;
    private bool settlementAckSent, initialized;
    private double lastScan = double.NegativeInfinity, lastSnapshot = double.NegativeInfinity, barrierDeadline;
    public bool Settling { get; private set; }
    public bool Ready => initialized && pending.Count == 0;
    public bool HasPending => pending.Count != 0;
    public bool Initialize(MultiplayerWorldSession owner, out string error)
    {
        localActor = MultiplayerGameplayProtocol.LocalActor;
        world = owner; error = "Falta el catalogo de loot de fase 4.";
        if (catalog == null || catalog.Length == 0) return false;
        var types = new HashSet<string>();
        foreach (var entry in catalog)
            if (entry == null || string.IsNullOrEmpty(entry.type) || !types.Add(entry.type) || entry.prefab == null || entry.prefab.GetComponent<InventoryObject>() == null) return false;
        initialized = true; error = string.Empty; return true;
    }
    private void OnEnable() { PhotonNetwork.AddCallbackTarget(this); }
    private void OnDisable() { PhotonNetwork.RemoveCallbackTarget(this); }
    public void BeginFloor(int floor)
    {
        if (MultiplayerGameplayProtocol.Host) ledger.NewFloor(floor);
        // El inventario conserva identidades; las copias del piso anterior se retiran antes del generador.
        foreach (var record in new List<MultiplayerLootRecord>(records.Values))
            if (record.location == MultiplayerItemLocation.World || record.location == MultiplayerItemLocation.Reserved)
            {
                if (copies.TryGetValue(record.id, out var item) && item != null) world.Inventory.Revoke(item, true);
                copies.Remove(record.id); records.Remove(record.id);
            }
        lastScan = lastSnapshot = double.NegativeInfinity;
    }
    public void Bind(PlayerController local)
    {
        bool first = player == null; player = local; Scan();
        if (first) foreach (var record in new List<MultiplayerLootRecord>(records.Values)) Reconcile(record);
    }
    public void Resume() { Settling = false; barrier = null; settlementAckSent = false; lastScan = lastSnapshot = double.NegativeInfinity; }
    public bool OwnedBy(string id, int actor)
    {
        var source = MultiplayerGameplayProtocol.Host ? ledger.Records : records;
        return !string.IsNullOrEmpty(id) && source.TryGetValue(id, out var item) && item.owner == actor &&
            (item.location == MultiplayerItemLocation.Reserved || item.location == MultiplayerItemLocation.Inventory);
    }
    public bool CanUse(GameObject item)
    {
        if (item == null) return false;
        var identity = item.GetComponent<MultiplayerItemIdentity>();
        return identity != null && OwnedBy(identity.Id, localActor);
    }
    private MultiplayerLootDefinition Definition(InventoryObject item)
    {
        WeaponObject weapon = item as WeaponObject;
        foreach (var entry in catalog)
            if (weapon != null ? entry.weapon == weapon.weaponData : entry.weapon == null && entry.objectName == item.Objectname) return entry;
        return null;
    }
    private MultiplayerLootDefinition Definition(string type)
    { foreach (var entry in catalog) if (entry.type == type) return entry; return null; }
    private bool LocalHeld(InventoryObject item) => player.Inventory.Inventory.Contains(item.gameObject) || player.Inventory.Loot.Contains(item.gameObject) || player.weapon == item.gameObject;
    private void Scan()
    {
        if (!initialized || player == null) return;
        foreach (var item in FindObjectsByType<InventoryObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (item == null || item is WeaponObject equipped && equipped.inHand || item.gameObject == (player.EquippedWeaponInstance != null ? player.EquippedWeaponInstance.gameObject : null)) continue;
            bool held = LocalHeld(item);
            bool inWorld = item.gameObject.scene == SceneManager.GetActiveScene() && item.gameObject.activeInHierarchy && !item.transform.IsChildOf(player.transform);
            if (!held && !inWorld) continue;
            var definition = Definition(item);
            if (definition == null)
            { MultiplayerRunSession.Instance.ReportBuildFailed("Objeto de inventario sin registro en el catalogo de loot: " + item.name); return; }
            var identity = item.GetComponent<MultiplayerItemIdentity>();
            if (identity == null) identity = item.gameObject.AddComponent<MultiplayerItemIdentity>();
            string run = MultiplayerRunSession.Instance.RunId;
            if (identity.Run != run || string.IsNullOrEmpty(identity.Id))
            { identity.Run = run; identity.Id = string.IsNullOrEmpty(identity.EmbeddedId) ? Guid.NewGuid().ToString("N") : MultiplayerRunSession.Instance.FloorNumber + ":" + identity.EmbeddedId; }
            copies[identity.Id] = item;
            if (!records.TryGetValue(identity.Id, out var record))
            {
                if (held) imported.Add(identity.Id);
                if (!PendingItem(identity.Id)) Request("register", new MultiplayerLootRecord { id = identity.Id, type = definition.type,
                    location = held ? MultiplayerItemLocation.Inventory : MultiplayerItemLocation.World, data = MultiplayerItemData.Capture(item),
                    position = item.transform.position, rotation = item.transform.rotation });
            }
            else if (!PendingItem(identity.Id) && record.owner == localActor)
            {
                if (inWorld && !held)
                {
                    var drop = record.Copy(); drop.position = item.transform.position; drop.rotation = item.transform.rotation; drop.data = MultiplayerItemData.Capture(item);
                    Request("drop", drop);
                }
                else if (record.location == MultiplayerItemLocation.Reserved && player.Inventory.Inventory.Contains(item.gameObject)) Request("store", record.Copy());
            }
        }
    }
    private bool PendingItem(string id)
    { foreach (var request in pending.Values) if (request.record != null && request.record.id == id) return true; return false; }
    public bool TryClaim(GameObject target)
    {
        if (Settling || claimRequest != null || target == null || player == null) return false;
        var identity = target.GetComponent<MultiplayerItemIdentity>();
        if (identity == null || !records.TryGetValue(identity.Id ?? "", out var record) || record.location != MultiplayerItemLocation.World || PendingItem(identity.Id)) return false;
        claimId = identity.Id;
        // Establecer antes del envio: en el host la confirmacion es sincrona.
        var message = NewRequest("claim", record.Copy()); claimRequest = message.request;
        SendRequest(message); return true;
    }
    public bool ClaimPending => claimRequest != null;
    private MultiplayerLootMessage NewRequest(string op, MultiplayerLootRecord record) =>
        new MultiplayerLootMessage { op = op, request = Guid.NewGuid().ToString("N"), record = record };
    private void Request(string op, MultiplayerLootRecord record) { SendRequest(NewRequest(op, record)); }
    private void SendRequest(MultiplayerLootMessage request)
    {
        pending[request.request] = request; sentAt[request.request] = Time.realtimeSinceStartupAsDouble;
        if (MultiplayerGameplayProtocol.Host) Process(localActor, request);
        else if (!MultiplayerGameplayProtocol.Send(MultiplayerGameplayProtocol.LootRequest, request, true, MultiplayerGameplayProtocol.HostActor))
            MultiplayerRunSession.Instance.ReportBuildFailed("No se pudo enviar la transaccion de loot.");
    }
    private void Process(int actor, MultiplayerLootMessage request)
    {
        if (request.op == "snapshot") { SendSnapshot(actor); return; }
        if (request.record == null || Definition(request.record.type) == null) return;
        if (request.op == "claim" && (!world.TryPosition(actor, out Vector3 position) ||
            !ledger.Records.TryGetValue(request.record.id ?? "", out var available) || Vector3.Distance(position, available.position) > 10f))
        {
            Confirm(actor, request.request, ledger.Records.TryGetValue(request.record.id ?? "", out var denied) ? denied.Copy() : null); return;
        }
        var result = ledger.Process(actor, request, MultiplayerRunSession.Instance.FloorNumber);
        Confirm(actor, request.request, result != null ? result.Copy() : null);
    }
    private void Confirm(int actor, string request, MultiplayerLootRecord record)
    {
        var message = new MultiplayerLootMessage { op = "state", actor = actor, request = request, record = record };
        Apply(message); MultiplayerGameplayProtocol.Send(MultiplayerGameplayProtocol.LootState, message, true);
    }
    private void Apply(MultiplayerLootMessage message)
    {
        bool ownResponse = message.actor == localActor && !string.IsNullOrEmpty(message.request);
        if (ownResponse) { pending.Remove(message.request); sentAt.Remove(message.request); }
        var record = message.record;
        if (record != null && !string.IsNullOrEmpty(record.id) && record.data != null && record.data.Valid() &&
            Definition(record.type) != null && MultiplayerGameplayProtocol.Finite(record.position) && MultiplayerGameplayProtocol.Rotation(record.rotation))
        {
            if (!records.TryGetValue(record.id, out var previous) || record.revision > previous.revision)
            {
                records[record.id] = record.Copy(); imported.Remove(record.id); if (player != null) Reconcile(record);
            }
        }
        if (ownResponse && message.request == claimRequest)
        {
            string id = claimId; claimRequest = claimId = null;
            if (records.TryGetValue(id, out var current) && current.owner == localActor && current.location == MultiplayerItemLocation.Reserved)
            {
                if (Settling || !copies.TryGetValue(id, out var item) || item == null || !world.Inventory.Inspect(item, Definition(current.type).icon))
                {
                    var drop = current.Copy(); drop.position = player.transform.position;
                    Request("drop", drop);
                }
            }
        }
    }
    private void Reconcile(MultiplayerLootRecord record)
    {
        copies.TryGetValue(record.id, out var item);
        bool mine = record.owner == localActor;
        if (record.location == MultiplayerItemLocation.World && record.floor == MultiplayerRunSession.Instance.FloorNumber)
        {
            if (item == null)
            {
                var definition = Definition(record.type);
                item = Instantiate(definition.prefab, record.position, record.rotation).GetComponent<InventoryObject>();
                var identity = item.GetComponent<MultiplayerItemIdentity>();
                if (identity == null) identity = item.gameObject.AddComponent<MultiplayerItemIdentity>();
                identity.Id = record.id; identity.Run = MultiplayerRunSession.Instance.RunId;
                copies[record.id] = item;
            }
            if (LocalHeld(item)) world.Inventory.Revoke(item, false);
            record.data.Apply(item); item.ID = 0;
            item.transform.SetParent(null, true); SceneManager.MoveGameObjectToScene(item.gameObject, SceneManager.GetActiveScene());
            item.transform.SetParent(player.Inventory.GetOrCreateWorldLootContainer(), true);
            item.transform.SetPositionAndRotation(record.position, record.rotation); item.gameObject.SetActive(true);
        }
        else if (mine && (record.location == MultiplayerItemLocation.Reserved || record.location == MultiplayerItemLocation.Inventory))
        {
            // Una confirmacion de guardar puede llegar despues de que la UI ya lo solto.
            // Conservar ese intento local para enviar drop con la revision confirmada.
            if (item != null && (claimId == record.id || LocalHeld(item)))
            { item.transform.SetParent(player.transform, true); item.gameObject.SetActive(false); }
        }
        else if (item != null)
        { world.Inventory.Revoke(item, true); copies.Remove(record.id); world.PublishEquipment(); }
    }
    private void SendSnapshot(int actor)
    {
        var array = new List<MultiplayerLootRecord>(); foreach (var item in ledger.Records.Values) array.Add(item.Copy());
        MultiplayerGameplayProtocol.Send(MultiplayerGameplayProtocol.LootSnapshot, new MultiplayerLootMessage { records = array.ToArray() }, true, actor);
    }
    public void Settle(Action<bool> completion)
    {
        if (!MultiplayerGameplayProtocol.Host || Settling) return;
        onSettled = completion; BeginSettlement(Guid.NewGuid().ToString("N"));
        MultiplayerGameplayProtocol.Send(MultiplayerGameplayProtocol.Settlement, new MultiplayerLootMessage { op = "prepare", barrier = barrier }, true);
    }
    private void BeginSettlement(string token)
    {
        Settling = true; barrier = token; lastBarrier = token; settlementAcks.Clear(); settlementAckSent = false;
        barrierDeadline = Time.realtimeSinceStartupAsDouble + 60d;
        world.Inventory.CloseForTransition(); Scan();
    }
    private void Update()
    {
        if (!initialized || player == null || MultiplayerRunSession.Instance == null || !MultiplayerRunSession.Instance.IsRunning || !MultiplayerRunSession.Instance.IsInMultiplayerScene) return;
        double now = Time.realtimeSinceStartupAsDouble;
        MultiplayerPlayers.TrafficRates traffic = world.Traffic;
        if (now - lastScan >= 1d / traffic.lootScanHz) { lastScan = now; Scan(); }
        foreach (var pair in new List<KeyValuePair<string, MultiplayerLootMessage>>(pending))
        {
            if (now - sentAt[pair.Key] < 1d / traffic.lootRetryHz) continue;
            sentAt[pair.Key] = now;
            if (!MultiplayerGameplayProtocol.Host) MultiplayerGameplayProtocol.Send(MultiplayerGameplayProtocol.LootRequest, pair.Value, true, MultiplayerGameplayProtocol.HostActor);
        }
        if (!MultiplayerGameplayProtocol.Host && now - lastSnapshot >= 1d / traffic.lootSnapshotRequestHz)
        {
            lastSnapshot = now;
            MultiplayerGameplayProtocol.Send(MultiplayerGameplayProtocol.LootRequest, new MultiplayerLootMessage { op = "snapshot" }, true, MultiplayerGameplayProtocol.HostActor);
        }
        if (MultiplayerGameplayProtocol.Host)
        {
            foreach (var item in new List<MultiplayerLootRecord>(ledger.Records.Values))
                if (item.owner > 0 && !MultiplayerGameplayProtocol.Participant(item.owner) && departed.Add(item.owner))
                {
                    if (!world.TryLastPosition(item.owner, out Vector3 position)) position = item.position;
                    ledger.Release(item.owner, position, MultiplayerRunSession.Instance.FloorNumber);
                    foreach (var released in ledger.Records.Values) if (released.location == MultiplayerItemLocation.World)
                        Confirm(0, null, released.Copy());
                }
        }
        if (!Settling) return;
        if (now > barrierDeadline) { onSettled?.Invoke(false); onSettled = null; MultiplayerRunSession.Instance.ReportBuildFailed("Se agoto la confirmacion del inventario antes de cambiar de piso."); return; }
        if (pending.Count == 0 && !settlementAckSent)
        {
            settlementAckSent = true;
            if (MultiplayerGameplayProtocol.Host) settlementAcks.Add(localActor);
            else MultiplayerGameplayProtocol.Send(MultiplayerGameplayProtocol.Settlement,
                new MultiplayerLootMessage { op = "ack", barrier = barrier }, true, MultiplayerGameplayProtocol.HostActor);
        }
        if (MultiplayerGameplayProtocol.Host && onSettled != null)
        {
            foreach (int actor in MultiplayerGameplayProtocol.Participants()) if (!settlementAcks.Contains(actor)) return;
            var done = onSettled; onSettled = null; done(true);
        }
    }
    public void OnEvent(EventData ev)
    {
        if (!initialized || !MultiplayerGameplayProtocol.Read(ev, out MultiplayerLootMessage message)) return;
        if (ev.Code == MultiplayerGameplayProtocol.LootRequest && MultiplayerGameplayProtocol.Host) Process(ev.Sender, message);
        else if (ev.Code == MultiplayerGameplayProtocol.LootState && ev.Sender == MultiplayerGameplayProtocol.HostActor && !MultiplayerGameplayProtocol.Host) Apply(message);
        else if (ev.Code == MultiplayerGameplayProtocol.LootSnapshot && ev.Sender == MultiplayerGameplayProtocol.HostActor && !MultiplayerGameplayProtocol.Host && message.records != null && message.records.Length <= 4096)
        { foreach (var record in message.records) if (record != null) Apply(new MultiplayerLootMessage { record = record }); }
        else if (ev.Code == MultiplayerGameplayProtocol.Settlement)
        {
            if (ev.Sender == MultiplayerGameplayProtocol.HostActor && message.op == "prepare" && !string.IsNullOrEmpty(message.barrier) && message.barrier != lastBarrier) BeginSettlement(message.barrier);
            else if (MultiplayerGameplayProtocol.Host && message.op == "ack" && message.barrier == barrier) settlementAcks.Add(ev.Sender);
        }
    }
    public void Shutdown()
    {
        if (!initialized) return;
        foreach (var pair in new List<KeyValuePair<string, InventoryObject>>(copies))
        {
            InventoryObject item = pair.Value; if (item == null) continue;
            // Photon ya puede haber restablecido ActorNumber al notificar la salida.
            // Solo conservar adquisiciones confirmadas; una reserva pendiente se devuelve en el host.
            bool confirmed = records.TryGetValue(pair.Key, out var record) && record.owner == localActor && record.location == MultiplayerItemLocation.Inventory;
            bool original = imported.Contains(pair.Key) && record == null;
            if (player != null && player.Inventory.Inventory.Contains(item.gameObject) && (confirmed || original))
            { var identity = item.GetComponent<MultiplayerItemIdentity>(); if (identity != null) { identity.Run = null; if (Application.isPlaying) Destroy(identity); else DestroyImmediate(identity); } }
            else if (player != null) world.Inventory.Revoke(item, true);
        }
        initialized = false; pending.Clear(); copies.Clear(); records.Clear(); claimRequest = claimId = null; onSettled = null;
    }
}
