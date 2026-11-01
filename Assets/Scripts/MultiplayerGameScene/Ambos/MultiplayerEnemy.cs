using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

// Reemplaza EnemyCombat solamente en las copias nuevas.
public sealed class MultiplayerEnemy : MonoBehaviour
{
    private static readonly int DeathAnimation = Animator.StringToHash("Base Layer.SlimeDeath");
    private const float RecoilDuration = 0.20f, ReconcileDuration = 0.10f;
    private const float RecoilDistance = 0.5f, RecoilHeight = 0.15f;
    [SerializeField] private int maxHealth = 10, contactDamage = 5;
    [SerializeField] private float knockbackForce = 5f, knockbackUpForce = 2f, weaponKnockbackMultiplier = 1f;
    [SerializeField] private Renderer rend;
    [SerializeField] private Color flashColor = Color.red;
    [SerializeField] private float flashDuration = 0.1f;

    [Header("Visual Effects")]
    [SerializeField] private GameObject hitEffectPrefab;
    [SerializeField] private GameObject deathEffectPrefab;
    [SerializeField] private Vector3 deathEffectOffset = Vector3.zero;

    private bool deathEffectSpawned, deathStarted;

    [SerializeField] private bool authority;
    private Rigidbody body;
    private Animator animator;
    private MultiplayerWorldSession world;
    private Material[] materials;
    private Color[] colors;
    private Coroutine flash;
    private int frameRevision, lastAnimation, lastHitActor, lastHitAttack;
    private float deathAge;
    // La instancia del enemigo aporta el tercer elemento de la clave (actor, ataque, enemigo).
    // Se conservan las claves resueltas para reconocer confirmaciones fiables tardias.
    private readonly Dictionary<long, bool> predictedHits = new Dictionary<long, bool>();
    private readonly HashSet<int> presentedHits = new HashSet<int>();
    private Transform combatVisual;
    private Vector3 visualRestPosition, recoilDirection, visualOffset, reconcileOffset;
    private double recoilStartedAt, reconcileStartedAt;
    private long activePrediction;
    private bool recoiling, reconciling;
    private readonly List<MultiplayerWorldMessage> frames = new List<MultiplayerWorldMessage>();
    public string Id { get; private set; }
    public string RoomId { get; private set; }
    public int Type { get; private set; }
    public int Health { get; private set; }
    public int HitRevision { get; private set; }
    public bool Authority => authority;
    public float AttackReach => 6f + GetComponent<Collider>().bounds.extents.magnitude;
    private void Awake()
    {
        body = GetComponent<Rigidbody>(); animator = GetComponent<Animator>(); Health = maxHealth;
        body.isKinematic = !authority;
        if (!authority) { body.useGravity = false; animator.updateMode = AnimatorUpdateMode.UnscaledTime; }
        if (rend != null)
        {
            if (!authority && rend.transform != transform)
            { combatVisual = rend.transform; visualRestPosition = combatVisual.localPosition; }
            materials = rend.materials; colors = new Color[materials.Length];
            for (int i = 0; i < colors.Length; i++) colors[i] = materials[i].color;
        }
    }
    public void Initialize(MultiplayerWorldSession owner, string id, string room, int type)
    { world = owner; Id = id; RoomId = room; Type = type; }
    private void OnTriggerEnter(Collider other)
    {
        if (Health <= 0 || world == null || !world.Playing) return;
        WeaponObject weapon = other.GetComponent<WeaponObject>();
        if (other.CompareTag("Weapon") && weapon != null) world.RequestHit(this, weapon);
    }
    private void OnCollisionEnter(Collision collision)
    {
        if (Health <= 0 || world == null || !world.Playing) return;
        PlayerController target = collision.collider.GetComponentInParent<PlayerController>();
        if (target == null || target != MultiplayerStartingRoomSpawns.LocalPlayer) return;
        target.playerClass.TakeDamage(contactDamage);
        target.ReceiveHit(gameObject);
    }
    public void TakeAuthoritativeHit(int damage, Vector3 origin, int actor = 0, int attack = 0)
    {
        if (!authority || Health <= 0 || damage <= 0) return;
        Health = Mathf.Max(0, Health - damage); HitRevision++;
        lastHitActor = actor; lastHitAttack = attack;
        ShowFlash();
        
        Collider enemyCollider = GetComponent<Collider>();

        Vector3 hitPosition = enemyCollider != null
            ? enemyCollider.ClosestPoint(origin)
            : transform.position;

        SpawnHitEffect(hitPosition);


        if (Health == 0)
        {
            BeginConfirmedDeath();
            body.linearVelocity = Vector3.zero;
            return;
        }
        Vector3 direction = transform.position - origin; direction.y = 0;
        if (direction.sqrMagnitude > 0.0001f)
        {
            direction.Normalize(); body.linearVelocity = Vector3.zero;
            body.AddForce((direction * knockbackForce + Vector3.up * knockbackUpForce) * weaponKnockbackMultiplier, ForceMode.Impulse);
        }
    }
    public MultiplayerWorldMessage Capture(int revision)
    {
        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        return new MultiplayerWorldMessage { op = "enemy", id = Id, room = RoomId, type = Type, revision = revision,
            health = Health, hit = HitRevision, actor = lastHitActor, attack = lastHitAttack,
            position = transform.position, rotation = transform.rotation, velocity = body.linearVelocity,
            animation = Health == 0 ? DeathAnimation : state.fullPathHash,
            normalizedTime = Health == 0 && state.fullPathHash != DeathAnimation ? 0f : state.normalizedTime, time = PhotonNetwork.Time };
    }
    public void Receive(MultiplayerWorldMessage state)
    {
        if (authority || state.revision <= 0 || state.health < 0 || !MultiplayerGameplayProtocol.Finite(state.normalizedTime)) return;
        // Resolver antes de filtrar revisiones: otro golpe puede haber adelantado el estado.
        bool anticipated = ResolvePrediction(state.actor, state.attack);
        if (state.hit > 0 && presentedHits.Add(state.hit) && !anticipated && !deathStarted)
        {
            ShowFlash();
            SpawnHitEffect(transform.position);
        }
        if (state.health == 0 && !deathStarted)
        {
            Health = 0;
            BeginConfirmedDeath();
        }
        if (state.revision <= frameRevision) return;
        frameRevision = state.revision; Health = Mathf.Min(Health, state.health);
        if (frames.Count == 0) transform.SetPositionAndRotation(state.position, state.rotation);
        frames.Add(state); if (frames.Count > 32) frames.RemoveAt(0);
    }
    private void Update()
    {
        if (authority)
        {
            if (Health == 0 && world != null && world.Playing)
            { deathAge += Time.deltaTime; if (deathAge > 3f) DestroyEnemy(); }
            return;
        }
        if (frames.Count == 0 || world == null || !world.Playing) return;
        double render = PhotonNetwork.Time - 0.1d;
        while (frames.Count > 2 && frames[1].time <= render) frames.RemoveAt(0);
        var from = frames[0]; var to = frames.Count > 1 ? frames[1] : from;
        float fraction = to.time > from.time ? Mathf.Clamp01((float)((render - from.time)/(to.time - from.time))) : 1f;
        transform.SetPositionAndRotation(Vector3.Lerp(from.position, to.position, fraction), Quaternion.Slerp(from.rotation, to.rotation, fraction));
        var state = fraction < 1f ? from : to;
        if (!deathStarted && state.animation != 0 && (lastAnimation != state.animation || Mathf.Abs(animator.GetCurrentAnimatorStateInfo(0).normalizedTime - state.normalizedTime) > 0.3f))
        { animator.Play(state.animation, 0, state.normalizedTime); lastAnimation = state.animation; }
    }
    public bool PredictHit(int actor, int attack, Vector3 origin)
    {
        if (authority || Health <= 0 || actor <= 0 || attack <= 0 || !MultiplayerGameplayProtocol.Finite(origin)) return false;
        long key = PredictionKey(actor, attack);
        if (predictedHits.ContainsKey(key)) return false;
        predictedHits.Add(key, false);
        ShowFlash();
        Collider enemyCollider = GetComponent<Collider>();
        SpawnHitEffect(enemyCollider != null ? enemyCollider.ClosestPoint(origin) : transform.position);
        recoilDirection = transform.position - origin; recoilDirection.y = 0f;
        recoilDirection.Normalize();
        activePrediction = key;
        recoilStartedAt = Time.realtimeSinceStartupAsDouble;
        recoiling = true; reconciling = false;
        return true;
    }
    public void RejectPredictedHit(int actor, int attack) { ResolvePrediction(actor, attack); }
    private static long PredictionKey(int actor, int attack) => ((long)actor << 32) | (uint)attack;
    private bool ResolvePrediction(int actor, int attack)
    {
        if (actor <= 0 || attack <= 0) return false;
        long key = PredictionKey(actor, attack);
        if (!predictedHits.TryGetValue(key, out bool resolved)) return false;
        if (!resolved)
        {
            predictedHits[key] = true;
            if (activePrediction == key) ReconcileVisual();
        }
        return true;
    }
    private void ReconcileVisual()
    {
        if (!recoiling && !reconciling) return;
        reconcileOffset = visualOffset;
        reconcileStartedAt = Time.realtimeSinceStartupAsDouble;
        recoiling = false; reconciling = true;
    }
    private void LateUpdate()
    {
        if (combatVisual == null) return;
        double now = Time.realtimeSinceStartupAsDouble;
        if (reconciling)
        {
            float t = Mathf.Clamp01((float)(now - reconcileStartedAt) / ReconcileDuration);
            visualOffset = Vector3.Lerp(reconcileOffset, Vector3.zero, t);
            if (t >= 1f) reconciling = false;
        }
        else if (recoiling)
        {
            float t = Mathf.Clamp01((float)(now - recoilStartedAt) / RecoilDuration);
            visualOffset = (recoilDirection * RecoilDistance + Vector3.up * RecoilHeight) * Mathf.Sin(t * Mathf.PI);
            if (t >= 1f) { recoiling = false; visualOffset = Vector3.zero; }
        }
        // Convertir cada frame: el Animator puede cambiar la escala de la raiz.
        Vector3 localOffset = transform.InverseTransformVector(visualOffset);
        combatVisual.localPosition = visualRestPosition + (MultiplayerGameplayProtocol.Finite(localOffset) ? localOffset : Vector3.zero);
    }
    public void ClearPredictedHits()
    {
        predictedHits.Clear(); recoiling = reconciling = false;
        visualOffset = reconcileOffset = Vector3.zero;
        if (combatVisual != null) combatVisual.localPosition = visualRestPosition;
    }
    private void BeginConfirmedDeath()
    {
        if (deathStarted) return;
        deathStarted = true;
        ReconcileVisual();
        SpawnDeathEffect();
        animator.ResetTrigger("StartJump"); animator.ResetTrigger("Land"); animator.ResetTrigger("Die");
        animator.Play(DeathAnimation, 0, 0f);
        lastAnimation = DeathAnimation;
    }
    // Animation Events de los clips originales. La replica no ejecuta fuerzas ni despawns.
    public void ApplyJumpForce()
    {
        if (authority && Health > 0 && world != null && world.Playing) GetComponent<HostSlimeMovement>().Launch();
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
    private void OnDisable() { ClearPredictedHits(); }
    private void OnDestroy() { ClearPredictedHits(); if (materials != null) foreach (Material material in materials) Destroy(material); }

    private void SpawnHitEffect(Vector3 position)
    {
        if (hitEffectPrefab == null)
            return;

        Instantiate(
            hitEffectPrefab,
            position,
            Quaternion.identity
        );
    }

    private void SpawnDeathEffect()
    {
        if (deathEffectPrefab == null || deathEffectSpawned)
            return;

        deathEffectSpawned = true;

        Instantiate(
            deathEffectPrefab,
            transform.position + deathEffectOffset,
            Quaternion.identity
        );
    }
}
