using UnityEngine;
using UnityEngine.AI;

public sealed class HostSlimeMovement : MonoBehaviour
{
    [SerializeField] private float minJumpCooldown = 0.8f, maxJumpCooldown = 1.3f;
    [SerializeField] private float horizontalJumpForce = 5f, verticalJumpForce = 3f, turnSpeed = 360f, navMeshCheckDistance = 10f;
    private Rigidbody body;
    private Animator animator;
    private MultiplayerEnemy enemy;
    private NavMeshPath path;
    private Vector3 direction;
    private float nextJump;
    private bool jumping;
    private void Awake()
    { body = GetComponent<Rigidbody>(); animator = GetComponent<Animator>(); enemy = GetComponent<MultiplayerEnemy>(); path = new NavMeshPath(); }
    private void Start() { nextJump = Time.time + Random.Range(minJumpCooldown, maxJumpCooldown); }
    private void Update()
    {
        MultiplayerWorldSession world = MultiplayerWorldSession.Instance;
        if (!enemy.Authority || enemy.Health <= 0 || world == null || !world.Playing) return;
        // Como en solitario, actualizar la direccion y el giro en cada frame,
        // incluso durante la espera y la animacion de salto.
        UpdateJumpDirection(world);
        if (jumping || Time.time < nextJump || direction == Vector3.zero) return;
        jumping = true; animator.SetTrigger("StartJump");
    }
    private void UpdateJumpDirection(MultiplayerWorldSession world)
    {
        direction = Vector3.zero;
        if (!world.TryNearest(enemy.RoomId, transform.position, out Vector3 target) ||
            !NavMesh.SamplePosition(transform.position, out NavMeshHit start, navMeshCheckDistance, NavMesh.AllAreas) ||
            !NavMesh.SamplePosition(target, out NavMeshHit end, navMeshCheckDistance, NavMesh.AllAreas) ||
            !NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete || path.corners.Length < 2) return;
        direction = path.corners[1] - transform.position; direction.y = 0;
        if (direction.sqrMagnitude < 0.0001f) return;
        direction.Normalize();
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), turnSpeed * Time.deltaTime);
    }
    public void Launch()
    {
        body.AddForce(direction * horizontalJumpForce + Vector3.up * verticalJumpForce, ForceMode.Impulse);
        nextJump = Time.time + Random.Range(minJumpCooldown, maxJumpCooldown);
    }
    private void OnCollisionEnter(Collision collision)
    {
        if (!jumping || !collision.gameObject.CompareTag("Obstacle")) return;
        jumping = false; animator.SetTrigger("Land");
    }
}
