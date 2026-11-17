using System.Collections;
using UnityEngine;

public abstract class WeaponEffect : ScriptableObject
{
    public virtual IEnumerator OnHit(WeaponEffectController target) => null;

    public virtual IEnumerator OnEquipped(PlayerClass wearer) => null;

    // Returns true if the value is positive and finite (not infinity or NaN).
    protected static bool IsPositiveFinite(float value) => value > 0f && !float.IsInfinity(value);
}
