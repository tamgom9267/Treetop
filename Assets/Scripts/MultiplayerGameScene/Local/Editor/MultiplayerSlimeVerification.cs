#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Object = UnityEngine.Object;

// Tres replicas con actores distintos y fisica real; el transporte se entrega en ordenes adversos.
public static class MultiplayerSlimeVerification
{
    private static readonly List<string> checks = new List<string>();
    [Serializable] private sealed class Report { public bool success; public string error; public string[] checks; }
    public static void BuildPlayerBatch()
    {
        var scenes = new List<string>();
        foreach (var scene in EditorBuildSettings.scenes) if (scene.enabled) scenes.Add(scene.path);
        const string output = "Builds/SlimeControlVerification/Treetop.exe";
        var report = BuildPipeline.BuildPlayer(scenes.ToArray(), output, BuildTarget.StandaloneWindows64, BuildOptions.Development);
        Directory.CreateDirectory("Logs/combat-verification");
        bool success = report.summary.result == BuildResult.Succeeded;
        File.WriteAllText("Logs/combat-verification/build.json", JsonUtility.ToJson(new Report { success = success,
            error = report.summary.result.ToString(), checks = new[] { output, "Errores: " + report.summary.totalErrors } }, true));
        Debug.Log("Build de verificacion de slimes: " + report.summary.result);
        if (Application.isBatchMode) EditorApplication.Exit(success ? 0 : 1);
    }
    public static IEnumerator CheckSlimes(MultiplayerWorldSession world)
    {
        checks.Clear(); bool success = false;
        float previousCaptureDelta = Time.captureDeltaTime;
        Time.captureDeltaTime = Time.fixedDeltaTime;
        try
        {
            for (int type = 1; type <= 2; type++)
            {
                var host = Spawn(type, true, world, 1);
                var a = Spawn(type, false, world, 2);
                var b = Spawn(type, false, world, 3);
                foreach (Collider collider in a.GetComponents<Collider>()) collider.enabled = !collider.isTrigger;
                var registry = (IDictionary)Get(world, "enemies"); registry.Add(host.Id, host);
                a.Receive(host.Capture(1)); b.Receive(host.Capture(1));
                Check(a.GetComponent<MeshRenderer>() != null && a.transform.Find("CombatVisual") == null, "Slime " + type + ": malla restaurada en raiz");
                Vector3 origin = a.transform.position - Vector3.right * 3f;
                Check(a.BeginLocalKnockback(2, 10, origin) && !a.BeginLocalKnockback(2, 10, origin), "Impulso provisional unico por golpe");
                Check(!a.GetComponent<Rigidbody>().isKinematic && a.GetComponent<Rigidbody>().useGravity && a.Health == 10, "Cliente activa fisica sin modificar vida");
                Vector3 before = a.GetComponent<Rigidbody>().position;
                yield return null; yield return null;
                var body = a.GetComponent<Rigidbody>();
                Check(body.position.x > before.x && body.linearVelocity.x > 0f, "AddForce mueve el Rigidbody real del atacante: " +
                    before + " -> " + body.position + "; velocidad=" + body.linearVelocity + "; simula=" + a.SimulatesMotion +
                    "; dt=" + Time.deltaTime + "; fixed=" + Time.fixedTimeAsDouble + "; scale=" + Time.timeScale);
                host.TakeAuthoritativeHit(1, origin, 2, 10); var grant = host.Capture(2);
                before = body.position; Vector3 velocity = body.linearVelocity;
                a.Receive(grant); b.Receive(grant);
                Check(a.SimulatesMotion && body.position == before && body.linearVelocity == velocity, "Concesion preserva posicion y fuerza ya aplicadas");
                Check(!host.SimulatesMotion && !host.CanRunHostAI && host.GetComponent<Rigidbody>().isKinematic, "Host pausa su fisica e IA al delegar");
                var frame = a.CaptureMotion();
                Check(host.ReceiveMotion(2, frame) && b.ReceiveMotion(2, frame), "Atacante entrega movimiento directamente a host y observador");
                Check(host.MotionPosition == frame.position, "Validacion usa ultima posicion recibida, no la interpolada");
                Check(!host.ReceiveMotion(2, frame) && !host.ReceiveMotion(3, frame), "Secuencia duplicada y remitente incorrecto se rechazan");
                var invalid = a.CaptureMotion(); invalid.position.x = float.NaN;
                Check(!host.ReceiveMotion(2, invalid), "Movimiento no finito se rechaza");
                var dto = JsonUtility.FromJson<MultiplayerSlimeMotion>(JsonUtility.ToJson(frame));
                Check(dto.actor == 2 && dto.attack == 10 && dto.sequence == frame.sequence && dto.position == frame.position,
                    "Contrato de movimiento conserva tramo y fisica al serializar");

                Check(b.BeginLocalKnockback(3, 20, origin), "Segundo atacante empieza inmediatamente");
                host.TakeAuthoritativeHit(1, origin, 3, 20); var second = host.Capture(3);
                a.Receive(second); b.Receive(second);
                Check(!a.SimulatesMotion && b.SimulatesMotion && host.MotionActor == 3, "Ultimo golpe valido cambia controlador cliente a cliente");
                Check(!host.ReceiveMotion(2, a.CaptureMotion()), "Paquete del controlador retirado no recupera autoridad");
                a.Receive(grant);
                Check(a.MotionActor == 3 && !a.SimulatesMotion, "Concesion antigua no restaura al primer controlador");
                host.TakeAuthoritativeHit(1, origin, 1, 21); var hostHit = host.Capture(4);
                a.Receive(hostHit); b.Receive(hostHit);
                Check(host.SimulatesMotion && !host.CanRunHostAI && !b.SimulatesMotion, "Golpe del host recupera fisica sin habilitar IA durante empuje");

                Check(a.BeginLocalKnockback(2, 30, origin), "Cliente inicia otro tramo durante empuje del host");
                var early = a.CaptureMotion();
                Check(!host.ReceiveMotion(2, early), "Muestra anterior a concesion no se aplica");
                host.TakeAuthoritativeHit(1, origin, 2, 30); var third = host.Capture(5);
                a.Receive(third); b.Receive(third); host.ApplyBufferedMotion();
                Check(host.MotionPosition == early.position, "Ultima muestra provisional se aplica tras conceder");

                Check(b.BeginLocalKnockback(3, 40, origin), "Siguiente golpe puede terminar antes de concesion");
                Call(b, "FinishKnockback", true);
                var final = (MultiplayerSlimeMotion)Get(b, "localFinal");
                Check(final.finished && final.landed && !b.SimulatesMotion, "Cliente publica estado final y detiene su simulacion");
                host.ReceiveMotion(3, final);
                host.TakeAuthoritativeHit(1, origin, 3, 40); var fourth = host.Capture(6);
                a.Receive(fourth); b.Receive(fourth);
                Check(!b.SimulatesMotion, "Concesion tardia no reinicia un empuje ya aterrizado");
                host.ApplyBufferedMotion(); var returned = host.Capture(7);
                a.Receive(returned); b.Receive(returned);
                Check(host.MotionActor == 1 && host.CanRunHostAI && host.MotionPosition == final.position,
                    "Aterrizaje buffered devuelve control y pose final al host");
                Check(!host.ReceiveMotion(3, final), "Final atrasado no revierte la devolucion");

                Check(a.BeginLocalKnockback(2, 50, origin), "Nuevo tramo para verificar recuperacion");
                host.TakeAuthoritativeHit(1, origin, 2, 50); var fifth = host.Capture(8); a.Receive(fifth); b.Receive(fifth);
                double now = Time.realtimeSinceStartupAsDouble;
                Check(host.NeedsHostRecovery(now + 1.01d, true) && host.NeedsHostRecovery(now, false), "Silencio y desconexion requieren recuperar control");
                Set(host, "lastMotionAt", now + 4.8d);
                Check(host.NeedsHostRecovery(now + 5.1d, true), "Delegacion tiene limite total de cinco segundos");
                host.RecoverMotionToHost(); a.Receive(host.Capture(9)); b.Receive(host.Capture(9));
                Check(host.SimulatesMotion && !host.CanRunHostAI, "Recuperacion en vuelo mantiene IA pausada");

                a.BeginLocalKnockback(2, 60, origin); a.RejectLocalKnockback(2, 60);
                Check(!a.SimulatesMotion && a.GetComponent<Rigidbody>().isKinematic, "Rechazo detiene fisica provisional");
                a.BeginLocalKnockback(2, 61, origin); b.BeginLocalKnockback(3, 61, origin);
                host.TakeAuthoritativeHit(100, origin, 2, 61); var death = host.Capture(10);
                a.Receive(death); b.Receive(death);
                var animator = a.GetComponent<Animator>(); animator.Update(0f);
                Check(a.Health == 0 && b.Health == 0 && !a.SimulatesMotion && !b.SimulatesMotion &&
                    animator.GetCurrentAnimatorStateInfo(0).fullPathHash == Animator.StringToHash("Base Layer.SlimeDeath"), "Muerte confirmada cancela toda simulacion e inicia clip directo");
                a.Receive(grant); a.ReceiveMotion(3, final); animator.Update(0f);
                Check(a.Health == 0 && !a.SimulatesMotion && animator.GetCurrentAnimatorStateInfo(0).fullPathHash == death.animation,
                    "Mensajes viejos no reviven ni reinician movimiento despues de morir");
                registry.Remove(host.Id);
                Object.DestroyImmediate(host.gameObject); Object.DestroyImmediate(a.gameObject); Object.DestroyImmediate(b.gameObject);
            }
            CheckOwnershipRaces(world);
            var physical = Spawn(1, false, world, 2);
            foreach (Collider collider in physical.GetComponents<Collider>()) collider.enabled = true;
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.name = "Slime verification floor"; floor.tag = "Obstacle";
            floor.transform.position = new Vector3(500f, -0.5f, 500f); floor.transform.localScale = new Vector3(30f, 1f, 30f);
            physical.transform.position = new Vector3(500f, 0.2f, 500f); physical.GetComponent<Rigidbody>().position = physical.transform.position;
            physical.BeginLocalKnockback(2, 100, physical.transform.position - Vector3.right * 2f);
            yield return null;
            Check(physical.SimulatesMotion, "Contacto inicial con suelo no termina el impulso");
            for (int i = 0; i < 90 && physical.SimulatesMotion; i++) yield return null;
            Check(!physical.SimulatesMotion && ((MultiplayerSlimeMotion)Get(physical, "localFinal")).landed,
                "Fisica real detecta despegue y posterior aterrizaje");
            Object.DestroyImmediate(floor);
            physical.SuspendMotion(); physical.ResumeMotion();
            physical.transform.position = new Vector3(500f, 10f, 500f); physical.GetComponent<Rigidbody>().position = physical.transform.position;
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.tag = "Obstacle";
            wall.transform.position = new Vector3(501.5f, 10f, 500f); wall.transform.localScale = new Vector3(0.3f, 30f, 30f);
            physical.BeginLocalKnockback(2, 101, physical.transform.position - Vector3.right * 2f);
            for (int i = 0; i < 20; i++) yield return null;
            Check(physical.SimulatesMotion, "Pared con tag Obstacle no devuelve el control");
            Object.DestroyImmediate(wall);
            physical.SuspendMotion(); physical.ResumeMotion();
            physical.transform.position = new Vector3(500f, 10f, 500f); physical.GetComponent<Rigidbody>().position = physical.transform.position;
            Physics.SyncTransforms();
            var ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube); ceiling.tag = "Obstacle";
            ceiling.transform.localScale = new Vector3(30f, 0.2f, 30f);
            ceiling.transform.position = new Vector3(500f, physical.GetComponent<Collider>().bounds.max.y + 0.2f, 500f);
            physical.BeginLocalKnockback(2, 102, physical.transform.position - Vector3.right * 2f);
            for (int i = 0; i < 20; i++) yield return null;
            Check(physical.SimulatesMotion, "Techo con tag Obstacle no se confunde con suelo");
            Object.DestroyImmediate(ceiling);
            physical.SuspendMotion(); physical.ResumeMotion();
            physical.transform.position = new Vector3(500f, 0.2f, 500f); physical.GetComponent<Rigidbody>().position = physical.transform.position;
            var slope = GameObject.CreatePrimitive(PrimitiveType.Cube); slope.tag = "Obstacle";
            slope.transform.position = new Vector3(500f, -0.5f, 500f); slope.transform.localScale = new Vector3(30f, 1f, 30f);
            slope.transform.rotation = Quaternion.Euler(0f, 0f, 30f);
            physical.BeginLocalKnockback(2, 103, physical.transform.position + Vector3.right * 2f);
            for (int i = 0; i < 120 && physical.SimulatesMotion; i++) yield return null;
            Check(!physical.SimulatesMotion && ((MultiplayerSlimeMotion)Get(physical, "localFinal")).landed,
                "Superficie inclinada permite devolver control al aterrizar");
            Object.DestroyImmediate(slope);
            physical.SuspendMotion();
            Check(!physical.SimulatesMotion && ((IDictionary)Get(physical, "candidates")).Count == 0, "Suspension libera fisica y mensajes pendientes");
            Object.DestroyImmediate(physical.gameObject);
            success = true;
        }
        finally
        {
            Time.captureDeltaTime = previousCaptureDelta;
            Directory.CreateDirectory("Logs/combat-verification");
            File.WriteAllText("Logs/combat-verification/slimes.json", JsonUtility.ToJson(new Report { success = success, checks = checks.ToArray() }, true));
            Debug.Log("Slimes multijugador: " + checks.Count + " comprobaciones; correcta=" + success);
        }
    }
    private static void CheckOwnershipRaces(MultiplayerWorldSession world)
    {
        var host = Spawn(1, true, world, 1); var client = Spawn(1, false, world, 2);
        client.Receive(host.Capture(1)); Vector3 origin = client.transform.position - Vector3.right;
        client.BeginLocalKnockback(2, 7, origin);
        host.TakeAuthoritativeHit(1, origin, 3, 8); client.Receive(host.Capture(2));
        Check(client.SimulatesMotion && client.MotionActor == 3, "Concesion ajena no resuelve un golpe local aun pendiente");
        host.TakeAuthoritativeHit(1, origin, 2, 7); client.Receive(host.Capture(3));
        Check(client.SimulatesMotion && client.MotionActor == 2 && client.MotionAttack == 7, "Concesion propia posterior conserva la simulacion provisional");
        client.BeginLocalKnockback(2, 9, origin); host.TakeAuthoritativeHit(1, origin, 2, 9);
        var delayed = host.Capture(4);
        host.TakeAuthoritativeHit(1, origin, 3, 10); client.Receive(host.Capture(5)); client.Receive(delayed);
        Check(!client.SimulatesMotion && client.MotionActor == 3, "Confirmacion propia antigua cede ante el controlador mas reciente");
        client.transform.position += Vector3.right;
        client.BeginLocalKnockback(2, 11, origin); client.RejectLocalKnockback(2, 11);
        Set(client, "correctionAt", Time.realtimeSinceStartupAsDouble - 0.11d); Call(client, "Update");
        Check(Vector3.Distance(client.transform.position, host.MotionPosition) < 0.01f, "Rechazo vuelve a pose confirmada en 0,10 segundos");
        var early = new MultiplayerSlimeMotion { id = host.Id, actor = 3, attack = 12, sequence = 1,
            rotation = Quaternion.identity, position = host.MotionPosition, time = PhotonNetwork.Time };
        host.ReceiveMotion(3, early);
        var cache = (IDictionary)Get(host, "candidates");
        cache[3].GetType().GetField("receivedAt").SetValue(cache[3], Time.realtimeSinceStartupAsDouble - 1.1d);
        Call(host, "ExpireCandidates"); Check(cache.Count == 0, "Muestra anterior a concesion caduca tras un segundo");
        host.ReceiveMotion(3, early); early = new MultiplayerSlimeMotion { id = host.Id, actor = 3, attack = 13, sequence = 1,
            rotation = Quaternion.identity, position = host.MotionPosition, time = PhotonNetwork.Time };
        host.ReceiveMotion(3, early); Check(cache.Count == 1, "Buffer guarda una unica muestra por candidato");
        client.SuspendMotion(); client.ResumeMotion();
        Check(!client.SimulatesMotion, "Reanudar no revive una delegacion local anterior");
        Object.DestroyImmediate(host.gameObject); Object.DestroyImmediate(client.gameObject);
    }
    private static MultiplayerEnemy Spawn(int type, bool host, MultiplayerWorldSession world, int actor)
    {
        var root = Object.Instantiate(Resources.Load<GameObject>("Multiplayer/" + (host ? "Host" : "Client") + "/Enemigos/Enemigo" + type),
            new Vector3(500f, 10f, 500f), Quaternion.identity);
        var enemy = root.GetComponent<MultiplayerEnemy>(); enemy.Initialize(world, "test:" + type, "test", type);
        Set(enemy, "localActor", actor); Set(enemy, "hostActor", 1); Call(enemy, "SetPhysics", host);
        foreach (Collider collider in root.GetComponents<Collider>()) collider.enabled = false;
        var movement = root.GetComponent<HostSlimeMovement>(); if (movement != null) movement.enabled = false;
        return enemy;
    }
    public static IEnumerator CheckNetwork(LoadBalancingClient attacker, PhotonPartyService service)
    {
        var networkChecks = new List<string>(); bool success = false;
        var observer = new LoadBalancingClient();
        GameObject coordinator = null, dummy = null;
        MultiplayerEnemy enemy = null;
        var run = MultiplayerRunSession.Instance;
        bool runEnabled = run.enabled;
        Action<EventData> attackerEvents = null, observerEvents = null, hostEvents = null;
        try
        {
            AppSettings settings = PhotonNetwork.PhotonServerSettings.AppSettings.CopyTo(new AppSettings());
            settings.FixedRegion = "us"; settings.AppVersion = PhotonNetwork.NetworkingClient.AppVersion;
            observer.NickName = "SlimeObserver";
            if (!observer.ConnectUsingSettings(settings)) throw new InvalidOperationException("No se pudo iniciar el observador Photon.");
            yield return NetworkWait(() => observer.State == ClientState.ConnectedToMasterServer, attacker, observer);
            if (!observer.OpJoinRoom(new EnterRoomParams { RoomName = service.RoomCode })) throw new InvalidOperationException("No se pudo unir el observador.");
            yield return NetworkWait(() => observer.InRoom && service.Members.Count == 3, attacker, observer);
            networkChecks.Add("Tres conexiones Photon reales en la sala aislada");
            int actor = attacker.LocalPlayer.ActorNumber;
            PhotonNetwork.CurrentRoom.SetCustomProperties(new ExitGames.Client.Photon.Hashtable
                { { "party.c1", actor }, { "party.c2", observer.LocalPlayer.ActorNumber } });
            yield return NetworkWait(() => MultiplayerGameplayProtocol.Participant(actor) &&
                MultiplayerGameplayProtocol.Participant(observer.LocalPlayer.ActorNumber), attacker, observer);
            run.enabled = false;
            Property(run, "RunId", "33333333333333333333333333333333"); Property(run, "FloorNumber", 1);
            Property(run, "Phase", MultiplayerRunPhase.Playing); Set(run, "activeRun", true); Set(run, "localBuiltKey", run.CurrentKey);
            coordinator = Object.Instantiate(Resources.Load<GameObject>(MultiplayerPlayers.ResourcePath));
            var players = coordinator.GetComponent<MultiplayerPlayers>(); players.enabled = false;
            if (!players.Initialize(run, out string error)) throw new InvalidOperationException(error);
            var world = players.World; world.Resume();
            dummy = new GameObject("Slime network dummy"); dummy.SetActive(false);
            Set(world, "player", dummy.AddComponent<PlayerController>());
            enemy = Spawn(1, true, world, PhotonNetwork.LocalPlayer.ActorNumber);
            ((IDictionary)Get(world, "enemies")).Add(enemy.Id, enemy);
            MultiplayerWorldMessage receivedState = null;
            MultiplayerSlimeMotion receivedMotion = null;
            bool spoofReceived = false;
            attackerEvents = ev =>
            {
                if (ev.Code == MultiplayerGameplayProtocol.WorldState && ev.CustomData is object[] data && data.Length == 4 && data[3] is string json)
                {
                    var state = JsonUtility.FromJson<MultiplayerWorldMessage>(json);
                    if (state.id == enemy.Id && state.op == "enemy") receivedState = state;
                }
            };
            observerEvents = ev =>
            {
                if (ev.Code == MultiplayerGameplayProtocol.SlimeMotion && ev.CustomData is object[] data && data.Length == 4 && data[3] is string json)
                {
                    var batch = JsonUtility.FromJson<MultiplayerSlimeMotionBatch>(json);
                    if (batch.entries != null && batch.entries.Length == 1) receivedMotion = batch.entries[0];
                }
            };
            attacker.EventReceived += attackerEvents; observer.EventReceived += observerEvents;
            hostEvents = ev => { if (ev.Code == MultiplayerGameplayProtocol.SlimeMotion && ev.Sender == observer.LocalPlayer.ActorNumber) spoofReceived = true; };
            PhotonNetwork.NetworkingClient.EventReceived += hostEvents;
            ((IDictionary)Get(world, "attacks"))[actor] = new MultiplayerAttackWindow(1, 1, Time.realtimeSinceStartupAsDouble + 10d);
            world.ObservePose(actor, enemy.MotionPosition - Vector3.right * 2f);
            SendNetwork(attacker, MultiplayerGameplayProtocol.WorldRequest, new MultiplayerWorldMessage { op = "hit", id = enemy.Id, attack = 1 }, true, run);
            yield return NetworkWait(() => enemy.MotionActor == actor && receivedState != null && receivedState.motionActor == actor, attacker, observer);
            networkChecks.Add("Peticion real de golpe valida dano y concede control desde el host");
            if (enemy.Health != 9 || enemy.SimulatesMotion) throw new InvalidOperationException("El host no separo dano de movimiento.");
            var frame = new MultiplayerSlimeMotion { id = enemy.Id, actor = actor, attack = 1, sequence = 1, time = PhotonNetwork.Time,
                position = enemy.MotionPosition + Vector3.right * 0.5f, rotation = Quaternion.identity, velocity = Vector3.right * 5f };
            SendNetwork(attacker, MultiplayerGameplayProtocol.SlimeMotion, new MultiplayerSlimeMotionBatch { entries = new[] { frame } }, false, run);
            yield return NetworkWait(() => enemy.MotionPosition == frame.position && receivedMotion != null && receivedMotion.sequence == 1, attacker, observer);
            networkChecks.Add("Evento 34 viaja directamente del atacante al host y al tercer cliente");
            var spoof = new MultiplayerSlimeMotion { id = enemy.Id, actor = actor, attack = 1, sequence = 2, time = PhotonNetwork.Time,
                position = frame.position + Vector3.right * 100f, rotation = Quaternion.identity };
            SendNetwork(observer, MultiplayerGameplayProtocol.SlimeMotion, new MultiplayerSlimeMotionBatch { entries = new[] { spoof } }, true, run);
            yield return NetworkWait(() => spoofReceived, attacker, observer);
            if (enemy.MotionPosition != frame.position || enemy.Health != 9) throw new InvalidOperationException("Otro remitente modifico el slime delegado.");
            networkChecks.Add("El host rechaza una muestra enviada por otro actor");
            frame.sequence = 3; frame.finished = frame.landed = true; frame.velocity = Vector3.zero;
            SendNetwork(attacker, MultiplayerGameplayProtocol.SlimeMotion, new MultiplayerSlimeMotionBatch { entries = new[] { frame } }, true, run);
            yield return NetworkWait(() => enemy.CanRunHostAI && receivedState != null && receivedState.motionActor == PhotonNetwork.LocalPlayer.ActorNumber &&
                receivedMotion != null && receivedMotion.finished, attacker, observer);
            networkChecks.Add("Final fiable llega a las otras conexiones y devuelve el control al host");
            success = true;
        }
        finally
        {
            if (attackerEvents != null) attacker.EventReceived -= attackerEvents;
            if (observerEvents != null) observer.EventReceived -= observerEvents;
            if (hostEvents != null) PhotonNetwork.NetworkingClient.EventReceived -= hostEvents;
            if (coordinator != null) Object.DestroyImmediate(coordinator);
            if (enemy != null) Object.DestroyImmediate(enemy.gameObject);
            if (dummy != null) Object.DestroyImmediate(dummy);
            Set(run, "activeRun", false); Set(run, "localBuiltKey", null); Property(run, "Phase", MultiplayerRunPhase.Idle);
            run.enabled = runEnabled; observer.Disconnect(); observer.Service();
            Directory.CreateDirectory("Logs/combat-verification");
            File.WriteAllText("Logs/combat-verification/slime-network.json", JsonUtility.ToJson(new Report { success = success, checks = networkChecks.ToArray() }, true));
            Debug.Log("Red de slimes: " + networkChecks.Count + " comprobaciones; correcta=" + success);
        }
        yield return NetworkWait(() => service.Members.Count == 2, attacker, observer);
    }
    private static IEnumerator NetworkWait(Func<bool> condition, LoadBalancingClient first, LoadBalancingClient second)
    {
        double deadline = Time.realtimeSinceStartupAsDouble + 20d;
        while (!condition())
        {
            first.Service(); second.Service();
            if (Time.realtimeSinceStartupAsDouble >= deadline) throw new InvalidOperationException("Timeout en prueba de red de slimes.");
            yield return null;
        }
    }
    private static void SendNetwork<T>(LoadBalancingClient peer, byte code, T message, bool reliable, MultiplayerRunSession run)
    {
        if (!peer.OpRaiseEvent(code, new object[] { MultiplayerGameplayProtocol.Version, run.RunId, run.FloorNumber, JsonUtility.ToJson(message) },
            new RaiseEventOptions { Receivers = ReceiverGroup.Others }, new SendOptions { Reliability = reliable }))
            throw new InvalidOperationException("Photon rechazo el mensaje de prueba.");
        peer.Service();
    }
    private static void Property(object target, string name, object value) => target.GetType().GetProperty(name).GetSetMethod(true).Invoke(target, new[] { value });
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); checks.Add(message); }
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static object Get(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    private static void Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
}
#endif
