using UnityEngine;

public class SpellProjectile : MonoBehaviour
{
    // efecto de colisión
    [SerializeField] private GameObject impactEffect;
    [SerializeField] public AudioClip FireBallImpactSound;

    private int damage;
    private bool isInitialized;
    private bool isConsumed;
    private int multiplayerCast;
    private bool visualOnly;

    public void InitializeMultiplayerCast(int cast) { multiplayerCast = cast; }
    public void InitializeRemoteVisual() { visualOnly = true; }

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
        if (FireBallImpactSound != null) AudioSource.PlayClipAtPoint(FireBallImpactSound, transform.position, 1);

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

        MultiplayerEnemy enemy = other.GetComponentInParent<MultiplayerEnemy>();
        if (enemy != null)
        {
            if (!visualOnly && multiplayerCast > 0 && !isConsumed)
                MultiplayerWorldSession.Instance?.RequestSpellHit(multiplayerCast, enemy, transform.position);
            TryConsume(out _);
            return;
        }

        TryConsume(out _);
    }
}
