using System.Collections;
using UnityEngine;

[CreateAssetMenu(fileName = "Bleed", menuName = "Scriptable Objects/Weapon Effects/Bleed")]
public sealed class BleedWeaponEffect : WeaponEffect
{
    [SerializeField, Min(1)] private int damagePerTick = 1;
    [SerializeField, Min(0.01f)] private float duration = 3f;
    [SerializeField, Min(0.01f)] private float tickInterval = 1f;

    public override IEnumerator OnHit(WeaponEffectController target)
    {
        if (damagePerTick > 0 && damagePerTick <= 10000 && IsPositiveFinite(duration) && IsPositiveFinite(tickInterval))
        {
            float ticks = Mathf.Floor(duration / tickInterval + 0.0001f);
            if (ticks >= 1f && ticks <= 10000f)
                return ApplyBleed(target, damagePerTick, (int)ticks, tickInterval);
        }

        Debug.LogWarning("Se ignoro una configuracion invalida de sangrado.", this);
        return null;
    }

    private static IEnumerator ApplyBleed(WeaponEffectController target, int damage, int ticksRemaining, float interval)
    {
        float remainingTime = interval;
        while (ticksRemaining > 0)
        {
            yield return null;
            if (!target.CanAffectEnemy) continue;

            remainingTime -= Time.deltaTime;
            while (remainingTime <= 0f && ticksRemaining > 0)
            {
                remainingTime += interval;
                ticksRemaining--;
                if (!target.TryDamageEnemy(damage)) yield break;
            }
        }
    }
}
