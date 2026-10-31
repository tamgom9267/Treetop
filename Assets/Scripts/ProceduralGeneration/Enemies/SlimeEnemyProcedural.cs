using UnityEngine;
using System.Collections;

public class SlimeEnemyProcedural : MonoBehaviour
{
    [Header("Estadisticas del slime")]
    [SerializeField] private int vidaSlime = 10;

    [Header("Ataque del slime")]

    // Dano que este slime hace al tocar al jugador.
    [SerializeField] private int contactDamage = 5;

    [Header("Knockback recibido")]

    // Fuerza horizontal con la que el slime es empujado al ser golpeado.
    [SerializeField] private float knockbackForce = 5f;

    // Fuerza vertical que eleva un poco al slime.
    [SerializeField] private float knockbackUpForce = 2f;

    [Header("Hit Feedback")]
    public Renderer rend;
    public Color flashColor = Color.red;
    public float flashDuration = 0.1f;

    [Header("Hit Stop")]
    [SerializeField] private float hitStopDuration = 0.08f;

    private Material[] slimeMaterials;
    private Color[] originalColors;

    [Header("Referencias")]
    private Rigidbody rb;
    private Animator animator;

    // Referencia a las estadisticas del jugador.
    private PlayerClass playerClass;

    private void Awake()
    {
        // Obtiene el Rigidbody del slime.
        rb = GetComponent<Rigidbody>();

        // obtiene el animator del slime
        animator = GetComponent<Animator>();
    }

    private void Start()
    {
        // Busca al Player para obtener su WD y PD.
        GameObject player = GameObject.FindGameObjectWithTag("Player");

        if (player != null)
            playerClass = player.GetComponent<PlayerClass>();

        slimeMaterials = rend.materials;
        originalColors = new Color[slimeMaterials.Length];

        for (int i = 0; i < slimeMaterials.Length; i++)
            originalColors[i] = slimeMaterials[i].color;
    }

    private void OnTriggerEnter(Collider other)
    {
        float knockbackMultiplier;

        // Si el arma es un Weapon.
        if (other.CompareTag("Weapon"))
        {
            //Debug.Log("Se ataco con Weapon");

            WeaponObject weaponObject = other.GetComponent<WeaponObject>();

            // Obtiene al jugador con la espada y valida el golpe.
            if (weaponObject == null || weaponObject.player == null || !weaponObject.player.RegisterEnemyHit(gameObject))
                return;

            // Define el multiplicador de knockback
            knockbackMultiplier = 1f;

            // Pausa la animacion del atacante y retrasa el knockback hasta que termine.
            weaponObject.player.TriggerHitStop(hitStopDuration);
            StartCoroutine(ApplyKnockbackAfterHitStop(other.transform.position, knockbackMultiplier));
            
            // Aplica el Damage
            if (playerClass != null)
                TakeDamage(playerClass.WD);
        }
        
        // Si el arma es un Proyectil
        else if (other.CompareTag("Projectile"))
        {
            //Debug.Log("Se ataco con Projectile");
            
            // Define el multiplicador de knockback
            knockbackMultiplier = 1f / 3f;

            // Aplica el knockback
            ApplyKnockback(other.transform.position, knockbackMultiplier);

            // Aplica el Damage
            if (playerClass != null)
            TakeDamage(playerClass.PD);
        }
    }

    // Knockback al slime
    private IEnumerator ApplyKnockbackAfterHitStop(Vector3 attackerPosition, float knockbackMultiplier)
    {
        yield return new WaitForSecondsRealtime(hitStopDuration);
        ApplyKnockback(attackerPosition, knockbackMultiplier);
    }

    private void ApplyKnockback(Vector3 attackerPosition, float knockbackMultiplier)
    {
        // Direccion desde el atacante hacia el slime.
        Vector3 knockbackDirection = transform.position - attackerPosition;

        // X y Z son el empuje hacia atras; Y se usa solo para elevarlo.
        knockbackDirection.y = 0f;

        // Evita aplicar una direccion invalida.
        if (knockbackDirection == Vector3.zero)
            return;

        knockbackDirection.Normalize();

        // Detiene cualquier movimiento previo antes del knockback.
        rb.linearVelocity = Vector3.zero;

        // Crea un vector para aplicar la fuerza
        Vector3 knockback = new Vector3(
            knockbackDirection.x * knockbackForce * knockbackMultiplier,
            knockbackUpForce * knockbackMultiplier,
            knockbackDirection.z * knockbackForce * knockbackMultiplier
        );
        
        // Aplica las furzas definidas en el vector como un golpe seco.
        rb.AddForce(knockback, ForceMode.Impulse);
    }

    private void TakeDamage(int damage)
    {
        // Evita valores de dano invalidos.
        if (damage <= 0)
            return;

        vidaSlime -= damage;

        Debug.Log("Slime recibio " + damage + " de dano. Vida actual: " + vidaSlime);
        StartCoroutine(DoFlash());

        // Se elimina al llegar a cero.
        if (vidaSlime <= 0)
            animator.SetTrigger("Die");
    }

    public void DestroySlime()
    {
        Destroy(gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Si colisiona con el es un Player
        if (!collision.gameObject.CompareTag("Player"))
            return;

        // Obtiene los scripts necesarios del jugador.
        PlayerClass playerClass = collision.gameObject.GetComponent<PlayerClass>();
        PlayerController playerController = collision.gameObject.GetComponent<PlayerController>();

        // // Aplica el dano del slime al jugador.
        // if (playerClass != null)
        //     playerClass.TakeDamage(contactDamage);

        // // Aplica el knockback.
        // if (playerController != null)
        //     playerController.ApplyEnemyKnockback(gameObject);

        if (playerClass != null)
        {
            playerClass.TakeDamage(contactDamage);
            playerController.ReceiveHit(gameObject);
        }
    }

    private IEnumerator DoFlash()
    {
        for (int i = 0; i < slimeMaterials.Length; i++)
            slimeMaterials[i].color = flashColor;

        yield return new WaitForSeconds(flashDuration);

        for (int i = 0; i < slimeMaterials.Length; i++)
            slimeMaterials[i].color = originalColors[i];
    }
}
