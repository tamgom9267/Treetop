using System.Collections.Generic;
using UnityEngine;

public sealed class WeaponEffectController : MonoBehaviour
{
    private sealed class ActiveEffect
    {
        public int damagePerTick;
        public int ticksRemaining;
        public float tickInterval;
        public float timeUntilNextTick;
    }

    private readonly Dictionary<WeaponEffectType, ActiveEffect> activeEffects =
        new Dictionary<WeaponEffectType, ActiveEffect>();
    private readonly List<WeaponEffectType> completedEffects = new List<WeaponEffectType>();
    private EnemyCombat localEnemy;
    private MultiplayerEnemy multiplayerEnemy;

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

    public void Apply(IList<WeaponEffectData> effects)
    {
        if (effects == null || !CanTick()) return;

        foreach (WeaponEffectData effect in effects)
        {
            if (effect == null || !System.Enum.IsDefined(typeof(WeaponEffectType), effect.type) ||
                effect.damagePerTick <= 0 || effect.damagePerTick > 10000 ||
                !IsFinite(effect.duration) || !IsFinite(effect.tickInterval) ||
                effect.duration <= 0f || effect.tickInterval <= 0f)
            {
                Debug.LogWarning("Se ignoro una configuracion invalida de efecto de arma.", this);
                continue;
            }

            int ticks = Mathf.FloorToInt(effect.duration / effect.tickInterval + 0.0001f);
            if (ticks <= 0 || ticks > 10000)
            {
                Debug.LogWarning("Se ignoro un efecto de arma con una cantidad de pulsos fuera de rango.", this);
                continue;
            }

            activeEffects[effect.type] = new ActiveEffect
            {
                damagePerTick = effect.damagePerTick,
                ticksRemaining = ticks,
                tickInterval = effect.tickInterval,
                timeUntilNextTick = effect.tickInterval
            };
        }
    }

    private void Update()
    {
        if (activeEffects.Count == 0 || Time.deltaTime <= 0f) return;

        completedEffects.Clear();
        foreach (KeyValuePair<WeaponEffectType, ActiveEffect> pair in activeEffects)
        {
            ActiveEffect effect = pair.Value;
            if (!CanTick()) continue;

            effect.timeUntilNextTick -= Time.deltaTime;
            while (effect.timeUntilNextTick <= 0f && effect.ticksRemaining > 0)
            {
                effect.timeUntilNextTick += effect.tickInterval;
                effect.ticksRemaining--;
                if (!ApplyDamage(effect.damagePerTick))
                {
                    completedEffects.Add(pair.Key);
                    break;
                }
            }

            if (effect.ticksRemaining == 0)
                completedEffects.Add(pair.Key);
        }

        foreach (WeaponEffectType type in completedEffects)
            activeEffects.Remove(type);
    }

    private bool ApplyDamage(int damage)
    {
        if (localEnemy != null && localEnemy.CanReceiveWeaponEffects)
        {
            localEnemy.TakeDamage(damage);
            return localEnemy.CanReceiveWeaponEffects;
        }

        return multiplayerEnemy != null && multiplayerEnemy.ApplyWeaponEffectDamage(damage);
    }

    private bool CanTick()
    {
        if (localEnemy != null) return localEnemy.CanReceiveWeaponEffects;
        return multiplayerEnemy != null && multiplayerEnemy.CanReceiveWeaponEffects;
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    public void Clear()
    {
        activeEffects.Clear();
        completedEffects.Clear();
    }

    private void OnDisable()
    {
        Clear();
    }
}
