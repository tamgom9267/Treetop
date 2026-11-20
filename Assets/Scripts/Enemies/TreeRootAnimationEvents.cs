using System;
using UnityEngine;
using UnityEngine.VFX;

public class TreeRootAnimationEvents : MonoBehaviour
{
    [SerializeField] private TreeRootController controller;
    [SerializeField] private int ramaIndex;
    public int RootIndex => ramaIndex;

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
        MultiplayerTreeRootSync sync = GetComponentInParent<MultiplayerTreeRootSync>();
        if (sync != null && !sync.Authority) return;
        if (attackCollider == null || rootAttack == null) return;

        rootAttack.EnableAttack();
        PlayEffect();
        attackCollider.enabled = true;
        if (sync != null) sync.NotifyWindow(ramaIndex, true);
    }

    public void DisableRootAttackCollider()
    {
        MultiplayerTreeRootSync sync = GetComponentInParent<MultiplayerTreeRootSync>();
        if (sync != null && !sync.Authority) return;
        if (attackCollider == null || rootAttack == null) return;

        rootAttack.DisableAttack();
        attackCollider.enabled = false;
        if (sync != null) sync.NotifyWindow(ramaIndex, false);
    }

    public void ApplyRemoteWindow(bool active)
    {
        if (attackCollider == null || rootAttack == null) return;
        if (attackCollider.enabled == active) return;
        if (active) rootAttack.EnableAttack(); else rootAttack.DisableAttack();
        attackCollider.enabled = active;
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
