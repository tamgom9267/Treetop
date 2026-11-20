using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

// El host mueve las raices; las copias cliente solo reproducen pose y ventanas de golpe.
public sealed class MultiplayerTreeRootSync : MonoBehaviour, IOnEventCallback
{
    private TreeRootController controller;
    private MultiplayerEnemy enemy;
    private TreeRootAnimationEvents[] animationEvents;
    private int sequence, receivedSequence, windowMask;
    private float nextSend;

    public bool Authority => enemy != null && enemy.Authority;
    public string RoomId => enemy != null ? enemy.RoomId : null;

    private void Awake()
    {
        controller = GetComponent<TreeRootController>();
        enemy = GetComponentInChildren<MultiplayerEnemy>(true);
        animationEvents = GetComponentsInChildren<TreeRootAnimationEvents>(true);
    }

    private void OnEnable() { PhotonNetwork.AddCallbackTarget(this); }
    private void OnDisable() { PhotonNetwork.RemoveCallbackTarget(this); }

    private void Start()
    {
        if (!Authority && controller != null) controller.DisableClientAgents();
    }

    private void Update()
    {
        if (!Authority || enemy == null || !enemy.CombatActive || controller == null ||
            MultiplayerWorldSession.Instance == null || !MultiplayerWorldSession.Instance.Playing || Time.unscaledTime < nextSend) return;
        nextSend = Time.unscaledTime + 0.1f;
        Publish(false);
    }

    public void NotifyWindow(int index, bool active)
    {
        if (!Authority || index < 0 || index >= controller.RootCount) return;
        int bit = 1 << index;
        int updated = active ? windowMask | bit : windowMask & ~bit;
        if (updated == windowMask) return;
        windowMask = updated;
        Publish(true);
    }

    public void CloseWindows()
    {
        windowMask = 0;
        foreach (TreeRootAnimationEvents animationEvent in animationEvents)
            if (animationEvent != null) animationEvent.ApplyRemoteWindow(false);
        if (Authority) Publish(true);
    }

    public void Publish(bool reliable, int target = 0)
    {
        if (!Authority || enemy == null || string.IsNullOrEmpty(enemy.Id) || controller == null) return;
        int count = controller.RootCount;
        var state = new MultiplayerTreeRootState
        {
            id = enemy.Id, sequence = ++sequence, windowMask = windowMask,
            positions = new Vector3[count], heights = new float[count]
        };
        for (int i = 0; i < count; i++)
        {
            state.positions[i] = controller.RootPosition(i);
            state.heights[i] = controller.RootHeight(i);
            if (controller.RootAttacking(i)) state.attackMask |= 1 << i;
        }
        MultiplayerGameplayProtocol.Send(MultiplayerGameplayProtocol.TreeRoots, state, reliable, target, reliable);
    }

    public void OnEvent(EventData ev)
    {
        if (Authority || ev.Code != MultiplayerGameplayProtocol.TreeRoots ||
            ev.Sender != MultiplayerGameplayProtocol.HostActor ||
            !MultiplayerGameplayProtocol.Read(ev, out MultiplayerTreeRootState state) ||
            enemy == null || controller == null || state.id != enemy.Id ||
            state.sequence <= receivedSequence || state.positions == null || state.heights == null ||
            state.positions.Length != controller.RootCount || state.heights.Length != controller.RootCount) return;

        for (int i = 0; i < controller.RootCount; i++)
            if (!MultiplayerGameplayProtocol.Finite(state.positions[i]) ||
                !MultiplayerGameplayProtocol.Finite(state.heights[i])) return;

        receivedSequence = state.sequence;
        for (int i = 0; i < controller.RootCount; i++)
            controller.ApplyRemoteRoot(i, state.positions[i], state.heights[i], (state.attackMask & (1 << i)) != 0);
        foreach (TreeRootAnimationEvents animationEvent in animationEvents)
            if (animationEvent != null)
                animationEvent.ApplyRemoteWindow((state.windowMask & (1 << animationEvent.RootIndex)) != 0);
    }
}
