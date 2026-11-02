using UnityEngine;


[RequireComponent(typeof(EnemyCombat))]
public class SlimeEnemy : MonoBehaviour
{
    [Header("Estado")]
    [SerializeField] public bool isActive = false;

    private Animator animator;
    private EnemyCombat enemyCombat;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        enemyCombat = GetComponent<EnemyCombat>();
    }

    private void Start()
    {
        // el slime tiene que comenzar inactivo en el tutorial
        enemyCombat.SetCombatActive(isActive);
    }

    // Activa el slime cuando el jugador entra al Area 3.
    public void ActivateSlime()
    {
        // Evita activar dos veces al mismo slime.
        if (isActive)
            return;

        isActive = true;

        enemyCombat.SetCombatActive(isActive);

        // Sale de SlimeInactive y entra a SlimeIdle.
        if (animator != null) animator.SetTrigger("Activate");
    }

}
