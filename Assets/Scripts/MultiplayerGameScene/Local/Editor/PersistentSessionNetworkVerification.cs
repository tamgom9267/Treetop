#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using Hashtable = ExitGames.Client.Photon.Hashtable;

/// <summary>Dos clientes reales: el servicio del juego y un peer Realtime independiente.</summary>
public static class PersistentSessionNetworkVerification
{
    private static readonly List<string> passed = new List<string>();
    private static LoadBalancingClient peer;
    [Serializable] private sealed class Report { public bool success; public string[] checks; }

    public static IEnumerator RunChecks()
    {
        passed.Clear();
        bool success = false;
        Scene village = SceneManager.GetSceneByName("VillageMenuScene");
        Scene menu = SceneManager.GetSceneByName("StartMenuScene");
        try
        {
            SceneManager.SetActiveScene(village);
            PhotonPartyService service = Session();
            service.ConnectIfNeeded();
            yield return Wait(() => service.IsConnected && !service.IsBusy, "Conectar servicio host");
            AppSettings settings = PhotonNetwork.PhotonServerSettings.AppSettings.CopyTo(new AppSettings());
            settings.FixedRegion = "us";
            settings.AppVersion = PhotonNetwork.NetworkingClient.AppVersion;
            peer = new LoadBalancingClient(); peer.NickName = "LifecyclePeer";
            Require(peer.ConnectUsingSettings(settings), "Iniciar el segundo cliente real");
            yield return Wait(() => peer.State == ClientState.ConnectedToMasterServer, "Conectar segundo cliente");
            service.CreateParty("LifecycleHost");
            yield return Wait(() => service.IsInParty && !service.IsBusy, "Crear party host");
            Require(peer.OpJoinRoom(new EnterRoomParams { RoomName = service.RoomCode }), "Segundo cliente solicita entrar a la party");
            yield return Wait(() => peer.InRoom && service.Members.Count == 2, "Confirmar los dos integrantes");
            Require(service.IsHost, "El servicio conserva el rol host con dos clientes reales");

            SceneManager.SetActiveScene(menu);
            yield return Wait(() => peer.InRoom && peer.CurrentRoom.CustomProperties["party.closing"] is bool closing && closing,
                "Recibir cierre del host en el segundo cliente");
            Require(!peer.CurrentRoom.IsOpen, "La salida del host cierra la admision de la sala");
            peer.OpLeaveRoom(false);
            yield return Wait(() => peer.State == ClientState.ConnectedToMasterServer && service == null && !PhotonNetwork.IsConnected,
                "Completar salida del host y del segundo cliente");
            Require(PhotonPartyService.Instance == null && MultiplayerRunSession.Instance == null,
                "El host desconecta y destruye los gestores despues de avisar");

            string code = UnityEngine.Random.Range(10000, 100000).ToString();
            Require(peer.OpCreateRoom(new EnterRoomParams { RoomName = code, RoomOptions = new RoomOptions
                { MaxPlayers = 3, IsOpen = true, IsVisible = false, PlayerTtl = 0, EmptyRoomTtl = 0,
                  CustomRoomProperties = new Hashtable { { "party.schema", 1 }, { "party.host", 0 },
                    { "party.c1", 0 }, { "party.c2", 0 }, { "party.locked", false }, { "party.closing", false } } } }),
                "El segundo cliente crea una sala de prueba aislada");
            yield return Wait(() => peer.InRoom, "Crear sala del segundo cliente");
            peer.CurrentRoom.SetCustomProperties(new Hashtable { { "party.host", peer.LocalPlayer.ActorNumber } });

            SceneManager.SetActiveScene(village);
            service = Session(); service.ConnectIfNeeded();
            yield return Wait(() => service.IsConnected && !service.IsBusy, "Reconectar servicio como cliente");
            service.JoinParty("LifecycleClient", code);
            yield return Wait(() => service.IsInParty && peer.CurrentRoom.PlayerCount == 2, "Entrada del servicio cliente");
            peer.CurrentRoom.SetCustomProperties(new Hashtable { { "party.c1", PhotonNetwork.LocalPlayer.ActorNumber } });
            yield return Wait(() => service.LocalRole == PartyRole.Client1 && !service.IsBusy, "Confirmar rol cliente");
            Time.timeScale = 0f;
            SceneManager.SetActiveScene(menu);
            yield return Wait(() => service == null && !PhotonNetwork.IsConnected && peer.CurrentRoom.PlayerCount == 1,
                "Salida real del cliente con el juego pausado");
            Time.timeScale = 1f;
            Require(peer.InRoom && peer.CurrentRoom.IsOpen, "La salida de un cliente conserva la party abierta del host");

            SceneManager.SetActiveScene(village);
            service = Session(); service.ConnectIfNeeded();
            yield return Wait(() => service.IsConnected && !service.IsBusy, "Volver a conectar despues de destruir los gestores");
            service.JoinParty("LifecycleClient", code);
            yield return Wait(() => service.IsInParty && peer.CurrentRoom.PlayerCount == 2, "Reingresar a la party");
            peer.CurrentRoom.SetCustomProperties(new Hashtable { { "party.c1", PhotonNetwork.LocalPlayer.ActorNumber } });
            yield return Wait(() => service.LocalRole == PartyRole.Client1 && !service.IsBusy, "Restablecer rol cliente");
            PartyEndReason? reason = null;
            service.PartyEnded += ended => reason = ended;
            peer.Disconnect();
            yield return Wait(() => !service.IsInParty && !PhotonNetwork.InRoom && !service.IsBusy,
                "Procesar la perdida de conexion del host");
            Require(reason == PartyEndReason.HostDisconnected && PhotonPartyService.Instance == service,
                "Perder al host mantiene la gestion normal en la aldea y comunica el motivo correcto");
            SceneManager.SetActiveScene(menu);
            yield return Wait(() => service == null && !PhotonNetwork.IsConnected, "Limpiar despues de perder al host");

            SceneManager.SetActiveScene(village);
            service = Session(); service.ConnectIfNeeded();
            SceneManager.SetActiveScene(menu);
            yield return Wait(() => service == null && !PhotonNetwork.IsConnected, "Salir durante una conexion real pendiente");
            Require(!PhotonNetwork.InRoom, "Cancelar conexion no deja una sala activa");

            SceneManager.SetActiveScene(village);
            service = Session(); service.ConnectIfNeeded();
            yield return Wait(() => service.IsConnected && !service.IsBusy, "Preparar creacion pendiente real");
            service.CreateParty("LifecycleCancel");
            SceneManager.SetActiveScene(menu);
            yield return Wait(() => service == null && !PhotonNetwork.IsConnected, "Cancelar creacion real pendiente");
            Require(!PhotonNetwork.InRoom && PhotonPartyService.Instance == null,
                "Una respuesta tardia de creacion no deja conexion ni gestores");
            success = true;
        }
        finally
        {
            Time.timeScale = 1f;
            if (peer != null) { peer.Disconnect(); peer.Service(); peer = null; }
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/lifecycle-verification/network.json"));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(new Report { success = success, checks = passed.ToArray() }, true));
            Debug.Log("Verificacion de red: " + passed.Count + " comprobaciones; correcta=" + success);
        }
    }

    private static IEnumerator Wait(Func<bool> condition, string label)
    {
        double until = Time.realtimeSinceStartupAsDouble + 30d;
        while (!condition())
        {
            if (peer != null) peer.Service();
            if (Time.realtimeSinceStartupAsDouble >= until) throw new InvalidOperationException("Red: " + label);
            yield return null;
        }
        passed.Add(label);
    }

    private static PhotonPartyService Session()
    {
        var root = new GameObject("MultiplayerSession de verificacion de red"); root.SetActive(false);
        var service = root.AddComponent<PhotonPartyService>(); root.AddComponent<MultiplayerRunSession>();
        typeof(PhotonPartyService).GetField("gameVersion", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(service, "treetop-lifecycle-verification-v1");
        root.SetActive(true); return service;
    }

    private static void Require(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException("Red: " + label);
        passed.Add(label);
    }
}
#endif
