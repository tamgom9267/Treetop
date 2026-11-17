using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class WeaponEffectController : MonoBehaviour
{
    private sealed class RunningEffect
    {
        public Coroutine coroutine;
    }

    private readonly Dictionary<Type, RunningEffect> activeEffects = new Dictionary<Type, RunningEffect>();
    private EnemyCombat localEnemy;
    private MultiplayerEnemy multiplayerEnemy;
    private PlayerClass wearer;
    private IList<WeaponEffect> equippedEffects;

    public bool CanAffectEnemy => localEnemy != null
        ? localEnemy.CanReceiveWeaponEffects
        : multiplayerEnemy != null && multiplayerEnemy.CanReceiveWeaponEffects;

    public static void StartEquippedEffects(WeaponObject weapon, PlayerClass wearer)
    {
        IList<WeaponEffect> effects = weapon.weaponData.additionalEffects;
        if (wearer == null || effects == null || effects.Count == 0) return;

        // La instancia equipada se desactiva al soltar o cambiar de arma.
        WeaponEffectController controller = weapon.GetComponent<WeaponEffectController>();
        if (controller == null) controller = weapon.gameObject.AddComponent<WeaponEffectController>();

        controller.wearer = wearer;
        controller.equippedEffects = effects;
        if (controller.isActiveAndEnabled) controller.StartPassives();
    }

    public void Initialize(EnemyCombat enemy)
    {
        localEnemy = enemy;
        multiplayerEnemy = null;
    }

    public void Initialize(MultiplayerEnemy enemy)
    {
        multiplayerEnemy = enemy;
        localEnemy = null;
    }

    public void Apply(IList<WeaponEffect> effects)
    {
        if (!isActiveAndEnabled || effects == null || !CanAffectEnemy) return;

        foreach (WeaponEffect effect in effects)
        {
            if (effect != null) StartEffect(effect.GetType(), effect.OnHit(this));
        }
    }

    public bool TryDamageEnemy(int damage)
    {
        if (!CanAffectEnemy || damage <= 0) return false;

        if (localEnemy == null) return multiplayerEnemy.ApplyWeaponEffectDamage(damage);

        localEnemy.TakeDamage(damage);
        return localEnemy.CanReceiveWeaponEffects;
    }

    private void StartPassives()
    {
        if (wearer == null || equippedEffects == null) return;

        foreach (WeaponEffect effect in equippedEffects)
        {
            if (effect != null) StartEffect(effect.GetType(), effect.OnEquipped(wearer));
        }
    }

    private void StartEffect(Type effectType, IEnumerator routine)
    {
        if (routine == null) return;

        // Volver a aplicar el mismo efecto reinicia su duracion en este objetivo.
        if (activeEffects.TryGetValue(effectType, out RunningEffect previous) && previous.coroutine != null)
            StopCoroutine(previous.coroutine);

        var running = new RunningEffect();
        activeEffects[effectType] = running;
        running.coroutine = StartCoroutine(RunEffect(effectType, running, routine));
    }

    private IEnumerator RunEffect(Type effectType, RunningEffect running, IEnumerator routine)
    {
        yield return routine;

        if (activeEffects.TryGetValue(effectType, out RunningEffect current) && current == running)
            activeEffects.Remove(effectType);
    }

    public void Clear()
    {
        StopAllCoroutines();
        activeEffects.Clear();
    }

    private void OnEnable() => StartPassives();

    private void OnDisable() => Clear();
}
