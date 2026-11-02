using UnityEngine;
using System.Collections;
using System.Runtime.InteropServices.WindowsRuntime;
using Unity.VisualScripting;
using System.Runtime.CompilerServices;

public class EnemyCombat : MonoBehaviour
{
    [Header("Estadisticas")]
    [SerializeField] private int maxHealth = 10;
    [SerializeField] private int contactDamage = 5;

    private int currentHealth;
    private bool isDead;

    [Header("Knockback recibido")]
    [SerializeField] private float knockbackForce = 5f;
    [SerializeField] private float knockbackUpForce = 2f;

    [Header("Knockback de ataque")]
    [SerializeField] private float weaponKnockbackMultiplier = 1f;
    [SerializeField] private float projectileKnockbackMultiplier = 1f / 3f;

    [Header("Feedback visual")]
    [SerializeField] private Renderer rend;
    [SerializeField] private Color flashColor = Color.red;
    [SerializeField] private float flashDuration = 0.1f;

    private Material[] enemyMaterials;
    private Color[] originalEmissionColors;
    private Coroutine flashCoroutine;

    [Header("HitStop")]
    [SerializeField] private float hitStopDuration = 0.08f;

    [Header("Estado")]
    [SerializeField] private bool startsActive = true;

    [Header("Particulas de Golpe")]
    [SerializeField] private ParticleSystem hitEffectPrefab;

    [Header("Particulas de Muerte")]
    [SerializeField] private GameObject deathEffectPrefab;
    private bool deathEffectSpawned = false;

    private bool isActive;

    [Header("Referencias")]
    private Rigidbody rb;
    private Animator animator;
    private PlayerClass playerClass;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();

        currentHealth = maxHealth;
        isActive = startsActive;

        if (rend != null)
        {
            enemyMaterials = rend.materials;
            originalEmissionColors = new Color[enemyMaterials.Length];

            for (int i = 0; i < enemyMaterials.Length; i++)
            {
                enemyMaterials[i].EnableKeyword("_EMISSION");
                originalEmissionColors[i] =
                    enemyMaterials[i].GetColor("_EmissionColor");
            }
        }
    }

    void Start()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");

        if (player != null)
        {
            playerClass = player.GetComponent<PlayerClass>();
        }
    }

    // permite activar o desactivar combate con enemigos
    public void SetCombatActive(bool active)
    {
        isActive = active;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isActive || isDead) return;

        // ataque con espada
        if (other.CompareTag("Weapon"))
        {
            WeaponObject weaponObject = other.GetComponent<WeaponObject>();

            if (weaponObject == null || 
                weaponObject.player == null || 
                !weaponObject.player.GetComponent<PlayerClass>())
            { return; }

            PlayerClass attackerClass = weaponObject.player.GetComponent<PlayerClass>();

            if (attackerClass == null) { return; }

            // evita recibir daño varias veces
            if (!weaponObject.player.RegisterEnemyHit(gameObject)) return;

            // efectos de golpe
            SpawnHitEffect(other);

            //Hitstop del jugador
            weaponObject.player.TriggerHitStop(hitStopDuration);

            // daño y knockback
            TakeDamage(attackerClass.WD);

            StartCoroutine(ApplyKnockbackAfterHitStop
                (other.transform.position,
                weaponKnockbackMultiplier));
        }
        // ataque con proyectil
        else if (other.CompareTag("Projectile"))
        {
            if (playerClass == null) return;

            // efectos de golpe
            SpawnHitEffect(other);

            TakeDamage(playerClass.PD);

            ApplyKnockback
            (other.transform.position,
            projectileKnockbackMultiplier);
        }
    }

    private IEnumerator ApplyKnockbackAfterHitStop(
        Vector3 attackerPosition, float multiplier)
    {
        yield return new WaitForSecondsRealtime(hitStopDuration);

        ApplyKnockback(attackerPosition, multiplier);
    }

    private void ApplyKnockback(Vector3 attackerPosition, float multiplier)
    {
        if (rb == null || isDead) return;

        Vector3 direction = transform.position - attackerPosition;
        direction.y = 0f;

        // esto hace que el knockback sea mas fuerte si el enemigo esta mas cerca del jugador\
        // funciona porque el return evita que se aplique knockback si el enemigo esta muy lejos del jugador, y la fuerza de knockback se multiplica por la distancia al jugador 
        if (direction.sqrMagnitude < 0.0001f) return;

        direction.Normalize();

        rb.linearVelocity = Vector3.zero;

        Vector3 knockback = new Vector3(
            direction.x * knockbackForce * multiplier,
            knockbackUpForce * multiplier,
            direction.z * knockbackForce * multiplier);

        rb.AddForce(knockback, ForceMode.Impulse);
    }

    public void TakeDamage(int damage)
    {
        if (damage <= 0 || isDead) return;

        currentHealth = Mathf.Max(0, currentHealth - damage);

        Debug.Log(gameObject.name + " recibio " + damage + " de daño. Vida actual: " + currentHealth);

        if (flashCoroutine != null) StopCoroutine(flashCoroutine);
        if (enemyMaterials != null) flashCoroutine = StartCoroutine(DoFlash());

        if (currentHealth <= 0) Die();
    }

    private void Die()
    {
        if (isDead) return;

        isDead = true;
        isActive = false;

        if (animator != null) animator.SetTrigger("Die");
    }

    // llamar desde animation event al terminar la animación de muerte
    public void DestroyEnemy()
    {
        SpawnDeathEffect();

        Destroy(gameObject);
    }

    // daño al jugador
    private void OnCollisionEnter(Collision collision)
    {
        if (!isActive || isDead) return;

        if (!collision.gameObject.CompareTag("Player")) return;

        PlayerClass targetClass = collision.gameObject.GetComponent<PlayerClass>();
        PlayerController playerController = collision.gameObject.GetComponent<PlayerController>();

        if (targetClass != null) targetClass.TakeDamage(contactDamage);
        if (playerController != null) playerController.ReceiveHit(gameObject);
    }

    // feedback visual
    private IEnumerator DoFlash()
    {
        for (int i = 0; i <enemyMaterials.Length; i++)
        {
            enemyMaterials[i].SetColor(
                "_EmissionColor", flashColor);
        }

        yield return new WaitForSeconds(flashDuration);

        for (int i = 0; i < enemyMaterials.Length; i++)
        {
            enemyMaterials[i].SetColor(
                "_EmissionColor", originalEmissionColors[i]);
        }

        flashCoroutine = null;
    }

    private void SpawnHitEffect(Collider attackerCollider)
    {
        if (hitEffectPrefab == null) return;

        Collider enemyCollider = GetComponent<Collider>();

        Vector3 contactPoint = transform.position;

        if (enemyCollider != null && attackerCollider != null)
        {
            Vector3 pointOnEnemy = enemyCollider.ClosestPoint(attackerCollider.bounds.center);

            Vector3 pointOnAttacker = attackerCollider.ClosestPoint(pointOnEnemy);

            contactPoint = (pointOnEnemy + pointOnAttacker) * 0.5f;
        }
        
        Instantiate(hitEffectPrefab, contactPoint, Quaternion.identity);
    }

    private void SpawnDeathEffect()
    {
        if (deathEffectPrefab == null || deathEffectSpawned) return;

        deathEffectSpawned = true;

        GameObject effect = Instantiate(deathEffectPrefab, transform.position, Quaternion.identity);
    }
}
