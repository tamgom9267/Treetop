using UnityEngine;

[RequireComponent(typeof(EnemyCombat))]
public class SlimeBoss : MonoBehaviour
{
    [Header("Estado")]
    public bool isActive = false;

    private Animator animator;
    private EnemyCombat enemyCombat;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        enemyCombat = GetComponent<EnemyCombat>();
    }

    private void Start()
    {
        // boss del tutorial comienza inactivo
        enemyCombat.SetCombatActive(isActive);
    }

    public void ActivateSlime()
    {
        // no activar 2 veces
        if (isActive) return;

        isActive = true;

        // permite que el boss le puedas actacar y te haga daño
        enemyCombat.SetCombatActive(isActive);

        // sale de SlimeInactive y entra a SlimeIdle
        if (animator != null) animator.SetTrigger("Activate");
    }
}
