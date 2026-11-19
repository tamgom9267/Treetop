using UnityEngine;

public class SpellProjectile : MonoBehaviour
{
    // efecto de colisión
    [SerializeField] private GameObject impactEffect;
    [SerializeField] public AudioClip FireBallImpactSound;

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
        AudioSource.PlayClipAtPoint(FireBallImpactSound, transform.position, 1);
        impactDamage = damage;
        if (isConsumed) return false;

        isConsumed = true;

        // efectos de colisión
        if (impactEffect != null){
            Instantiate(
                impactEffect, transform.position, Quaternion.identity
            );
        }

        
        Destroy(gameObject);
        return isInitialized;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") || other.TryGetComponent<EnemyCombat>(out _)) return;

        TryConsume(out _);
    }
}
