using UnityEngine;

public class SpellProjectile : MonoBehaviour
{
    private int damage;
    private bool isInitialized;
    private bool isConsumed;

    public void Initialize(int damage)
    {
        this.damage = damage;
        isInitialized = true;
    }

    public bool TryConsume(out int impactDamage)
    {
        impactDamage = damage;
        if (isConsumed) return false;

        isConsumed = true;
        Destroy(gameObject);
        return isInitialized;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") || other.TryGetComponent<EnemyCombat>(out _)) return;

        TryConsume(out _);
    }
}
