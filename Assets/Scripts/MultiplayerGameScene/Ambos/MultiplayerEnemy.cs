using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

// Reemplaza EnemyCombat solamente en las copias nuevas.
public sealed class MultiplayerEnemy : MonoBehaviour
{
    [SerializeField] private int maxHealth = 10, contactDamage = 5;
    [SerializeField] private float knockbackForce = 5f, knockbackUpForce = 2f, weaponKnockbackMultiplier = 1f;
    [SerializeField] private Renderer rend;
    [SerializeField] private Color flashColor = Color.red;
    [SerializeField] private float flashDuration = 0.1f;
    [SerializeField] private bool authority;
    private Rigidbody body;
    private Animator animator;
    private MultiplayerWorldSession world;
    private Material[] materials;
    private Color[] colors;
    private Coroutine flash;
    private int frameRevision, effectRevision, lastAnimation;
    private float deathAge;
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
    public void TakeAuthoritativeHit(int damage, Vector3 origin)
    {
        if (!authority || Health <= 0 || damage <= 0) return;
        Health = Mathf.Max(0, Health - damage); HitRevision++;
        ShowFlash();
        if (Health == 0) { animator.SetTrigger("Die"); body.linearVelocity = Vector3.zero; return; }
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
            health = Health, hit = HitRevision, position = transform.position, rotation = transform.rotation,
            velocity = body.linearVelocity, animation = state.fullPathHash, normalizedTime = state.normalizedTime, time = PhotonNetwork.Time };
    }
    public void Receive(MultiplayerWorldMessage state)
    {
        if (authority || state.revision <= 0 || state.health < 0 || !MultiplayerGameplayProtocol.Finite(state.normalizedTime)) return;
        // Un frame no fiable adelantado no puede ocultar el feedback del evento fiable.
        if (state.hit > effectRevision)
        {
            effectRevision = state.hit; ShowFlash();
        }
        if (state.health == 0 && Health > 0) { Health = 0; animator.SetTrigger("Die"); }
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
        if (state.animation != 0 && (lastAnimation != state.animation || Mathf.Abs(animator.GetCurrentAnimatorStateInfo(0).normalizedTime - state.normalizedTime) > 0.3f))
        { animator.Play(state.animation, 0, state.normalizedTime); lastAnimation = state.animation; }
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
    private void OnDestroy() { if (materials != null) foreach (Material material in materials) Destroy(material); }
}
