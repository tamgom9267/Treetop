using UnityEngine;
using UnityEngine.AI;

public class SlimeEnemyMovement : MonoBehaviour
{
    [Header("Referencias")]

    private SlimeEnemy slimeEnemy;
    private Rigidbody rb;
    private Animator animator;

    private Transform player;

    [Header("Salto")]

    // Tiempo minimo que espera entre un salto y el siguiente.
    [SerializeField] private float minJumpCooldown = 0.8f;
    [SerializeField] private float maxJumpCooldown = 1.3f;

    // Fuerza horizontal maxima del salto.
    [SerializeField] private float horizontalJumpForce = 5f;

    // Fuerza vertical del salto.
    [SerializeField] private float verticalJumpForce = 3f;

    [Header("Rotacion")]
    [SerializeField] private float turnSpeed = 360f;

    [Header("NavMesh")]

    // Distancia maxima para comprobar que jugador y slime estan sobre NavMesh.
    [SerializeField] private float navMeshCheckDistance = 1f;

    private NavMeshPath path;
    private Vector3 jumpDirection;

    private float nextJumpTime;
    private bool jumpCycleActive;

    private void Awake()
    {
        // Obtine automaticamente las referencias
        if (slimeEnemy == null)
            slimeEnemy = GetComponent<SlimeEnemy>();

        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (animator == null)
            animator = GetComponent<Animator>();

        // guarda un objeto para calcular caminos
        path = new NavMeshPath();
    }

    private void Start()
    {
        // busa el jugador usando su tag
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");

        if (playerObject != null)
        {
            player = playerObject.transform;
        }
        // Cada slime espera un tiempo inicial distinto antes de su primer salto.
        nextJumpTime = Time.time + Random.Range(minJumpCooldown, maxJumpCooldown);
    }

    private void Update()
    {
        // El slime no calcula rutas ni salta antes de que el Area 3 se active.
        if (!slimeEnemy.isActive)
            return;
        
        UpdateJumpDirection();

        // Si el slime esta en el aire, en la animacion, o aun no termina el tiempo de salto, no inicia otro salto
        if (jumpCycleActive || Time.time < nextJumpTime)
            return;

        StartJump();
    }

    private void StartJump()
    {
        // Hace un calculo final justo antes de iniciar el salto.
        UpdateJumpDirection();

        // Si no encontro un camino valido, no salta.
        if (jumpDirection == Vector3.zero)
            return;

        // Evita iniciar otro salto mientras carga o esta en el aire.
        jumpCycleActive = true;

        // Inicia SlimeJumpCharge.
        animator.SetTrigger("StartJump");
    }

    // Calcula el siguiente punto de la ruta y hace que el slime lo mire.
    private void UpdateJumpDirection()
    {
        // Por defecto, no hay una direccion valida.
        jumpDirection = Vector3.zero;

        if (player == null)
            return;

        // Comprueba que el slime y el jugador esten dentro del NavMesh.
        if (!NavMesh.SamplePosition(transform.position, out NavMeshHit slimeNavMeshPosition, navMeshCheckDistance, NavMesh.AllAreas))
            return;
        if (!NavMesh.SamplePosition(player.position, out NavMeshHit playerNavMeshPosition, navMeshCheckDistance, NavMesh.AllAreas))
            return;

        // Calcula la ruta entre el slime y el jugador.
        bool pathFound = NavMesh.CalculatePath(slimeNavMeshPosition.position, playerNavMeshPosition.position, NavMesh.AllAreas, path);

        // Si no existe una ruta completa, no calcula salto.
        if (!pathFound || path.status != NavMeshPathStatus.PathComplete)
            return;

        // Un camino requiere al menos inicio y siguiente punto.
        if (path.corners.Length < 2)
            return;

        // El slime salta hacia el siguiente punto util de la ruta.
        jumpDirection = path.corners[1] - transform.position;

        // La direccion de navegacion usa solo X y Z.
        jumpDirection.y = 0f;

        if (jumpDirection == Vector3.zero)
            return;

        jumpDirection.Normalize();

        // Calcula la rotacion hacia donde saltara.
        Quaternion targetRotation = Quaternion.LookRotation(jumpDirection);

        // Rota hacia la direccion calculada.
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
    }

    // Animation Event dentro de SlimeJump. Aplica la fuerza para que salte.
    public void ApplyJumpForce()
    {
        // Aplica las fuerzas
        Vector3 jumpForce = new Vector3(
            jumpDirection.x * horizontalJumpForce,
            verticalJumpForce,
            jumpDirection.z * horizontalJumpForce
        );

        rb.AddForce(jumpForce, ForceMode.Impulse);

        // Cada nuevo salto recibe un cooldown aleatorio.
        nextJumpTime = Time.time + Random.Range(minJumpCooldown, maxJumpCooldown);
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Solo importa si este slime estaba saltando.
        if (!jumpCycleActive)
            return;

        // El salto termina al tocar una superficie solida.
        if (!collision.gameObject.CompareTag("Obstacle"))
            return;

        // Activa SlimeLand.
        animator.SetTrigger("Land");

        // El ciclo de salto termino.
        jumpCycleActive = false;
    }
}
