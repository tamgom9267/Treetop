using System;
using UnityEngine;

public class TreeRootAnimationEvents : MonoBehaviour
{
    [SerializeField] private TreeRootController controller;
    [SerializeField] private int ramaIndex;

    [Header("Ataque")]
    [SerializeField] private BoxCollider attackCollider;
    [SerializeField] private TreeRootAttack rootAttack;

    private void Awake()
    {
        if (attackCollider != null) attackCollider.enabled = false;
    }

    public void EnableRootAttackCollider()
    {
        if (attackCollider == null || rootAttack == null) return;

        rootAttack.EnableAttack();
        attackCollider.enabled = true;
    }

    public void DisableRootAttackCollider()
    {
        if (attackCollider == null || rootAttack == null) return;

        rootAttack.DisableAttack();
        attackCollider.enabled = false;
    }

    public void BeginRootRetreat()
    {
        if (controller != null) controller.BeginRootRetreat(ramaIndex);
    }

    public void FinishRootAttack()
    {
        if (controller != null) controller.FinishAttack(ramaIndex);
        
    }
}
