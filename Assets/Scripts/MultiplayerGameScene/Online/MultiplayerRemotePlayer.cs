using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

/// <summary>Movimiento y animacion visual. No lee input, no simula fisica ni aplica golpes.</summary>
public sealed class MultiplayerRemotePlayer : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private MultiplayerRemoteEquipment equipment;
    private readonly List<MultiplayerPlayerProtocol.Pose> poses = new List<MultiplayerPlayerProtocol.Pose>();
    private readonly Queue<MultiplayerPlayerProtocol.Attack> attacks = new Queue<MultiplayerPlayerProtocol.Attack>();
    private float interpolationDelay = 0.1f;
    private double lastReception;
    private int poseSequence;
    private int equipmentSequence;
    private int attackSequence;
    private bool initialState;
    private bool actionPlaying;
    private float actionElapsed;
    private static readonly int Speed = Animator.StringToHash("Speed");
    private static readonly int Attack = Animator.StringToHash("isAttacking");

    public Animator Animator => animator;
    public MultiplayerRemoteEquipment Equipment => equipment;
    public bool HasInitialState => initialState;
    public int LastAttackSequence => attackSequence;

    public bool ValidateAssets() => animator != null && animator.gameObject == gameObject &&
        animator.runtimeAnimatorController != null && equipment != null && equipment.Hand != null;

    public void Configure(Animator playerAnimator, MultiplayerRemoteEquipment playerEquipment)
    {
        animator = playerAnimator;
        equipment = playerEquipment;
    }

    public void Initialize(MultiplayerVisualDefinition[] catalog, float delay)
    {
        interpolationDelay = Mathf.Clamp(delay, 0f, 0.3f);
        equipment.Configure(equipment.Hand, catalog);
        animator.updateMode = AnimatorUpdateMode.UnscaledTime;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.applyRootMotion = false;
        ResetForFloor();
    }

    public void ResetForFloor()
    {
        SetPlaying(false);
        poses.Clear();
        attacks.Clear();
        poseSequence = equipmentSequence = attackSequence = 0;
        initialState = false;
        equipment.EndAction();
    }

    public void SetPlaying(bool playing)
    {
        bool visible = playing && initialState && poses.Count > 0;
        if (gameObject.activeSelf == visible) return;
        gameObject.SetActive(visible);
        if (visible)
        {
            animator.Rebind();
            animator.ResetTrigger(Attack);
        }
    }

    public bool ReceiveEquipment(MultiplayerPlayerProtocol.Equipment state)
    {
        if (!MultiplayerPlayerProtocol.IsNewer(state.pose.sequence, equipmentSequence)) return false;
        equipmentSequence = state.pose.sequence;
        equipment.Apply(state);
        initialState = true;
        ReceivePose(state.pose);
        return true;
    }

    public bool ReceivePose(MultiplayerPlayerProtocol.Pose pose)
    {
        if (!MultiplayerPlayerProtocol.IsNewer(pose.sequence, poseSequence)) return false;
        if (poses.Count > 0 && pose.time < poses[poses.Count - 1].time) return false;
        poseSequence = pose.sequence;
        lastReception = Time.realtimeSinceStartupAsDouble;
        if (poses.Count == 0 || Vector3.Distance(poses[poses.Count - 1].position, pose.position) > 5f)
        {
            poses.Clear();
            transform.SetPositionAndRotation(pose.position, pose.rotation);
        }
        if (poses.Count > 0 && pose.time == poses[poses.Count - 1].time) poses.RemoveAt(poses.Count - 1);
        poses.Add(pose);
        if (poses.Count > 32) poses.RemoveAt(0);
        return true;
    }

    public bool ReceiveAttack(MultiplayerPlayerProtocol.Attack attack)
    {
        if (!MultiplayerPlayerProtocol.IsNewer(attack.pose.sequence, attackSequence)) return false;
        attackSequence = attack.pose.sequence;
        ReceivePose(attack.pose);
        if (attacks.Count >= 16) attacks.Dequeue();
        attacks.Enqueue(attack);
        return true;
    }

    private void LateUpdate() { RenderFrame(PhotonNetwork.Time, Time.unscaledDeltaTime); }

    // Tambien permite comprobar interpolacion y eventos nativos en una escena de vista previa.
    public void RenderFrame(double networkTime, float deltaTime)
    {
        if (poses.Count == 0) return;
        double renderTime = networkTime - interpolationDelay;
        while (poses.Count > 2 && poses[1].time <= renderTime) poses.RemoveAt(0);
        MultiplayerPlayerProtocol.Pose from = poses[0];
        MultiplayerPlayerProtocol.Pose to = poses.Count > 1 ? poses[1] : from;
        float fraction = to.time > from.time ? Mathf.Clamp01((float)((renderTime - from.time) / (to.time - from.time))) : 1f;
        transform.SetPositionAndRotation(Vector3.Lerp(from.position, to.position, fraction),
            Quaternion.Slerp(from.rotation, to.rotation, fraction));
        bool stale = Time.realtimeSinceStartupAsDouble - lastReception > 0.5d;
        animator.SetFloat(Speed, stale ? 0f : Mathf.Lerp(from.speed, to.speed, fraction));
        // Mantiene pausas/hitstop del emisor; la pausa del receptor no detiene a los otros.
        animator.speed = stale ? 1f : (fraction < 1f ? from.playbackRate : to.playbackRate);
        while (attacks.Count > 0 && attacks.Peek().pose.time <= renderTime)
        {
            MultiplayerPlayerProtocol.Attack next = attacks.Dequeue();
            if (renderTime - next.pose.time > 0.75d) continue;
            equipment.BeginAction(next.item);
            animator.ResetTrigger(Attack);
            animator.SetTrigger(Attack);
            actionPlaying = true;
            actionElapsed = 0f;
        }
        if (actionPlaying)
        {
            actionElapsed += deltaTime * animator.speed;
            if (actionElapsed > 1f) EndAttack(); // Respaldo si un clip no entrega su evento final.
        }
    }

    // Nombres conservados para los Animation Events de los clips originales.
    public void EnableWeaponCollider() { equipment.SetActionTrail(true); }
    public void EndAttack()
    {
        actionPlaying = false;
        actionElapsed = 0f;
        equipment.EndAction();
    }

    private void OnDisable()
    {
        attacks.Clear();
        if (equipment != null) EndAttack();
    }
}
