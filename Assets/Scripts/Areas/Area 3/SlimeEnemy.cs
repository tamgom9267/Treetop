using UnityEngine;

public class SlimeEnemy : MonoBehaviour
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

    // Define si el slime esta en el aire
    public bool inAir = false;

    [Header("Estado")]
    public bool isActive = false;

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
    }

    // Activa el slime cuando el jugador entra al Area 3.
    public void ActivateSlime()
    {
        // Evita activar dos veces al mismo slime.
        if (isActive)
            return;

        isActive = true;

        // Sale de SlimeInactive y entra a SlimeIdle.
        animator.SetTrigger("Activate");
    }

    private void OnTriggerEnter(Collider other)
    {
        // Un slime inactivo no puede recibir daño.
        if (!isActive)
            return;
        float knockbackMultiplier;

        // Si el arma es un Weapon.
        if (other.CompareTag("Weapon"))
        {
            //Debug.Log("Se ataco con Weapon");

            WeaponObject weaponObject = other.GetComponent<WeaponObject>();

            // Obtiene al jugador con la espada y valida el golpe.
            if (weaponObject == null || weaponObject.player == null || !weaponObject.player.RegisterSlimeHit(this))
                return;

            // Define el multiplicador de knockback
            knockbackMultiplier = 1f;

            // Aplica el knockback
            ApplyKnockback(other.gameObject, knockbackMultiplier);
            
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
            ApplyKnockback(other.gameObject, knockbackMultiplier);

            // Aplica el Damage
            if (playerClass != null)
            TakeDamage(playerClass.PD);
        }
    }

    // Knockback al slime
    private void ApplyKnockback(GameObject attacker, float knockbackMultiplier)
    {
        // Evita aplicar otro knockback mientras el slime sigue en el aire.
        if (inAir)
            return;
        
        // Direccion desde el atacante hacia el slime.
        Vector3 knockbackDirection = transform.position - attacker.transform.position;

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

        // Mientras recibe knockback, su movimiento automatico queda bloqueado.
        inAir = true;
        
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
        // Un slime inactivo no puede dañar ni empujar al jugador.
        if (!isActive)
            return;
            
        // Si toca una superficie solida, ya no esta en el aire.
        if (inAir && collision.gameObject.CompareTag("Obstacle") )
        {
            inAir = false;
        }


        // Si colisiona con el es un Player
        if (!collision.gameObject.CompareTag("Player"))
            return;

        // Obtiene los scripts necesarios del jugador.
        PlayerClass playerClass = collision.gameObject.GetComponent<PlayerClass>();
        PlayerController playerController = collision.gameObject.GetComponent<PlayerController>();

        // Aplica el dano del slime al jugador.
        if (playerClass != null)
            playerClass.TakeDamage(contactDamage);

        // Aplica el knockback.
        if (playerController != null)
            playerController.ApplyEnemyKnockback(gameObject);
    }
}
