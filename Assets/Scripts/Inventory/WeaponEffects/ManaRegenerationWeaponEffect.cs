using System.Collections;
using UnityEngine;

[CreateAssetMenu(fileName = "ManaRegeneration", menuName = "Scriptable Objects/Weapon Effects/Mana Regeneration")]
public sealed class ManaRegenerationWeaponEffect : WeaponEffect
{
    [SerializeField, Min(1)] private int manaPerTick = 1;
    [SerializeField, Min(0.01f)] private float tickInterval = 2f;

    public override IEnumerator OnEquipped(PlayerClass wearer)
    {
        if (manaPerTick > 0 && IsPositiveFinite(tickInterval))
            return Regenerate(wearer, manaPerTick, tickInterval);

        /*Debug.LogWarning("Se ignoro una configuracion invalida de regeneracion de mana.", this)*/;
        return null;
    }

    private static IEnumerator Regenerate(PlayerClass wearer, int mana, float interval)
    {
        float remainingTime = interval;
        while (wearer != null)
        {
            yield return null;
            if (wearer == null) yield break;
            if (!wearer.isActiveAndEnabled || wearer.health <= 0 || wearer.MP >= wearer.maxMP)
            {
                remainingTime = interval;
                continue;
            }

            if (Time.deltaTime <= 0f) continue;
            remainingTime -= Time.deltaTime;
            if (remainingTime > 0f) continue;

            remainingTime += interval;
            int missingMana = wearer.maxMP - wearer.MP;
            wearer.RestoreMP(Mathf.Min(missingMana, mana), showFeedback: false);
        }
    }
}
