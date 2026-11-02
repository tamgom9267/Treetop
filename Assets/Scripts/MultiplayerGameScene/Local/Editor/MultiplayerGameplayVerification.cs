#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class MultiplayerGameplayVerification
{
    private static readonly List<string> passed = new List<string>();
    [Serializable] private sealed class Report { public bool success; public string error; public string[] checks; }
    [MenuItem("Tools/Multiplayer/Probar fase 4 (sin red)")]
    public static void RunChecks()
    {
        passed.Clear();
        try
        {
            MultiplayerGameplayPrefabBuilder.ValidatePrefabs(); Check(true, "22 cuartos y 4 slimes configurados por rol");
            CheckWorldSerialization(); CheckPresence(); CheckAttacks(); CheckLoot(); CheckRooms(); CheckInventory();
            MultiplayerPlayerVerification.RunChecks(); Check(true, "Regresion de las pruebas nativas de fase 3");
            Write(true, null); Debug.Log("FASE 4: " + passed.Count + " comprobaciones correctas. Temp/phase4-verification/editor-tests.json");
        }
        catch (Exception error) { Write(false, error.ToString()); throw; }
    }
    private static void CheckWorldSerialization()
    {
        bool depthWarning = false;
        Application.LogCallback capture = (message, trace, type) =>
        { if (message.IndexOf("Serialization depth limit", StringComparison.OrdinalIgnoreCase) >= 0) depthWarning = true; };
        Application.logMessageReceived += capture;
        try
        {
            var presence = new MultiplayerWorldMessage { op = "presence", room = "2", inside = true, position = new Vector3(1,2,3) };
            var decoded = JsonUtility.FromJson<MultiplayerWorldMessage>(JsonUtility.ToJson(presence));
            Check(decoded.op == "presence" && decoded.room == "2" && decoded.inside && decoded.position == presence.position,
                "Mensaje individual conserva presencia y posicion al serializar");
            var enemy = new MultiplayerWorldMessage { op = "enemy", id = "2:0", room = "2", type = 1,
                revision = 7, health = 5, hit = 1, position = new Vector3(4,5,6), velocity = Vector3.up, rotation = Quaternion.Euler(0,90,0) };
            var frames = JsonUtility.FromJson<MultiplayerWorldBatch>(JsonUtility.ToJson(
                new MultiplayerWorldBatch { op = "frames", entries = new[] { enemy } }));
            Check(frames.op == "frames" && frames.entries.Length == 1 && frames.entries[0].id == enemy.id &&
                frames.entries[0].health == 5 && frames.entries[0].position == enemy.position &&
                frames.entries[0].velocity == enemy.velocity && Quaternion.Angle(frames.entries[0].rotation, enemy.rotation) < 0.01f,
                "Grupo de movimiento conserva identidad, salud y transformacion del slime");
            var snapshot = JsonUtility.FromJson<MultiplayerWorldBatch>(JsonUtility.ToJson(new MultiplayerWorldBatch
            { op = "snapshot", entries = new[] { new MultiplayerWorldMessage { op = "room", room = "2", stage = 2, revision = 8 },
                enemy, new MultiplayerWorldMessage { op = "remove", id = "2:1", revision = 9 } } }));
            Check(snapshot.entries.Length == 3 && snapshot.entries[0].stage == 2 && snapshot.entries[1].hit == 1 &&
                snapshot.entries[2].op == "remove" && snapshot.entries[2].id == "2:1",
                "Snapshot conserva cuartos, enemigos y eliminaciones en un solo nivel");
            var empty = JsonUtility.FromJson<MultiplayerWorldBatch>(JsonUtility.ToJson(
                new MultiplayerWorldBatch { op = "snapshot", entries = Array.Empty<MultiplayerWorldMessage>() }));
            Check(empty.entries != null && empty.entries.Length == 0, "Snapshot vacio conserva un grupo vacio");
            Check(!depthWarning, "Serializacion de mensajes y grupos no excede el limite de profundidad de Unity");
        }
        finally { Application.logMessageReceived -= capture; }
    }
    private static void CheckPresence()
    {
        var presence = new MultiplayerRoomPresence();
        Check(!presence.All(new[] {1,2}, 0), "Un cuarto vacio espera al grupo");
        presence.Set(1,true,1); presence.Set(1,true,1);
        Check(!presence.All(new[] {1,2},1), "Varios colliders de uno no equivalen a dos jugadores");
        presence.Set(2,true,1); Check(presence.All(new[] {1,2},1), "Dos participantes presentes activan");
        Check(!presence.All(new[] {1,2,3},1), "Con tres participantes espera al tercero");
        presence.Set(3,true,1); Check(presence.All(new[] {1,2,3},1), "Tres participantes presentes activan");
        presence.Set(2,false,1); Check(!presence.All(new[] {1,2,3},1), "Salir antes de activar retira presencia");
        Check(presence.All(new[] {1,3},1), "Desconectado deja de contar entre los participantes");
        Check(!presence.All(new[] {1,3},3), "Presencia obsoleta expira");
        Check(!presence.All(new[] {1},1), "El host solo no activa un cuarto");
        Check(!MultiplayerGameplayProtocol.Finite(new Vector3(float.NaN,0,0)) && !MultiplayerGameplayProtocol.Rotation(new Quaternion()), "Datos de transformacion invalidos rechazados");
    }
    private static void CheckAttacks()
    {
        var first = new MultiplayerAttackWindow(1,5,2);
        var other = new MultiplayerAttackWindow(1,5,2);
        Check(first.TryHit(1,"enemy",1), "Un ataque valido golpea una vez");
        Check(!first.TryHit(1,"enemy",1), "Impacto duplicado rechazado");
        Check(first.TryHit(1,"second",1), "Un ataque puede alcanzar otro enemigo");
        Check(other.TryHit(1,"enemy",1), "Otro jugador puede golpear al mismo slime");
        Check(!first.TryHit(2,"third",1), "Identificador de ataque incorrecto rechazado");
        Check(!first.TryHit(1,"third",3), "Ataque vencido rechazado");
    }
    private static MultiplayerItemData Data() => new MultiplayerItemData { name="Sword", shape=new[] { new MultiplayerItemRow { cells=new[] {1,0} }, new MultiplayerItemRow { cells=new[] {1,0} } } };
    private static MultiplayerLootMessage Command(string op, string request, MultiplayerLootRecord record) => new MultiplayerLootMessage {op=op,request=request,record=record.Copy()};
    private static void CheckLoot()
    {
        var ledger = new MultiplayerLootLedger();
        var record = new MultiplayerLootRecord {id="one",type="sword",data=Data(),location=MultiplayerItemLocation.World};
        var current = ledger.Process(1,Command("register","r",record),1).Copy();
        Check(current.owner==0 && current.revision>0, "Loot registrado con revision e identidad de red");
        var stale = current.Copy();
        current = ledger.Process(2,Command("claim","first",current),1).Copy();
        Check(current.owner==2 && current.location==MultiplayerItemLocation.Reserved, "Primera solicitud reserva exclusivamente");
        current = ledger.Process(3,Command("claim","second",stale),1).Copy();
        Check(current.owner==2, "Recogida simultanea perdedora no cambia propietario");
        int revision=current.revision;
        Check(ledger.Process(2,Command("claim","first",stale),1).revision==revision, "Reintento de la misma solicitud es idempotente");
        Check(ledger.Process(3,Command("drop","invalid",current),1).owner==2, "Otro actor no puede tirar un objeto reservado");
        current = ledger.Process(2,Command("store","store",current),1).Copy();
        Check(current.location==MultiplayerItemLocation.Inventory, "Guardar confirma el inventario del ganador");
        current.position=new Vector3(3,0,4); current=ledger.Process(2,Command("drop","drop",current),1).Copy();
        Check(current.owner==0 && current.position==new Vector3(3,0,4), "Tirar conserva identidad y posicion");
        var latest = ledger.Process(3,Command("claim","third",current),1).Copy();
        Check(latest.owner==3, "Un objeto devuelto puede adjudicarse a otro jugador");
        Check(ledger.Process(2,Command("claim","first",stale),1).owner==3, "Una solicitud historica no vuelve a adjudicar");
        ledger.Release(3,new Vector3(7,0,0),1); current=ledger.Records["one"].Copy();
        Check(current.owner==0 && current.position.x==7, "Desconexion libera una reserva");
        var held = new MultiplayerLootRecord {id="two",type="sword",data=Data(),location=MultiplayerItemLocation.Inventory};
        ledger.Process(2,Command("register","held",held),1);
        ledger.NewFloor(2);
        Check(ledger.Records["one"].location==MultiplayerItemLocation.Removed && ledger.Records["two"].owner==2, "Cambio de piso retira suelo y conserva inventario");
        Check(ledger.Process(3,Command("claim","late",current),2).location==MultiplayerItemLocation.Removed, "Un paquete viejo no recupera loot del piso anterior");
        var corrupt=record.Copy();corrupt.id="bad";corrupt.data.shape[0].cells[0]=9;
        Check(ledger.Process(1,Command("register","bad",corrupt),1)==null, "Forma de inventario invalida rechazada");
    }
    private static void CheckRooms()
    {
        var preview=EditorSceneManager.NewPreviewScene();
        try
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(MultiplayerGameplayPrefabBuilder.Root+"/Host/Cuartos/0.prefab");
            var root=(GameObject)PrefabUtility.InstantiatePrefab(prefab,preview);
            var room=root.GetComponent<MultiplayerRoomController>();
            typeof(MultiplayerRoomController).GetMethod("Awake",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(room,null);
            var avatar=new GameObject("Avatar",typeof(CapsuleCollider),typeof(PlayerController));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(avatar,preview);
            avatar.transform.position=root.transform.position;
            Physics.SyncTransforms();
            Check(room.ContainsLocal(avatar.GetComponent<PlayerController>()),"Piso detecta el collider real del personaje");
            avatar.transform.position+=Vector3.right*100;Physics.SyncTransforms();
            Check(!room.ContainsLocal(avatar.GetComponent<PlayerController>()),"Salir del piso retira al personaje real");
            var entry=new GameObject("Entry");var exit=new GameObject("Exit");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(entry,preview);UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(exit,preview);
            room.enterDoor=entry;room.exitDoor=exit;
            room.ApplyState(1,1);Check(entry.activeSelf && exit.activeSelf,"Activacion cierra entrada y salida");
            room.ApplyState(2,2);Check(!entry.activeSelf && !exit.activeSelf,"Completar abre ambas puertas");
            room.ApplyState(1,1);Check(room.Stage==2 && !entry.activeSelf,"Un mensaje anterior no reactiva el cuarto");
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
    }
    private static void CheckInventory()
    {
        var preview=EditorSceneManager.OpenPreviewScene("Assets/Scenes/VillageMenuScene.unity");
        try
        {
            PlayerController player=null;
            foreach(var root in preview.GetRootGameObjects()) { player=root.GetComponentInChildren<PlayerController>(true);if(player!=null)break; }
            PlayerInventory inventory=player.Inventory;
            inventory.AssignPlayer(player);
            inventory.grid=new List<List<int>> {new List<int>{7,0},new List<int>{7,0}};
            inventory.lootgrid=new List<List<int>> {new List<int>{7,0},new List<int>{7,0},new List<int>{0,0},new List<int>{0,0}};
            var serialized=new SerializedObject(inventory);
            var cursorPrefab=(GameObject)serialized.FindProperty("cursorPrefab").objectReferenceValue;
            inventory.cursor=Object.Instantiate(cursorPrefab,inventory.transform);
            inventory.cursor.GetComponent<CursorObject>().Inventory=inventory;
            var coordinator=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(MultiplayerGameplayPrefabBuilder.Root+"/Local/MultiplayerPlayers.prefab"),preview);
            var adapter=coordinator.GetComponent<MultiplayerInventoryAdapter>();
            var duration=typeof(PlayerController).GetField("receivedHitStopDuration",BindingFlags.Instance|BindingFlags.NonPublic);
            float before=(float)duration.GetValue(player);
            var world=coordinator.GetComponent<MultiplayerWorldSession>();
            Check(world.Initialize(null,coordinator.GetComponent<MultiplayerPlayers>(),out _),"Coordinador de gameplay valida sus dependencias");
            Check(adapter.Bind(world,player,out string error),"Adaptador enlaza sin cambiar scripts originales: "+error);
            Check((float)duration.GetValue(player)==0,"Hit stop recibido desactivado temporalmente");
            var item=new GameObject("Item",typeof(InventoryObject)).GetComponent<InventoryObject>();
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(item.gameObject,preview);item.transform.SetParent(player.transform);item.ID=7;Data().Apply(item);
            inventory.Inventory.Add(item.gameObject);inventory.Loot.Add(item.gameObject);inventory.SelectedObject=inventory.LootObject=item;
            player.Slot1=player.Slot2=player.Slot3=player.SelectedItem=player.nearestWeaponObj=item;player.nearestWeapon=item.gameObject;player.nearWeapons.Add(item.gameObject);
            var image=new GameObject("Image",typeof(RectTransform),typeof(PanelManager));image.transform.SetParent(inventory.transform);
            image.GetComponent<PanelManager>().itemObj=item.gameObject;
            inventory.cursor.GetComponent<CursorObject>().InventoryItemObj=image;
            var moving=typeof(PlayerInventory).GetField("isMoving",BindingFlags.Instance|BindingFlags.NonPublic);
            var drag=typeof(PlayerInventory).GetField("SpriteObject",BindingFlags.Instance|BindingFlags.NonPublic);
            moving.SetValue(inventory,true);drag.SetValue(inventory,image);
            adapter.Revoke(item,false);
            Check(item!=null && inventory.Inventory.Count==0 && inventory.Loot.Count==0,"Referencia retirada antes de destruir el objeto real");
            Check(inventory.grid[0][0]==0 && inventory.lootgrid[1][0]==0,"Celdas de ambas cuadrillas liberadas");
            Check(player.Slot1==null && player.Slot2==null && player.Slot3==null && player.SelectedItem==null && player.nearestWeapon==null && player.nearWeapons.Count==0,"Slots, seleccion y proximidad limpiados");
            Check(inventory.SelectedObject==null && inventory.LootObject==null && inventory.cursor.GetComponent<CursorObject>().InventoryItemObj==null,"Seleccion de inventario y cursor limpiados");
            Check(!(bool)moving.GetValue(inventory) && drag.GetValue(inventory)==null && (image==null || image.GetComponent<PanelManager>().itemObj==null),"Arrastre y panel desreferenciados antes de eliminar");
            MultiplayerInventoryAdapter.RotateShape(item);
            Check(item.requieredCells.Count==1 && item.requieredCells[0].Count==2,"Forma rotada se normaliza sin depender de WeaponObject");
            adapter.Revoke(item,false);Check(inventory.AvailableIDs.FindAll(x=>x==7).Count==1,"Limpieza repetida devuelve el ID solo una vez");
            var loot=coordinator.GetComponent<MultiplayerLootSession>();
            typeof(MultiplayerLootSession).GetField("player",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(loot,player);
            typeof(MultiplayerLootSession).GetField("localActor",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(loot,2);
            var copies=(Dictionary<string,InventoryObject>)typeof(MultiplayerLootSession).GetField("copies",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(loot);
            var records=(Dictionary<string,MultiplayerLootRecord>)typeof(MultiplayerLootSession).GetField("records",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(loot);
            var confirmed=new GameObject("Confirmed",typeof(InventoryObject)).GetComponent<InventoryObject>();
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(confirmed.gameObject,preview);confirmed.transform.SetParent(player.transform);
            confirmed.gameObject.AddComponent<MultiplayerItemIdentity>().Id="confirmed";Data().Apply(confirmed);
            inventory.Inventory.Add(confirmed.gameObject);copies["confirmed"]=confirmed;
            records["confirmed"]=new MultiplayerLootRecord {id="confirmed",owner=2,location=MultiplayerItemLocation.Inventory};
            var reserved=new GameObject("Reserved",typeof(InventoryObject)).GetComponent<InventoryObject>();
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(reserved.gameObject,preview);reserved.transform.SetParent(player.transform);
            reserved.gameObject.AddComponent<MultiplayerItemIdentity>().Id="reserved";Data().Apply(reserved);
            inventory.Inventory.Add(reserved.gameObject);copies["reserved"]=reserved;
            records["reserved"]=new MultiplayerLootRecord {id="reserved",owner=2,location=MultiplayerItemLocation.Reserved};
            loot.Shutdown();
            Check(confirmed!=null && inventory.Inventory.Contains(confirmed.gameObject),"Desconexion conserva propiedad confirmada aun sin ActorNumber de Photon");
            Check(reserved==null && inventory.Inventory.Count==1,"Reserva sin guardar confirmado no se duplica al desconectarse");
            adapter.Shutdown();Check((float)duration.GetValue(player)==before,"Configuracion de hit stop del solitario restaurada");
        }
        finally {EditorSceneManager.ClosePreviewScene(preview);}
    }
    private static void Check(bool condition,string message) {if(!condition)throw new InvalidOperationException("FASE 4: "+message);passed.Add(message);}
    private static void Write(bool success,string error)
    {Directory.CreateDirectory("Temp/phase4-verification");File.WriteAllText("Temp/phase4-verification/editor-tests.json",JsonUtility.ToJson(new Report{success=success,error=error,checks=passed.ToArray()},true));}
}
#endif
