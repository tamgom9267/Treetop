#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Regresion de las armas importadas desde la aldea, sin agregar loot a las escenas.</summary>
public static class MultiplayerWeaponVerification
{
    private static readonly List<string> checks = new List<string>();
    [Serializable] private sealed class Report { public bool success; public string error; public string[] checks; }

    [MenuItem("Tools/Multiplayer/Probar catalogos y armas de la aldea")]
    public static void RunChecks()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Sal de Play para comprobar las armas.");
        checks.Clear();
        try
        {
            var sources = Snapshot("Assets", path => !path.Replace('\\', '/').StartsWith(MultiplayerPlayerPrefabBuilder.Root + "/", StringComparison.Ordinal));
            MultiplayerPlayerPrefabBuilder.CreateMissingPrefabs();
            var generated = Snapshot(MultiplayerPlayerPrefabBuilder.Root, _ => true);
            MultiplayerPlayerPrefabBuilder.CreateMissingPrefabs();
            MultiplayerGameplayPrefabBuilder.UpdateLootCatalog();
            Check(Same(generated, Snapshot(MultiplayerPlayerPrefabBuilder.Root, _ => true)), "Actualizar dos veces no modifica ni duplica assets generados");
            Check(Same(sources, Snapshot("Assets", path => !path.Replace('\\', '/').StartsWith(MultiplayerPlayerPrefabBuilder.Root + "/", StringComparison.Ordinal))),
                "Escenas, animaciones y prefabs originales permanecen intactos");
            CheckCatalogsAndDrops();
            CheckInventoryShapes();
            MultiplayerGameplayVerification.RunChecks();
            Write("editor", true, null);
            Debug.Log("Armas multijugador: " + checks.Count + " comprobaciones correctas.");
        }
        catch (Exception error) { Write("editor", false, error.ToString()); throw; }
    }

    public static void RunBatch() { RunChecks(); }
    public static void RunCombatBatch() { RunChecks(); MultiplayerCombatVerification.RunChecks(); }

    private static void CheckCatalogsAndDrops()
    {
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var coordinator = AssetDatabase.LoadAssetAtPath<GameObject>(MultiplayerPlayerPrefabBuilder.LocalPath);
            var serialized = new SerializedObject(coordinator.GetComponent<MultiplayerLootSession>());
            var catalog = serialized.FindProperty("catalog");
            foreach (string type in MultiplayerPlayerPrefabBuilder.WeaponTypes)
            {
                ObjectData data = MultiplayerPlayerPrefabBuilder.WeaponData(type);
                var matches = new List<SerializedProperty>();
                for (int i = 0; i < catalog.arraySize; i++)
                    if (catalog.GetArrayElementAtIndex(i).FindPropertyRelative("weapon").objectReferenceValue == data)
                        matches.Add(catalog.GetArrayElementAtIndex(i));
                Check(matches.Count == 1 && matches[0].FindPropertyRelative("type").stringValue == type, type + ": un registro de loot con tipo estable");
                var prefab = matches[0].FindPropertyRelative("prefab").objectReferenceValue as GameObject;
                Check(prefab != null && prefab.GetComponent<MultiplayerItemIdentity>() != null && prefab.GetComponent<WeaponObject>().weaponData == data,
                    type + ": prefab de loot enlaza datos e identidad");
                var item = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, scene)).GetComponent<WeaponObject>();
                Call(item, "Awake"); // En preview no se ejecuta el ciclo de vida de gameplay.
                Check(item.GetComponentsInChildren<InventoryObject>(true).Count(MultiplayerLootSession.IsRootItem) == 1,
                    type + ": solo la raiz puede registrarse al soltar el arma");
                var source = data.prefab.GetComponent<BoxCollider>();
                var collider = item.GetComponent<BoxCollider>();
                Check(collider != null && source != null && collider.size == source.size && collider.center == source.center,
                    type + ": collider de loot conserva su geometria");
                var visual = AssetDatabase.LoadAssetAtPath<GameObject>(MultiplayerPlayerPrefabBuilder.VisualPath(type));
                Check(visual.transform.localScale == data.prefab.transform.localScale &&
                    Quaternion.Angle(visual.transform.localRotation, data.prefab.transform.localRotation) < 0.01f,
                    type + ": visual conserva escala y orientacion originales");
                var original = MultiplayerItemData.Capture(item);
                Check(original.Valid() && original.wd == data.WD && original.shape.Length == data.requiredCells.Count / 2,
                    type + ": estadisticas y forma originales validas");
                MultiplayerInventoryAdapter.RotateShape(item);
                var rotated = MultiplayerItemData.Capture(item);
                var ledger = new MultiplayerLootLedger();
                var record = new MultiplayerLootRecord { id = type, type = type, data = original,
                    location = MultiplayerItemLocation.Inventory, rotation = Quaternion.identity };
                record = Command(ledger, 1, "register", record);
                Check(record.owner == 1 && record.location == MultiplayerItemLocation.Inventory, type + ": importacion desde la aldea");
                record.data = rotated;
                record = Command(ledger, 1, "drop", record);
                var available = record.Copy();
                record = Command(ledger, 2, "claim", record);
                var contested = Command(ledger, 3, "claim", available);
                Check(contested.owner == 2 && record.location == MultiplayerItemLocation.Reserved, type + ": recogida simultanea conserva un solo propietario");
                record = Command(ledger, 2, "store", record);
                record = Command(ledger, 2, "drop", record);
                record = Command(ledger, 1, "claim", record);
                record = Command(ledger, 1, "store", record);
                ledger.NewFloor(2);
                Check(record.owner == 1 && record.location == MultiplayerItemLocation.Inventory && ledger.Records.Count == 1,
                    type + ": intercambio y cambio de piso conservan identidad");
                record.data.Apply(item);
                Check(JsonUtility.ToJson(MultiplayerItemData.Capture(item)) == JsonUtility.ToJson(rotated),
                    type + ": soltar y recoger conserva forma rotada y estadisticas");
                var packed = MultiplayerPlayerProtocol.PackAttack("weapons", 1, new MultiplayerPlayerProtocol.Attack
                { pose = new MultiplayerPlayerProtocol.Pose { sequence = 1, time = 1d, rotation = Quaternion.identity, playbackRate = 1f },
                    item = new MultiplayerPlayerProtocol.Item(1, type) });
                Check(MultiplayerPlayerProtocol.TryReadAttack(packed, out var attack) && attack.item.type == type,
                    type + ": protocolo actual conserva el tipo de ataque");
            }
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    // Ejecutado por la regresion de combate: atraviesa Scan, Reconcile y el adaptador reales.
    public static void CheckPlayMode(MultiplayerWorldSession world)
    {
        checks.Clear();
        var root = new GameObject("Weapon verification player"); root.SetActive(false);
        var player = root.AddComponent<PlayerController>(); player.enabled = false;
        var inventory = root.AddComponent<PlayerInventory>(); player.Inventory = inventory;
        inventory.AssignPlayer(player);
        var hand = new GameObject("Hand"); hand.transform.SetParent(root.transform);
        Set(player, "handObj", hand.transform);
        var loot = world.Loot;
        Set(loot, "player", player); Set(world, "player", player);
        try
        {
            foreach (string type in MultiplayerPlayerPrefabBuilder.WeaponTypes)
            {
                CheckAnimationEvents(type);
                var item = Object.Instantiate(MultiplayerPlayerPrefabBuilder.WeaponData(type).prefab).GetComponent<WeaponObject>();
                item.transform.SetParent(root.transform); item.gameObject.SetActive(false);
                inventory.Inventory.Add(item.gameObject);
                player.weapon = item.gameObject; player.equipWeapon();
                Call(loot, "Scan");
                var identity = item.GetComponent<MultiplayerItemIdentity>();
                Check(identity != null && loot.CanUse(item.gameObject), type + ": equipada desde aldea registra propiedad utilizable");
                var records = (Dictionary<string, MultiplayerLootRecord>)Get(loot, "records");
                string id = identity.Id;
                var data = MultiplayerItemData.Capture(item);
                player.EndAttack(); player.UnequipWeapon(); inventory.Inventory.Remove(item.gameObject);
                item.transform.SetParent(null); item.gameObject.SetActive(true);
                Call(loot, "Scan");
                Check(records[id].location == MultiplayerItemLocation.World && records.Values.Count(x => x.type == type) == 1,
                    type + ": soltar crea un unico registro de mundo");
                Check(item.GetComponentsInChildren<MultiplayerItemIdentity>(true).Length == 1,
                    type + ": escaneo real no asigna identidades a los modelos hijos");
                // Un adaptador sin espacio/enlace rechaza inspeccionar y devuelve el objeto al mundo.
                Check(loot.TryClaim(item.gameObject) && records[id].location == MultiplayerItemLocation.World,
                    type + ": adaptador no disponible devuelve la reserva sin perder el objeto");
                Check(JsonUtility.ToJson(data) == JsonUtility.ToJson(MultiplayerItemData.Capture(item)), type + ": reserva rechazada conserva datos");
                Object.DestroyImmediate(item.gameObject);
            }
            Write("play", true, null);
        }
        catch (Exception error) { Write("play", false, error.ToString()); throw; }
        finally { Set(loot, "player", null); Set(world, "player", null); Object.DestroyImmediate(root); }
    }

    private static void CheckInventoryShapes()
    {
        var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/VillageMenuScene.unity");
        try
        {
            var player = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<PlayerController>(true)).First();
            var inventory = player.Inventory; inventory.AssignPlayer(player);
            inventory.grid = new List<List<int>> { new List<int> { 0, 0 }, new List<int> { 0, 0 }, new List<int> { 0, 0 }, new List<int> { 0, 0 } };
            inventory.lootgrid = inventory.grid.Select(row => new List<int>(row)).ToList();
            inventory.Inventory.Clear(); inventory.Loot.Clear(); inventory.AvailableIDs.Clear(); inventory.AvailableIDs.Add(1);
            var settings = new SerializedObject(inventory);
            inventory.cursor = Object.Instantiate((GameObject)settings.FindProperty("cursorPrefab").objectReferenceValue, inventory.transform);
            inventory.cursor.GetComponent<CursorObject>().Inventory = inventory;
            var slots = (GameObject)settings.FindProperty("ChestLoot").objectReferenceValue;
            var slot = new GameObject("Weapon test slot", typeof(RectTransform), typeof(PanelManager));
            slot.transform.SetParent(slots.transform, false); slot.GetComponent<PanelManager>().location = Vector2Int.zero;
            var coordinator = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(MultiplayerPlayerPrefabBuilder.LocalPath), scene);
            var world = coordinator.GetComponent<MultiplayerWorldSession>();
            Check(world.Initialize(null, coordinator.GetComponent<MultiplayerPlayers>(), out _), "Inventario real: coordinador valido");
            Check(world.Inventory.Bind(world, player, out _), "Inventario real: adaptador enlazado a UI original");
            foreach (string type in MultiplayerPlayerPrefabBuilder.WeaponTypes)
            {
                var item = ((GameObject)PrefabUtility.InstantiatePrefab(MultiplayerPlayerPrefabBuilder.WeaponData(type).prefab, scene)).GetComponent<WeaponObject>();
                Call(item, "Awake");
                MultiplayerInventoryAdapter.RotateShape(item);
                Check(world.Inventory.Inspect(item, item.weaponData.UIsprite), type + ": arma rotada cabe en la ventana real de loot");
                Check(inventory.lootgrid.Sum(row => row.Count(cell => cell == item.ID)) == item.weaponData.requiredCells.Count(cell => cell == 1),
                    type + ": ocupa exactamente sus celdas de inventario");
                world.Inventory.Revoke(item, false);
                Check(inventory.Loot.Count == 0 && inventory.AvailableIDs.Count == 1 && inventory.lootgrid.All(row => row.All(cell => cell == 0)),
                    type + ": transferir libera celdas, panel e ID");
                foreach (var row in inventory.lootgrid) for (int x = 0; x < row.Count; x++) row[x] = 9;
                string before = JsonUtility.ToJson(MultiplayerItemData.Capture(item));
                Check(!world.Inventory.Inspect(item, item.weaponData.UIsprite) && inventory.Loot.Count == 0 &&
                    inventory.AvailableIDs.Count == 1 && before == JsonUtility.ToJson(MultiplayerItemData.Capture(item)),
                    type + ": sin espacio rechaza sin alterar forma, datos ni ID");
                foreach (var row in inventory.lootgrid) for (int x = 0; x < row.Count; x++) row[x] = 0;
                Object.DestroyImmediate(item.gameObject);
            }
            world.Inventory.Shutdown();
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    private static MultiplayerLootRecord Command(MultiplayerLootLedger ledger, int actor, string op, MultiplayerLootRecord record) =>
        ledger.Process(actor, new MultiplayerLootMessage { op = op, request = Guid.NewGuid().ToString("N"), record = record.Copy() }, 1).Copy();

    private static void CheckAnimationEvents(string type)
    {
        var remote = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(MultiplayerPlayerPrefabBuilder.RemotePath)).GetComponent<MultiplayerRemotePlayer>();
        try
        {
            remote.Initialize(new[] { new MultiplayerVisualDefinition { typeId = type,
                remotePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MultiplayerPlayerPrefabBuilder.VisualPath(type)).GetComponent<MultiplayerWeaponVisual>() } }, 0f);
            var pose = new MultiplayerPlayerProtocol.Pose { sequence = 1, time = PhotonNetwork.Time, rotation = Quaternion.identity, playbackRate = 1f };
            remote.ReceiveEquipment(new MultiplayerPlayerProtocol.Equipment { pose = pose, equippedId = 1,
                items = new[] { new MultiplayerPlayerProtocol.Item(1, type) } });
            remote.SetPlaying(true); remote.Animator.Update(0f);
            pose.sequence++;
            remote.ReceiveAttack(new MultiplayerPlayerProtocol.Attack { pose = pose, item = new MultiplayerPlayerProtocol.Item(2, type) });
            remote.RenderFrame(pose.time + 0.01d, 0f);
            bool finalEvent = false;
            for (int frame = 0; frame < 50; frame++)
            {
                remote.Animator.Update(0.02f);
                if (remote.Equipment.CachedCount == 1) finalEvent = true;
            }
            Check(finalEvent, type + ": Animation Event real termina la accion remota");
        }
        finally { Object.DestroyImmediate(remote.gameObject); }
    }

    public static IEnumerator CheckNetwork(LoadBalancingClient peer)
    {
        checks.Clear();
        bool success = false;
        var run = MultiplayerRunSession.Instance;
        bool wasEnabled = run.enabled;
        GameObject coordinator = null;
        MultiplayerLootRecord received = null;
        MultiplayerPlayerProtocol.Equipment equipment = null;
        MultiplayerPlayerProtocol.Attack? attack = null;
        Action<EventData> listener = ev =>
        {
            if (ev.Sender != MultiplayerGameplayProtocol.HostActor || !(ev.CustomData is object[] values)) return;
            if (ev.Code == MultiplayerGameplayProtocol.LootState && values.Length == 4 && values[3] is string json)
            {
                var message = JsonUtility.FromJson<MultiplayerLootMessage>(json);
                if (message.record != null && message.record.id.StartsWith("weapon-network:")) received = message.record;
            }
            if (ev.Code == MultiplayerPlayerProtocol.EquipmentEvent && MultiplayerPlayerProtocol.TryReadEquipment(values, out var state)) equipment = state;
            if (ev.Code == MultiplayerPlayerProtocol.AttackEvent && MultiplayerPlayerProtocol.TryReadAttack(values, out var action)) attack = action;
        };
        peer.EventReceived += listener;
        try
        {
            run.enabled = false;
            Property(run, "RunId", "44444444444444444444444444444444"); Property(run, "FloorNumber", 1);
            Property(run, "Phase", MultiplayerRunPhase.Playing); Set(run, "activeRun", true); Set(run, "localBuiltKey", run.CurrentKey);
            coordinator = Object.Instantiate(Resources.Load<GameObject>(MultiplayerPlayers.ResourcePath));
            var players = coordinator.GetComponent<MultiplayerPlayers>(); players.enabled = false;
            Check(players.Initialize(run, out string error), "Dos clientes: coordinador inicializa: " + error);
            var loot = players.World.Loot;
            int host = PhotonNetwork.LocalPlayer.ActorNumber, client = peer.LocalPlayer.ActorNumber;
            players.World.ObservePose(client, Vector3.zero);
            foreach (string type in MultiplayerPlayerPrefabBuilder.WeaponTypes)
            {
                var item = Object.Instantiate(MultiplayerPlayerPrefabBuilder.WeaponData(type).prefab).GetComponent<WeaponObject>();
                var data = MultiplayerItemData.Capture(item); Object.DestroyImmediate(item.gameObject);
                var record = new MultiplayerLootRecord { id = "weapon-network:" + type, type = type,
                    location = MultiplayerItemLocation.Inventory, data = data, rotation = Quaternion.identity };
                received = null;
                Call(loot, "Process", host, new MultiplayerLootMessage { op = "register", request = Guid.NewGuid().ToString("N"), record = record });
                yield return NetworkWait(peer, () => received != null && received.id == record.id && received.owner == host);
                Check(received.type == type && JsonUtility.ToJson(received.data) == JsonUtility.ToJson(data), type + ": importacion llega al segundo cliente con sus datos");
                Call(loot, "Process", host, new MultiplayerLootMessage { op = "drop", request = Guid.NewGuid().ToString("N"), record = received.Copy() });
                yield return NetworkWait(peer, () => received.location == MultiplayerItemLocation.World);
                foreach (string op in new[] { "claim", "store", "drop" })
                {
                    // El peer de prueba no tiene publicador de movimiento: mantener vigente su pose.
                    players.World.ObservePose(client, Vector3.zero);
                    int revision = received.revision;
                    var request = new MultiplayerLootMessage { op = op, request = Guid.NewGuid().ToString("N"), record = received.Copy() };
                    Check(peer.OpRaiseEvent(MultiplayerGameplayProtocol.LootRequest,
                        new object[] { MultiplayerGameplayProtocol.Version, run.RunId, 1, JsonUtility.ToJson(request) },
                        new RaiseEventOptions { TargetActors = new[] { host } }, SendOptions.SendReliable), type + ": cliente envia " + op);
                    yield return NetworkWait(peer, () => received.revision > revision);
                    Check(received.owner == (op == "drop" ? 0 : client), type + ": host confirma propiedad de " + op);
                }
                Check(JsonUtility.ToJson(received.data) == JsonUtility.ToJson(data), type + ": intercambio real conserva estadisticas y forma");
                equipment = null; attack = null;
                var pose = new MultiplayerPlayerProtocol.Pose { time = PhotonNetwork.Time, rotation = Quaternion.identity, playbackRate = 1f };
                Check(players.SendEquipment(new MultiplayerPlayerProtocol.Equipment { pose = pose, equippedId = 1,
                    items = new[] { new MultiplayerPlayerProtocol.Item(1, type) } }), type + ": envia equipo por Photon");
                Check(players.SendAttack(new MultiplayerPlayerProtocol.Attack { pose = pose, item = new MultiplayerPlayerProtocol.Item(1, type) }), type + ": envia ataque por Photon");
                yield return NetworkWait(peer, () => equipment != null && attack.HasValue);
                Check(equipment.items[0].type == type && attack.Value.item.type == type, type + ": equipo y ataque conservan tipo entre dos conexiones reales");
            }
            success = true;
        }
        finally
        {
            peer.EventReceived -= listener;
            if (coordinator != null) Object.DestroyImmediate(coordinator);
            Set(run, "activeRun", false); Set(run, "localBuiltKey", null); Property(run, "Phase", MultiplayerRunPhase.Idle);
            run.enabled = wasEnabled;
            Write("network", success, success ? null : "La prueba de red no termino; consulta el log de Unity.");
        }
    }

    private static IEnumerator NetworkWait(LoadBalancingClient peer, Func<bool> condition)
    {
        double deadline = Time.realtimeSinceStartupAsDouble + 20d;
        while (!condition())
        {
            peer.Service();
            PhotonNetwork.SendAllOutgoingCommands();
            if (Time.realtimeSinceStartupAsDouble >= deadline) throw new TimeoutException("Armas: espera de evento Photon");
            yield return null;
        }
    }

    private static Dictionary<string, string> Snapshot(string directory, Func<string, bool> include)
    {
        var result = new Dictionary<string, string>();
        using (var hash = SHA256.Create()) foreach (string path in Directory.GetFiles(directory, "*", SearchOption.AllDirectories).Where(include))
            result[path] = Convert.ToBase64String(hash.ComputeHash(File.ReadAllBytes(path)));
        return result;
    }
    private static bool Same(Dictionary<string, string> first, Dictionary<string, string> second) =>
        first.Count == second.Count && first.All(pair => second.TryGetValue(pair.Key, out string hash) && hash == pair.Value);
    private static void Check(bool value, string description) { if (!value) throw new InvalidOperationException(description); checks.Add(description); }
    private static object Get(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static void Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
    private static void Property(object target, string name, object value) => target.GetType().GetProperty(name).GetSetMethod(true).Invoke(target, new[] { value });
    private static void Write(string phase, bool success, string error)
    {
        Directory.CreateDirectory("Logs/weapon-verification");
        File.WriteAllText("Logs/weapon-verification/" + phase + ".json", JsonUtility.ToJson(new Report { success = success, error = error, checks = checks.ToArray() }, true));
    }
}
#endif
