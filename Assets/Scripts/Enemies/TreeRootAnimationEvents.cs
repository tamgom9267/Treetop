using System;
using UnityEngine;
using UnityEngine.VFX;

public class TreeRootAnimationEvents : MonoBehaviour
{
    [SerializeField] private TreeRootController controller;
    [SerializeField] private int ramaIndex;

    [Header("Ataque")]
    [SerializeField] private BoxCollider attackCollider;
    [SerializeField] private TreeRootAttack rootAttack;

    [Header("Efectos visuales")]
    [SerializeField] private ParticleSystem emergenceEffect;

    private void Awake()
    {
        if (attackCollider != null) attackCollider.enabled = false;
    }

    public void EnableRootAttackCollider()
    {
        if (attackCollider == null || rootAttack == null) return;

        rootAttack.EnableAttack();
        PlayEffect();
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

    private void PlayEffect()
    {
        if (emergenceEffect == null) return;

        GameObject effectObj = emergenceEffect.gameObject;

        effectObj.SetActive(true);
    }
}
