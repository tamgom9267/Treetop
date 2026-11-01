using System;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

/// <summary>Una copia por partida, creada por MultiplayerRunSession desde Resources.</summary>
[RequireComponent(typeof(MultiplayerLocalPlayerPublisher))]
public sealed class MultiplayerPlayers : MonoBehaviourPunCallbacks, IOnEventCallback
{
    public const string ResourcePath = "Multiplayer/Local/MultiplayerPlayers";
    [SerializeField] private MultiplayerRemotePlayer remotePlayerPrefab;
    [SerializeField] private MultiplayerVisualDefinition[] catalog = Array.Empty<MultiplayerVisualDefinition>();
    [SerializeField, Range(1f, 30f)] private float poseFrequency = 15f;
    [SerializeField, Range(0f, 0.3f)] private float interpolationDelay = 0.1f;
    private readonly Dictionary<int, MultiplayerRemotePlayer> remotes = new Dictionary<int, MultiplayerRemotePlayer>();
    private readonly Dictionary<int, int> requestSequences = new Dictionary<int, int>();
    private readonly List<int> departedActors = new List<int>();
    private MultiplayerRunSession session;
    private MultiplayerLocalPlayerPublisher publisher;
    private string runId;
    private int floorNumber;
    private int sequence;
    private bool localReady;
    private bool suspended = true;
    private bool initialized;
    private float previousDispatchThreshold;

    public bool ValidateAssets(out string error)
    {
        error = "Falta RemotePlayer o su Animator/Hand en el prefab MultiplayerPlayers.";
        if (remotePlayerPrefab == null || !remotePlayerPrefab.ValidateAssets()) return false;
        HashSet<string> types = new HashSet<string>();
        HashSet<WeaponData> weapons = new HashSet<WeaponData>();
        if (catalog == null || catalog.Length == 0) { error = "El catalogo visual multijugador esta vacio."; return false; }
        foreach (MultiplayerVisualDefinition definition in catalog)
        {
            if (definition == null || string.IsNullOrEmpty(definition.typeId) || definition.typeId.Length > 64 ||
                definition.localWeapon == null || definition.remotePrefab == null || !types.Add(definition.typeId) ||
                !weapons.Add(definition.localWeapon))
            { error = "Hay referencias vacias o repetidas en el catalogo visual multijugador."; return false; }
        }
        error = string.Empty;
        return true;
    }

    public bool Initialize(MultiplayerRunSession runSession, out string error)
    {
        if (!ValidateAssets(out error)) return false;
        session = runSession;
        publisher = GetComponent<MultiplayerLocalPlayerPublisher>();
        if (!initialized)
        {
            previousDispatchThreshold = PhotonNetwork.MinimalTimeScaleToDispatchInFixedUpdate;
            PhotonNetwork.MinimalTimeScaleToDispatchInFixedUpdate = Mathf.Max(0f, previousDispatchThreshold);
            initialized = true;
        }
        BeginFloor(session.RunId, session.FloorNumber);
        return true;
    }

    public bool TryGetType(WeaponData data, out string type)
    {
        foreach (MultiplayerVisualDefinition definition in catalog)
            if (definition.localWeapon == data) { type = definition.typeId; return true; }
        type = string.Empty;
        return false;
    }

    public void BeginFloor(string run, int floor)
    {
        Suspend();
        if (runId == run && floorNumber == floor) return;
        runId = run;
        floorNumber = floor;
        localReady = false;
        requestSequences.Clear();
        foreach (MultiplayerRemotePlayer remote in remotes.Values) remote.ResetForFloor();
    }

    public void Suspend()
    {
        suspended = true;
        if (publisher != null) publisher.Suspend();
        foreach (MultiplayerRemotePlayer remote in remotes.Values) remote.SetPlaying(false);
    }

    public void ResumePlaying()
    {
        suspended = false;
        if (localReady) publisher.SendFullState();
    }

    public bool PrepareLocalFloor(PlayerController player, out string error)
    {
        BeginFloor(session.RunId, session.FloorNumber);
        if (!publisher.Bind(this, player, out error)) return false;
        localReady = true;
        if (!publisher.SendFullState() || !Send(MultiplayerPlayerProtocol.RequestStateEvent,
            MultiplayerPlayerProtocol.PackRequest(runId, floorNumber, NextSequence(), PhotonNetwork.Time), true))
        {
            localReady = false;
            error = "No se pudo publicar el estado inicial de los jugadores.";
            return false;
        }
        error = string.Empty;
        return true;
    }

    private int NextSequence() { return ++sequence; }

    public bool SendEquipment(MultiplayerPlayerProtocol.Equipment state, int targetActor = 0)
    {
        state.pose.sequence = NextSequence();
        return Send(MultiplayerPlayerProtocol.EquipmentEvent,
            MultiplayerPlayerProtocol.PackEquipment(runId, floorNumber, state), true, targetActor);
    }

    public bool SendAttack(MultiplayerPlayerProtocol.Attack attack)
    {
        attack.pose.sequence = NextSequence();
        return Send(MultiplayerPlayerProtocol.AttackEvent,
            MultiplayerPlayerProtocol.PackAttack(runId, floorNumber, attack), true);
    }

    public void SendPose(MultiplayerPlayerProtocol.Pose pose)
    {
        pose.sequence = NextSequence();
        Send(MultiplayerPlayerProtocol.PoseEvent, MultiplayerPlayerProtocol.PackPose(runId, floorNumber, pose), false);
    }

    private bool Send(byte code, object[] data, bool reliable, int targetActor = 0)
    {
        if (!initialized || session == null || !session.IsRunning || !PhotonNetwork.InRoom) return false;
        RaiseEventOptions options = targetActor > 0 ? new RaiseEventOptions { TargetActors = new[] { targetActor } } :
            new RaiseEventOptions { Receivers = ReceiverGroup.Others };
        return PhotonNetwork.RaiseEvent(code, data, options, new SendOptions { Reliability = reliable });
    }

    private void LateUpdate()
    {
        if (session == null || !session.IsRunning || !session.IsInMultiplayerScene) { Suspend(); return; }
        departedActors.Clear();
        foreach (int actor in remotes.Keys) if (!IsParticipant(actor)) departedActors.Add(actor);
        foreach (int actor in departedActors) RemoveRemote(actor);
        bool playing = localReady && !suspended && session.IsPlaying;
        if (playing) publisher.Tick(poseFrequency);
        foreach (MultiplayerRemotePlayer remote in remotes.Values) remote.SetPlaying(playing);
    }

    private bool IsParticipant(int actor)
    {
        if (!PhotonNetwork.InRoom || actor == PhotonNetwork.LocalPlayer.ActorNumber ||
            !PhotonNetwork.CurrentRoom.Players.TryGetValue(actor, out Player participant) || participant.IsInactive) return false;
        PhotonPartyService party = PhotonPartyService.Instance;
        if (party == null || !party.IsInParty) return false;
        foreach (PartyMember member in party.Members)
            if (member.ActorNumber == actor && member.Role != PartyRole.None) return true;
        return false;
    }

    public void OnEvent(EventData photonEvent)
    {
        if (!initialized || session == null || !session.IsRunning || !IsParticipant(photonEvent.Sender)) return;
        byte code = photonEvent.Code;
        int length = code == MultiplayerPlayerProtocol.PoseEvent ? 9 : code == MultiplayerPlayerProtocol.EquipmentEvent ? 12 :
            code == MultiplayerPlayerProtocol.AttackEvent ? 11 : code == MultiplayerPlayerProtocol.RequestStateEvent ? 5 : 0;
        if (length == 0 || !MultiplayerPlayerProtocol.TryReadHeader(photonEvent.CustomData,
            session.RunId, session.FloorNumber, length, out object[] data)) return;
        if (runId != session.RunId || floorNumber != session.FloorNumber) BeginFloor(session.RunId, session.FloorNumber);
        int actor = photonEvent.Sender;
        if (code == MultiplayerPlayerProtocol.RequestStateEvent)
        {
            int incomingSequence = (int)data[3];
            requestSequences.TryGetValue(actor, out int previous);
            if (!MultiplayerPlayerProtocol.IsNewer(incomingSequence, previous)) return;
            requestSequences[actor] = incomingSequence;
            if (localReady) publisher.SendFullState(actor);
        }
        else if (code == MultiplayerPlayerProtocol.EquipmentEvent && MultiplayerPlayerProtocol.TryReadEquipment(data, out var equipment))
            GetRemote(actor).ReceiveEquipment(equipment);
        else if (code == MultiplayerPlayerProtocol.AttackEvent && MultiplayerPlayerProtocol.TryReadAttack(data, out var attack))
            GetRemote(actor).ReceiveAttack(attack);
        else if (code == MultiplayerPlayerProtocol.PoseEvent && MultiplayerPlayerProtocol.TryReadPose(data, out var pose))
            GetRemote(actor).ReceivePose(pose);
    }

    private MultiplayerRemotePlayer GetRemote(int actor)
    {
        if (!remotes.TryGetValue(actor, out MultiplayerRemotePlayer remote))
        {
            remote = Instantiate(remotePlayerPrefab, transform);
            remote.name = "RemotePlayer_" + actor;
            remote.gameObject.SetActive(false);
            remote.Initialize(catalog, interpolationDelay);
            remotes.Add(actor, remote);
        }
        return remote;
    }

    public override void OnPlayerLeftRoom(Player otherPlayer) { RemoveRemote(otherPlayer.ActorNumber); }

    private void RemoveRemote(int actor)
    {
        if (!remotes.TryGetValue(actor, out MultiplayerRemotePlayer remote)) return;
        remote.gameObject.SetActive(false);
        Destroy(remote.gameObject);
        remotes.Remove(actor);
        requestSequences.Remove(actor);
    }

    public void Shutdown()
    {
        Suspend();
        if (publisher != null) publisher.Detach();
        if (initialized)
        {
            PhotonNetwork.MinimalTimeScaleToDispatchInFixedUpdate = previousDispatchThreshold;
            initialized = false;
        }
        localReady = false;
    }

    public override void OnDisable() { Shutdown(); base.OnDisable(); }
    private void OnDestroy() { Shutdown(); }
}
