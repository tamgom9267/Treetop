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
    private PlayerClass manaRecipient;
    private int manaPerTick;
    private float manaTickInterval;
    private float timeUntilManaTick;

    public static void StartEquippedEffects(WeaponObject weapon, PlayerClass wearer)
    {
        if (wearer == null || weapon.weaponData.additionalEffects == null) return;

        foreach (WeaponEffectData effect in weapon.weaponData.additionalEffects)
        {
            if (effect == null || effect.type != WeaponEffectType.ManaRegeneration) continue;

            if (effect.manaPerTick <= 0 || !IsFinite(effect.tickInterval) || effect.tickInterval <= 0f)
            {
                Debug.LogWarning("Se ignoro una configuracion invalida de regeneracion de mana.", weapon);
                continue;
            }

            // La instancia equipada se desactiva al soltar o cambiar de arma.
            WeaponEffectController controller = weapon.GetComponent<WeaponEffectController>();
            if (controller == null) controller = weapon.gameObject.AddComponent<WeaponEffectController>();

            controller.manaRecipient = wearer;
            controller.manaPerTick = effect.manaPerTick;
            controller.manaTickInterval = effect.tickInterval;
            controller.timeUntilManaTick = effect.tickInterval;
            return;
        }
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

    public void Apply(IList<WeaponEffectData> effects)
    {
        if (effects == null || !CanTick()) return;

        foreach (WeaponEffectData effect in effects)
        {
            if (effect?.type == WeaponEffectType.ManaRegeneration) continue;

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
        float deltaTime = Time.deltaTime;
        if (deltaTime <= 0f) return;

        UpdateManaRegeneration(deltaTime);
        if (activeEffects.Count == 0) return;

        completedEffects.Clear();
        foreach (KeyValuePair<WeaponEffectType, ActiveEffect> pair in activeEffects)
        {
            ActiveEffect effect = pair.Value;
            if (!CanTick()) continue;

            effect.timeUntilNextTick -= deltaTime;
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

    private void UpdateManaRegeneration(float deltaTime)
    {
        if (manaRecipient == null) return;

        if (!manaRecipient.isActiveAndEnabled || manaRecipient.health <= 0 || manaRecipient.MP >= manaRecipient.maxMP)
        {
            timeUntilManaTick = manaTickInterval;
            return;
        }

        timeUntilManaTick -= deltaTime;
        if (timeUntilManaTick > 0f) return;

        timeUntilManaTick += manaTickInterval;
        int missingMana = manaRecipient.maxMP - manaRecipient.MP;
        manaRecipient.RestoreMP(Mathf.Min(missingMana, manaPerTick), showFeedback: false);
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
        timeUntilManaTick = manaTickInterval;
    }
}
