using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

// Salud e IA pertenecen al host; el movimiento puede delegarse durante un empuje.
public sealed class MultiplayerEnemy : MonoBehaviour
{
    private static readonly int DeathAnimation = Animator.StringToHash("Base Layer.SlimeDeath");
    [SerializeField] private int maxHealth = 10, contactDamage = 5;
    [SerializeField] private float knockbackForce = 5f, knockbackUpForce = 2f, weaponKnockbackMultiplier = 1f;
    [SerializeField] private Renderer rend;
    [SerializeField] private Color flashColor = Color.red;
    [SerializeField] private float flashDuration = 0.1f;
    [Header("Visual Effects")]
    [SerializeField] private GameObject hitEffectPrefab, deathEffectPrefab;
    [SerializeField] private Vector3 deathEffectOffset = Vector3.zero;
    [SerializeField] private bool authority;

    private Rigidbody body;
    private Animator animator;
    private MultiplayerWorldSession world;
    private Material[] materials;
    private Color[] colors;
    private Coroutine flash;
    private int localActor, hostActor, frameRevision, lastAnimation, lastHitActor, lastHitAttack;
    private int motionActor, motionAttack, motionVersion, receivedSequence, localSequence;
    private int pendingAttack, pendingBaseVersion;
    private bool knockbackActive, localLanded, suspended, deathStarted, deathEffectSpawned, leftGround, motionBlocked;
    private float deathAge;
    private double pendingAt, delegatedAt, lastMotionAt, impulseFixedAt, lastGroundAt = double.NegativeInfinity;
    private double correctionAt = double.NegativeInfinity;
    private Vector3 correctionFrom;
    private Quaternion correctionRotation;
    private MultiplayerSlimeMotion latestMotion;
    private MultiplayerSlimeMotion localFinal;
    private readonly HashSet<long> anticipatedHits = new HashSet<long>();
    private readonly HashSet<long> retiredControls = new HashSet<long>();
    private readonly HashSet<int> presentedHits = new HashSet<int>();
    private readonly Dictionary<int, BufferedMotion> candidates = new Dictionary<int, BufferedMotion>();
    private readonly List<MultiplayerSlimeMotion> frames = new List<MultiplayerSlimeMotion>();
    private readonly List<int> expiredCandidates = new List<int>();
    private sealed class BufferedMotion { public MultiplayerSlimeMotion frame; public double receivedAt; }

    public string Id { get; private set; }
    public string RoomId { get; private set; }
    public int Type { get; private set; }
    public int Health { get; private set; }
    public int HitRevision { get; private set; }
    public bool Authority => authority;
    public int MotionActor => motionActor;
    public int MotionAttack => motionAttack;
    public int MotionVersion => motionVersion;
    public bool KnockbackActive => knockbackActive || pendingAttack > 0;
    public bool SimulatesMotion => !suspended && !deathStarted && !localLanded && !motionBlocked &&
        (pendingAttack > 0 || motionActor == localActor);
    public bool CanRunHostAI => authority && SimulatesMotion && !KnockbackActive;
    public bool PublishesMotion => !authority && SimulatesMotion && KnockbackActive;
    public Vector3 MotionPosition => SimulatesMotion ? body.position : latestMotion != null ? latestMotion.position : transform.position;
    public float AttackReach => 6f + GetComponent<Collider>().bounds.extents.magnitude;

    private void Awake()
    {
        body = GetComponent<Rigidbody>(); animator = GetComponent<Animator>(); Health = maxHealth;
        body.isKinematic = !authority; body.useGravity = authority;
        if (!authority) animator.updateMode = AnimatorUpdateMode.UnscaledTime;
        if (rend != null)
        {
            materials = rend.materials; colors = new Color[materials.Length];
            for (int i = 0; i < colors.Length; i++) colors[i] = materials[i].color;
        }
    }
    public void Initialize(MultiplayerWorldSession owner, string id, string room, int type)
    {
        world = owner; Id = id; RoomId = room; Type = type;
        suspended = owner != null && !owner.Playing;
        localActor = MultiplayerGameplayProtocol.LocalActor; hostActor = MultiplayerGameplayProtocol.HostActor;
        motionActor = hostActor; motionVersion = authority ? 1 : 0;
        SetPhysics(SimulatesMotion);
    }
    private static long Key(int actor, int attack) => ((long)actor << 32) | (uint)attack;
    private void SetPhysics(bool simulate)
    {
        if (body == null) return;
        body.isKinematic = !simulate; body.useGravity = simulate;
        body.interpolation = simulate && KnockbackActive ? RigidbodyInterpolation.Interpolate : RigidbodyInterpolation.None;
    }
    private void OnTriggerEnter(Collider other)
    {
        if (Health <= 0 || world == null || !world.Playing) return;
        WeaponObject weapon = other.GetComponent<WeaponObject>();
        if (other.CompareTag("Weapon") && weapon != null) world.RequestHit(this, weapon);
    }
    private void OnCollisionEnter(Collision collision)
    {
        ObserveGround(collision);
        if (Health <= 0 || world == null || !world.Playing) return;
        PlayerController target = collision.collider.GetComponentInParent<PlayerController>();
        if (target == null || target != MultiplayerStartingRoomSpawns.LocalPlayer) return;
        target.playerClass.TakeDamage(contactDamage); target.ReceiveHit(gameObject);
    }
    private void OnCollisionStay(Collision collision) { ObserveGround(collision); }
    private void ObserveGround(Collision collision)
    {
        if (!collision.gameObject.CompareTag("Obstacle")) return;
        for (int i = 0; i < collision.contactCount; i++)
            if (Vector3.Dot(collision.GetContact(i).normal, Vector3.up) >= 0.6f)
            {
                GroundContact(); break;
            }
    }
    private void GroundContact()
    {
        lastGroundAt = Time.fixedTimeAsDouble;
        if (SimulatesMotion && KnockbackActive && leftGround &&
            Time.fixedTimeAsDouble - impulseFixedAt >= 0.05d && body.linearVelocity.y <= 0.1f)
            FinishKnockback();
    }
    private void FixedUpdate()
    {
        if (!SimulatesMotion || !KnockbackActive || world == null || !world.Playing) return;
        if (Time.fixedTimeAsDouble - impulseFixedAt >= 0.04d &&
            Time.fixedTimeAsDouble - lastGroundAt > Time.fixedDeltaTime * 1.5d) leftGround = true;
    }
    private void StartGroundDetection(bool airborne = false)
    {
        impulseFixedAt = Time.fixedTimeAsDouble;
        lastGroundAt = Time.fixedTimeAsDouble; leftGround = airborne;
    }
    private void PauseAI()
    {
        var movement = GetComponent<HostSlimeMovement>();
        if (movement != null) movement.PauseForKnockback();
    }
    private void ResumeAI(bool landed)
    {
        if (landed) animator.SetTrigger("Land");
        var movement = GetComponent<HostSlimeMovement>();
        if (movement != null) movement.ResumeAfterKnockback();
    }
    private void ApplyImpulse(Vector3 origin)
    {
        Vector3 direction = body.position - origin; direction.y = 0f;
        body.linearVelocity = Vector3.zero;
        if (direction.sqrMagnitude > 0.0001f)
            body.AddForce((direction.normalized * knockbackForce + Vector3.up * knockbackUpForce) * weaponKnockbackMultiplier, ForceMode.Impulse);
        StartGroundDetection();
    }
    private void ShowHit(Vector3 origin)
    {
        ShowFlash();
        Collider collider = GetComponent<Collider>();
        SpawnHitEffect(collider != null ? collider.ClosestPoint(origin) : transform.position);
    }
    public bool BeginLocalKnockback(int actor, int attack, Vector3 origin)
    {
        if (authority || Health <= 0 || suspended || actor != localActor || actor <= 0 || attack <= 0 ||
            !MultiplayerGameplayProtocol.Finite(origin) || !anticipatedHits.Add(Key(actor, attack))) return false;
        pendingAttack = attack; pendingBaseVersion = motionVersion; pendingAt = Time.realtimeSinceStartupAsDouble;
        localSequence = 0; localLanded = false; localFinal = null; motionBlocked = false; frames.Clear(); correctionAt = double.NegativeInfinity;
        Vector3 start = transform.position; Quaternion rotation = transform.rotation;
        SetPhysics(true); body.position = start; body.rotation = rotation;
        ApplyImpulse(origin); ShowHit(origin);
        return true;
    }
    public void TakeAuthoritativeHit(int damage, Vector3 origin, int actor = 0, int attack = 0)
    {
        if (!authority || Health <= 0 || damage <= 0) return;
        if (actor <= 0) actor = hostActor;
        Vector3 start = MotionPosition;
        Health = Mathf.Max(0, Health - damage); HitRevision++;
        lastHitActor = actor; lastHitAttack = attack; ShowHit(origin);
        if (Health == 0) { BeginConfirmedDeath(); return; }
        RetireCurrentControl();
        motionActor = actor; motionAttack = attack; motionVersion++; knockbackActive = true;
        receivedSequence = localSequence = 0; localLanded = false; motionBlocked = false;
        delegatedAt = lastMotionAt = Time.realtimeSinceStartupAsDouble;
        frames.Clear(); pendingAttack = 0; PauseAI();
        // La concesion parte de la ultima pose valida, no de la copia visual interpolada.
        transform.position = start; body.position = start;
        latestMotion = new MultiplayerSlimeMotion { id = Id, actor = actor, attack = attack, position = start,
            rotation = transform.rotation, time = PhotonNetwork.Time };
        SetPhysics(SimulatesMotion);
        if (SimulatesMotion) ApplyImpulse(origin);
    }
    private void RetireCurrentControl()
    {
        if (motionAttack > 0) retiredControls.Add(Key(motionActor, motionAttack));
    }
    public MultiplayerSlimeMotion CaptureMotion(bool landed = false)
    {
        var state = animator.GetCurrentAnimatorStateInfo(0);
        return new MultiplayerSlimeMotion { id = Id, actor = localActor,
            attack = pendingAttack > 0 ? pendingAttack : motionAttack, sequence = ++localSequence,
            time = PhotonNetwork.Time, position = body.position, rotation = body.rotation,
            velocity = body.isKinematic ? Vector3.zero : body.linearVelocity,
            angularVelocity = body.isKinematic ? Vector3.zero : body.angularVelocity,
            animation = state.fullPathHash, normalizedTime = state.normalizedTime, landed = landed, finished = landed };
    }
    public MultiplayerWorldMessage Capture(int revision)
    {
        var state = animator.GetCurrentAnimatorStateInfo(0);
        bool remote = motionActor != hostActor && latestMotion != null;
        return new MultiplayerWorldMessage { op = "enemy", id = Id, room = RoomId, type = Type, revision = revision,
            health = Health, hit = HitRevision, actor = lastHitActor, attack = lastHitAttack,
            motionActor = motionActor, motionAttack = motionAttack, motionVersion = motionVersion,
            motionSequence = remote ? receivedSequence : revision, knockbackActive = knockbackActive,
            position = remote ? latestMotion.position : body.position, rotation = remote ? latestMotion.rotation : body.rotation,
            velocity = remote ? latestMotion.velocity : body.isKinematic ? Vector3.zero : body.linearVelocity,
            angularVelocity = remote ? latestMotion.angularVelocity : body.isKinematic ? Vector3.zero : body.angularVelocity,
            animation = Health == 0 ? DeathAnimation : remote ? latestMotion.animation : state.fullPathHash,
            normalizedTime = Health == 0 && state.fullPathHash != DeathAnimation ? 0f : remote ? latestMotion.normalizedTime : state.normalizedTime,
            time = remote ? latestMotion.time : PhotonNetwork.Time };
    }
    public void Receive(MultiplayerWorldMessage state)
    {
        if (authority || state.revision <= 0 || state.health < 0 || state.motionVersion <= 0 || state.motionActor <= 0 ||
            !MultiplayerGameplayProtocol.Finite(state.normalizedTime) || !MultiplayerGameplayProtocol.Finite(state.velocity) ||
            !MultiplayerGameplayProtocol.Finite(state.angularVelocity)) return;
        bool anticipated = anticipatedHits.Contains(Key(state.actor, state.attack));
        if (state.hit > 0 && presentedHits.Add(state.hit) && !anticipated && !deathStarted) ShowHit(transform.position);
        Health = Mathf.Min(Health, state.health);
        if (Health == 0) { BeginConfirmedDeath(); return; }
        bool acknowledged = pendingAttack > 0 && state.actor == localActor && state.attack == pendingAttack && state.hit > 0;
        if (state.revision > frameRevision) frameRevision = state.revision;
        if (state.motionVersion > motionVersion)
        {
            bool keepLocal = pendingAttack > 0 && state.motionActor == localActor && state.motionAttack == pendingAttack;
            RetireCurrentControl();
            motionActor = state.motionActor; motionAttack = state.motionAttack; motionVersion = state.motionVersion;
            knockbackActive = state.knockbackActive; receivedSequence = 0; frames.Clear();
            motionBlocked = false;
            latestMotion = FromHost(state); receivedSequence = state.motionSequence;
            if (keepLocal)
            {
                pendingAttack = 0;
                // Un aterrizaje anterior a la concesion permanece finalizado.
                SetPhysics(SimulatesMotion);
            }
            else if (pendingAttack == 0)
            {
                localLanded = false; SetPhysics(SimulatesMotion);
                if (SimulatesMotion)
                {
                    SetBody(latestMotion); StartGroundDetection(true);
                }
                else StartCorrection();
            }
            if (knockbackActive) PauseAI(); else ResumeAI(true);
            ApplyBufferedMotion();
        }
        // Una confirmacion anterior puede llegar despues de un snapshot que ya cambio de dueno.
        if (acknowledged && pendingAttack > 0 && state.motionVersion > pendingBaseVersion)
        {
            pendingAttack = 0; localLanded = false; SetPhysics(SimulatesMotion); StartCorrection();
        }
        if (state.motionVersion != motionVersion || state.motionActor != motionActor || state.motionAttack != motionAttack) return;
        if (motionActor == hostActor && state.revision >= frameRevision)
        {
            latestMotion = FromHost(state);
            if (!SimulatesMotion) AddFrame(latestMotion);
        }
        else if (motionActor != hostActor && state.motionSequence > receivedSequence)
        {
            latestMotion = FromHost(state); receivedSequence = state.motionSequence;
            if (!SimulatesMotion) AddFrame(latestMotion);
        }
    }
    private MultiplayerSlimeMotion FromHost(MultiplayerWorldMessage state) => new MultiplayerSlimeMotion
    {
        id = Id, actor = state.motionActor, attack = state.motionAttack, sequence = state.motionSequence, time = state.time,
        position = state.position, rotation = state.rotation, velocity = state.velocity, angularVelocity = state.angularVelocity,
        animation = state.animation, normalizedTime = state.normalizedTime
    };
    public bool ReceiveMotion(int sender, MultiplayerSlimeMotion frame)
    {
        if (deathStarted || suspended || frame == null || !frame.IsValid || frame.id != Id || sender != frame.actor ||
            retiredControls.Contains(Key(frame.actor, frame.attack))) return false;
        if (!knockbackActive || frame.actor != motionActor || frame.attack != motionAttack)
        {
            ExpireCandidates();
            if (candidates.TryGetValue(sender, out var prior))
            {
                if (prior.frame.attack > frame.attack || prior.frame.attack == frame.attack && prior.frame.sequence >= frame.sequence) return false;
            }
            else if (candidates.Count >= PartyRules.MaxPlayers) return false;
            candidates[sender] = new BufferedMotion { frame = frame, receivedAt = Time.realtimeSinceStartupAsDouble };
            return false;
        }
        if (frame.sequence <= receivedSequence) return false;
        receivedSequence = frame.sequence; latestMotion = frame; lastMotionAt = Time.realtimeSinceStartupAsDouble;
        if (!SimulatesMotion) AddFrame(frame);
        if (frame.finished && authority && world != null) world.CompleteSlimeMotion(this, frame, frame.landed);
        return true;
    }
    public void ApplyBufferedMotion()
    {
        ExpireCandidates();
        if (!candidates.TryGetValue(motionActor, out var candidate) || candidate.frame.attack != motionAttack) return;
        candidates.Remove(motionActor); ReceiveMotion(candidate.frame.actor, candidate.frame);
    }
    private void ExpireCandidates()
    {
        expiredCandidates.Clear();
        foreach (var pair in candidates)
            if (Time.realtimeSinceStartupAsDouble - pair.Value.receivedAt > 1d) expiredCandidates.Add(pair.Key);
        foreach (int actor in expiredCandidates) candidates.Remove(actor);
    }
    private void AddFrame(MultiplayerSlimeMotion frame)
    {
        frames.Add(frame); if (frames.Count > 32) frames.RemoveAt(0);
    }
    private void SetBody(MultiplayerSlimeMotion frame)
    {
        body.position = frame.position; body.rotation = frame.rotation;
        transform.SetPositionAndRotation(frame.position, frame.rotation);
        if (!body.isKinematic) { body.linearVelocity = frame.velocity; body.angularVelocity = frame.angularVelocity; }
    }
    private void StartCorrection()
    {
        correctionFrom = transform.position; correctionRotation = transform.rotation;
        correctionAt = Time.realtimeSinceStartupAsDouble;
        if (latestMotion != null) AddFrame(latestMotion);
    }
    public void RejectLocalKnockback(int actor, int attack)
    {
        if (actor != localActor || attack != pendingAttack || pendingAttack <= 0) return;
        pendingAttack = 0; localLanded = false; motionBlocked = true; SetPhysics(false);
        StartCorrection();
    }
    private void FinishKnockback(bool landed = true)
    {
        MultiplayerSlimeMotion final = CaptureMotion(landed); final.finished = true;
        localFinal = final;
        localLanded = true;
        if (landed) animator.SetTrigger("Land");
        if (authority) world.CompleteSlimeMotion(this, final, landed);
        else
        {
            SetPhysics(false); world.PublishSlimeLanding(final);
        }
    }
    public bool NeedsHostRecovery(double now, bool ownerPresent) => authority && !deathStarted && knockbackActive &&
        motionActor != hostActor && (!ownerPresent || now - lastMotionAt >= 1d || now - delegatedAt >= 5d);
    public void ReturnMotionToHost(MultiplayerSlimeMotion final, bool landed)
    {
        if (!authority || deathStarted) return;
        RetireCurrentControl();
        if (final != null) latestMotion = final;
        motionActor = hostActor; motionAttack = 0; motionVersion++; knockbackActive = !landed;
        pendingAttack = 0; localLanded = false; motionBlocked = false; receivedSequence = 0; frames.Clear(); candidates.Clear();
        SetPhysics(SimulatesMotion);
        if (latestMotion != null) SetBody(latestMotion);
        if (landed) ResumeAI(true); else { PauseAI(); StartGroundDetection(true); }
    }
    public void RecoverMotionToHost() { ReturnMotionToHost(latestMotion, false); }
    public void SuspendMotion()
    {
        suspended = true; pendingAttack = 0; localLanded = false; motionBlocked = true;
        candidates.Clear(); frames.Clear(); anticipatedHits.Clear(); retiredControls.Clear();
        correctionAt = double.NegativeInfinity; SetPhysics(false);
    }
    public void ResumeMotion()
    {
        suspended = false;
        if (authority) motionBlocked = false;
        if (authority && motionActor != hostActor) RecoverMotionToHost();
        SetPhysics(SimulatesMotion);
    }
    private void Update()
    {
        ExpireCandidates();
        if (authority && deathStarted && world != null && world.Playing)
        { deathAge += Time.deltaTime; if (deathAge > 3f) DestroyEnemy(); }
        if (world == null || !world.Playing || suspended || deathStarted) return;
        if (pendingAttack > 0 && Time.realtimeSinceStartupAsDouble - pendingAt >= 5d)
            RejectLocalKnockback(localActor, pendingAttack);
        if (!authority && SimulatesMotion && KnockbackActive && Time.timeScale <= 0f)
        { FinishKnockback(false); return; }
        if (SimulatesMotion || frames.Count == 0) return;
        double render = PhotonNetwork.Time - 0.1d;
        while (frames.Count > 2 && frames[1].time <= render) frames.RemoveAt(0);
        var from = frames[0]; var to = frames.Count > 1 ? frames[1] : from;
        float fraction = to.time > from.time ? Mathf.Clamp01((float)((render - from.time) / (to.time - from.time))) : 1f;
        Vector3 position = Vector3.Lerp(from.position, to.position, fraction);
        Quaternion rotation = Quaternion.Slerp(from.rotation, to.rotation, fraction);
        float correction = Mathf.Clamp01((float)(Time.realtimeSinceStartupAsDouble - correctionAt) / 0.1f);
        transform.SetPositionAndRotation(Vector3.Lerp(correctionFrom, position, correction), Quaternion.Slerp(correctionRotation, rotation, correction));
        var state = fraction < 1f ? from : to;
        if (state.animation != 0 && state.animation != DeathAnimation && (lastAnimation != state.animation ||
            Mathf.Abs(animator.GetCurrentAnimatorStateInfo(0).normalizedTime - state.normalizedTime) > 0.3f))
        { animator.Play(state.animation, 0, state.normalizedTime); lastAnimation = state.animation; }
    }
    private void BeginConfirmedDeath()
    {
        if (deathStarted) return;
        deathStarted = true; pendingAttack = 0; knockbackActive = false;
        candidates.Clear(); frames.Clear(); localLanded = false; PauseAI();
        if (!body.isKinematic) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
        SetPhysics(false);
        SpawnDeathEffect();
        animator.ResetTrigger("StartJump"); animator.ResetTrigger("Land"); animator.ResetTrigger("Die");
        animator.Play(DeathAnimation, 0, 0f); lastAnimation = DeathAnimation;
    }
    public void ApplyJumpForce()
    {
        if (CanRunHostAI && world != null && world.Playing) GetComponent<HostSlimeMovement>().Launch();
    }
    public void DestroyEnemy() { if (authority && Health == 0 && world != null) world.Despawn(this); }
    private void ShowFlash() { if (materials == null) return; if (flash != null) StopCoroutine(flash); flash = StartCoroutine(Flash()); }
    private IEnumerator Flash()
    {
        for (int i = 0; i < materials.Length; i++) materials[i].color = flashColor;
        yield return new WaitForSecondsRealtime(flashDuration);
        for (int i = 0; i < materials.Length; i++) materials[i].color = colors[i];
        flash = null;
    }
    private void OnDisable() { SuspendMotion(); }
    private void OnDestroy() { if (materials != null) foreach (Material material in materials) Destroy(material); }
    private void SpawnHitEffect(Vector3 position)
    { if (hitEffectPrefab != null) Instantiate(hitEffectPrefab, position, Quaternion.identity); }
    private void SpawnDeathEffect()
    {
        if (deathEffectPrefab == null || deathEffectSpawned) return;
        deathEffectSpawned = true; Instantiate(deathEffectPrefab, transform.position + deathEffectOffset, Quaternion.identity);
    }
}
