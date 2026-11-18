using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class fnatasmaenemigo : MonoBehaviour
{
    [Header("Referencias")]
    private Rigidbody rb;
    private Animator animator;
    private EnemyCombat enemyCombat;
    private Transform player;
    private Renderer[] meshRenderers;
    private Collider[] enemyColliders;

    [Header("Teletransporte")]
    [SerializeField] private float minHiddenTime = 1.5f;
    [SerializeField] private float maxHiddenTime = 2.8f;
    [SerializeField] private float teleportDistance = 3.5f;
    [SerializeField] private float navMeshSampleRadius = 1.5f;

    [Header("Movimiento y Giro de Misil")]
    [SerializeField] private float moveSpeed = 3.5f;
    [SerializeField] private float turnSpeed = 720f;
    [Tooltip("Velocidad de giro sobre su propio eje en grados por segundo (720 = 2 vueltas/seg).")]
    [SerializeField] private float missileSpinSpeed = 900f;
    [SerializeField] private float visibleDuration = 2.5f;

    private float nextAppearTime;
    private float vanishTime;
    private bool isVisible;
    private float initialYPosition;

    // Guarda la direccion hacia el jugador separada del giro de misil
    private Quaternion currentLookRotation;
    private float currentRollAngle;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();
        enemyCombat = GetComponent<EnemyCombat>();

        meshRenderers = GetComponentsInChildren<Renderer>();
        enemyColliders = GetComponentsInChildren<Collider>();

        initialYPosition = transform.position.y;
        currentLookRotation = transform.rotation;
    }

    private void Start()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            player = playerObject.transform;
        }

        SetGhostState(false);
        nextAppearTime = Time.time + Random.Range(minHiddenTime, maxHiddenTime);
    }

    private void Update()
    {
        if (player == null)
            return;

        if (isVisible)
        {
            if (enemyCombat != null && !enemyCombat.CanReceiveWeaponEffects)
                return;

            RotateAndSpinLikeMissile();

            if (Time.time >= vanishTime)
            {
                Vanish();
            }
            return;
        }

        if (Time.time >= nextAppearTime)
        {
            TryTeleportAndAppear();
        }
    }

    private void FixedUpdate()
    {
        if (!isVisible || player == null || rb == null)
            return;

        if (enemyCombat != null && !enemyCombat.CanReceiveWeaponEffects)
            return;

        // Mantiene la altura fija
        Vector3 pos = rb.position;
        pos.y = initialYPosition;
        rb.position = pos;

        // Avanza hacia el jugador respetando el knockback si recibe un golpe
        if (rb.linearVelocity.magnitude <= moveSpeed + 0.5f)
        {
            Vector3 velocity = transform.forward * moveSpeed;
            velocity.y = 0f;
            rb.linearVelocity = velocity;
        }
    }

    private void RotateAndSpinLikeMissile()
    {
        Vector3 direction = player.position - transform.position;
        direction.y = 0f;

        if (direction != Vector3.zero)
        {
            Quaternion targetLook = Quaternion.LookRotation(direction);
            currentLookRotation = Quaternion.RotateTowards(currentLookRotation, targetLook, turnSpeed * Time.deltaTime);
        }

        // Acumula el giro en el eje Z (frontal) como un misil
        currentRollAngle += missileSpinSpeed * Time.deltaTime;
        if (currentRollAngle >= 360f)
            currentRollAngle -= 360f;

        // Combina la direccion hacia el jugador con el giro sobre su propio eje
        transform.rotation = currentLookRotation * Quaternion.Euler(0f, 0f, currentRollAngle);
    }

    private void TryTeleportAndAppear()
    {
        List<Vector3> candidateDirections = new List<Vector3>();

        if (Random.value > 0.5f)
        {
            candidateDirections.Add(Vector3.left);
            candidateDirections.Add(Vector3.right);
        }
        else
        {
            candidateDirections.Add(Vector3.right);
            candidateDirections.Add(Vector3.left);
        }

        candidateDirections.Add(Vector3.forward);
        candidateDirections.Add(Vector3.back);

        foreach (Vector3 dir in candidateDirections)
        {
            Vector3 desiredPosition = player.position + dir * teleportDistance;

            if (NavMesh.SamplePosition(desiredPosition, out NavMeshHit hit, navMeshSampleRadius, NavMesh.AllAreas))
            {
                Vector3 spawnPos = new Vector3(hit.position.x, initialYPosition, hit.position.z);
                transform.position = spawnPos;
                if (rb != null) rb.position = spawnPos;

                Vector3 lookDir = player.position - transform.position;
                lookDir.y = 0f;
                if (lookDir != Vector3.zero)
                {
                    currentLookRotation = Quaternion.LookRotation(lookDir);
                    currentRollAngle = 0f;
                    transform.rotation = currentLookRotation;
                }

                SetGhostState(true);
                vanishTime = Time.time + visibleDuration;

                if (animator != null)
                    animator.SetTrigger("Appear");

                return;
            }
        }

        nextAppearTime = Time.time + 0.3f;
    }

    public void Vanish()
    {
        SetGhostState(false);
        nextAppearTime = Time.time + Random.Range(minHiddenTime, maxHiddenTime);
    }

    private void SetGhostState(bool active)
    {
        isVisible = active;

        if (rb != null)
            rb.linearVelocity = Vector3.zero;

        foreach (Renderer r in meshRenderers)
        {
            r.enabled = active;
        }

        foreach (Collider c in enemyColliders)
        {
            c.enabled = active;
        }

        if (enemyCombat != null)
        {
            enemyCombat.SetCombatActive(active);
        }
    }
}