using System.ComponentModel.Design.Serialization;
using System.Security.Cryptography;
using JetBrains.Annotations;
using Unity.VisualScripting.Antlr3.Runtime.Tree;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering.UI;

public class TreeRootController : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private NavMeshAgent[] rootAgents;
    [SerializeField] private Animator[] rootAnimators;

    private Transform player;
    private MultiplayerTreeRootSync multiplayerSync;

    [Header("Modelos de rama")]
    [SerializeField] private Transform[] ramaModels;

    [Header("Movimiento de rama")]
    [SerializeField] private float attackHeight = 1f;
    [SerializeField] private float verticalSpeed = 300f;

    private Vector3[] rootOffsets;
    private float[] currentHeights;
    private float[] targetHeights;

    [Header("Movimiento")]
    [SerializeField] private float attackDistance = 2f;
    [SerializeField] private float moveSpeed = 4f;

    private bool[] isAttacking;
    private bool ramaActive;
    private int rootCount;

    private void Awake()
    {
        // if null = 0, else length
        int agentCount = rootAgents == null ? 0 : rootAgents.Length;
        int animatorCount = rootAnimators == null ? 0 : rootAnimators.Length;
        int modelCount = ramaModels == null ? 0 : ramaModels.Length;

        rootCount = Mathf.Min(agentCount, Mathf.Min(animatorCount, modelCount));

        if (rootCount == 0)
        {
            /*Debug.LogError("TreeRootController - asigna al menos 1 agnete, animator y modelo de rama", this)*/;
            return;
        }

        if (agentCount != animatorCount || agentCount != modelCount)
        {
            /*Debug.LogWarning(
                $"TreeRootController: los arrays tienen tamaños diferentes. " +
                $"Se utilizarán {rootCount} raíces " +
                $"Agentes: {agentCount}, Animator: {animatorCount}, Modelos: {modelCount}", this
            )*/;
        }

        isAttacking = new bool[rootCount];
        rootOffsets = new Vector3[rootCount];
        currentHeights = new float[rootCount];
        targetHeights = new float[rootCount];
        

        // foreach (NavMeshAgent agent in rootAgents)
        // {
        //     agent.speed = moveSpeed;
        //     agent.updateRotation = false;
        //     agent.stoppingDistance = 0.5f;
        //     agent.isStopped = true;

        // }

        for (int i = 0; i < rootCount; i++)
        {
            // if (rootAgents[i] == null) continue;

            if (rootAgents[i] != null)
            {
                rootAgents[i].speed = moveSpeed;
                rootAgents[i].updateRotation = false;
                rootAgents[i].stoppingDistance = 0.5f;
                if (rootAgents[i].isOnNavMesh) rootAgents[i].isStopped = true;
            }

            if (rootAgents[i] != null && ramaModels[i] != null)
            {
                rootOffsets[i] = ramaModels[i].position - rootAgents[i].transform.position;
            }

            // rootOffsets[i] = ramaModels[i].position - rootAgents[i].transform.position;
        }

    }

    private void Start()
    {
        multiplayerSync = GetComponent<MultiplayerTreeRootSync>();
        if (multiplayerSync != null) return;
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");

        if (playerObject != null) player = playerObject.transform;
    }

    private void Update()
    {
        if (!ramaActive) return;
        if (multiplayerSync != null && !multiplayerSync.Authority) return;
        Vector3 target;
        if (multiplayerSync != null)
        {
            MultiplayerWorldSession world = MultiplayerWorldSession.Instance;
            if (world == null || !world.Playing || !world.TryNearest(multiplayerSync.RoomId, transform.position, out target)) return;
        }
        else
        {
            if (player == null) return;
            target = player.position;
        }

        for (int i = 0; i < rootCount; i++)
        {
            if (isAttacking[i]) continue;

            UpdateRootMovements(i, target);
        }
    }

    private void LateUpdate()
    {
        if (!ramaActive) return;

        for (int i = 0; i < rootCount; i++)
        {
            if (rootAgents[i] == null || ramaModels[i] == null) continue;

            // sincronizar la posición horizontal de la sombra
            Vector3 shadowPosition = rootAgents[i].transform.position;
            Vector3 targetPosition = shadowPosition + rootOffsets[i];

            // subir o bajar la rama
            currentHeights[i] = Mathf.MoveTowards(
                currentHeights[i], targetHeights[i], verticalSpeed * Time.deltaTime
            );

            targetPosition.y += currentHeights[i];

            ramaModels[i].position = targetPosition;
        }
    }

    private void UpdateRootMovements(int index, Vector3 target)
    {
        NavMeshAgent agent = rootAgents[index];

        if (agent == null || !agent.isOnNavMesh) return;

        agent.SetDestination(target);

        float distance = Vector3.Distance(
            agent.transform.position, target
        );

        if (distance <= attackDistance)
        {
            // agent.isStopped = true;
            // isAttacking[index] = true;

            // Debug.Log("La rama " + index + " esta lista para atacar");
            StartRootAttack(index);
        }
    }

    private void StartRootAttack(int index)
    {
        if (!IsValidIndex(index)) return;

        NavMeshAgent agent = rootAgents[index];
        Animator animator = rootAnimators[index];

        if (agent == null || animator == null) return;

        isAttacking[index] = true;

        agent.isStopped = true;
        agent.ResetPath();

        targetHeights[index] = attackHeight;

        animator.SetBool("isAttacking", true);

        /*Debug.Log("La rama " + index + " Ataca")*/;
    }

    // para pasar del final de la animación de ataque a la de bajar la rama
    public void BeginRootRetreat(int index)
    {
        if (!IsValidIndex(index) || !ramaActive || !isAttacking[index]) return;

        targetHeights[index] = 0f;

        rootAnimators[index].SetBool("isAttacking", false);
    }

    public void FinishAttack(int index)
    {
        if (!IsValidIndex(index) || !ramaActive || !isAttacking[index]) return;

        isAttacking[index] = false;
        
        NavMeshAgent agent = rootAgents[index];
        if(agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = false;
        }

        /*Debug.Log("La raiz " + index + " vuelve a seguir al jugador")*/;
    }

    public void SetRootActive(bool active)
    {
        ramaActive = active;
        
        for (int i = 0; i < rootCount; i++)
        {
            NavMeshAgent agent = rootAgents[i];

            isAttacking[i] = false;

            if (rootAnimators[i] != null) rootAnimators[i].SetBool("isAttacking", false);

            if (agent == null || !agent.isOnNavMesh) continue;

            if (!active)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }
            else agent.isStopped = false;
        }
    }

    public int RootCount => rootCount;
    public Vector3 RootPosition(int index) => rootAgents[index].transform.position;
    public float RootHeight(int index) => currentHeights[index];
    public bool RootAttacking(int index) => isAttacking[index];
    public void DisableClientAgents()
    {
        for (int i = 0; i < rootCount; i++)
            if (rootAgents[i] != null) rootAgents[i].enabled = false;
    }
    public void ApplyRemoteRoot(int index, Vector3 position, float height, bool attacking)
    {
        if (!IsValidIndex(index) || rootAgents[index] == null) return;
        rootAgents[index].transform.position = position;
        currentHeights[index] = targetHeights[index] = height;
        isAttacking[index] = attacking;
        if (rootAnimators[index] != null) rootAnimators[index].SetBool("isAttacking", attacking);
    }

    private bool IsValidIndex(int index)
    {
        return index >= 0 && index < rootCount;
    }
}
