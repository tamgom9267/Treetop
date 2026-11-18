using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Rigidbody), typeof(MultiplayerEnemy))]
public sealed class HostGhostMovement : MonoBehaviour
{
    [Header("Teletransporte")]
    [SerializeField] private float minHiddenTime = 1.5f;
    [SerializeField] private float maxHiddenTime = 2.8f;
    [SerializeField] private float teleportDistance = 3.5f;
    [SerializeField] private float navMeshSampleRadius = 1.5f;

    [Header("Movimiento y giro")]
    [SerializeField] private float moveSpeed = 3.5f;
    [SerializeField] private float turnSpeed = 720f;
    [SerializeField] private float missileSpinSpeed = 900f;
    [SerializeField] private float visibleDuration = 2.5f;
    [SerializeField] private float knockbackDuration = 0.25f;

    private Rigidbody body;
    private MultiplayerEnemy enemy;
    private float initialY;
    private float nextAppearTime;
    private float vanishTime;
    private float knockbackUntil;
    private Quaternion lookRotation;
    private float rollAngle;
    private bool hasTarget;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        enemy = GetComponent<MultiplayerEnemy>();
        initialY = body.position.y;
    }

    private void Start()
    {
        nextAppearTime = Time.time + Random.Range(minHiddenTime, maxHiddenTime);
    }

    private void Update()
    {
        MultiplayerWorldSession world = MultiplayerWorldSession.Instance;
        if (!enemy.CanRunGhostAI || world == null || !world.Playing) return;

        if (!enemy.GhostHidden && Time.time >= vanishTime)
        {
            Vanish();
            return;
        }

        if (enemy.GhostHidden && Time.time < nextAppearTime) return;
        hasTarget = world.TryNearest(enemy.RoomId, body.position, out Vector3 target);
        if (!hasTarget) return;

        if (enemy.GhostHidden)
        {
            Vector3 side = Random.value > 0.5f ? Vector3.left : Vector3.right;
            if (TryAppear(target, side) || TryAppear(target, -side) ||
                TryAppear(target, Vector3.forward) || TryAppear(target, Vector3.back)) return;

            nextAppearTime = Time.time + 0.3f;
            return;
        }

        Vector3 direction = target - body.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.0001f)
            lookRotation = Quaternion.RotateTowards(lookRotation, Quaternion.LookRotation(direction), turnSpeed * Time.deltaTime);

        rollAngle = Mathf.Repeat(rollAngle + missileSpinSpeed * Time.deltaTime, 360f);
        body.rotation = lookRotation * Quaternion.Euler(0f, 0f, rollAngle);
    }

    private void FixedUpdate()
    {
        if (!enemy.CanRunGhostAI || enemy.GhostHidden || body.isKinematic) return;

        Vector3 position = body.position;
        if (!Mathf.Approximately(position.y, initialY))
        {
            position.y = initialY;
            body.position = position;
        }

        if (Time.time >= knockbackUntil)
            body.linearVelocity = hasTarget ? lookRotation * Vector3.forward * moveSpeed : Vector3.zero;
    }

    private bool TryAppear(Vector3 target, Vector3 direction)
    {
        Vector3 candidate = target + direction * teleportDistance;
        if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, navMeshSampleRadius, NavMesh.AllAreas)) return false;

        Vector3 position = new Vector3(hit.position.x, initialY, hit.position.z);
        Vector3 facing = target - position;
        facing.y = 0f;
        lookRotation = facing.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(facing) : body.rotation;
        rollAngle = 0f;
        body.position = position;
        body.rotation = lookRotation;
        transform.SetPositionAndRotation(position, lookRotation);
        vanishTime = Time.time + visibleDuration;
        enemy.SetGhostHidden(false);
        return true;
    }

    public void Vanish()
    {
        if (!enemy.CanRunGhostAI || enemy.GhostHidden) return;

        nextAppearTime = Time.time + Random.Range(minHiddenTime, maxHiddenTime);
        knockbackUntil = 0f;
        enemy.SetGhostHidden(true);
    }

    internal void PauseForKnockback()
    {
        knockbackUntil = Time.time + knockbackDuration;
    }
}
