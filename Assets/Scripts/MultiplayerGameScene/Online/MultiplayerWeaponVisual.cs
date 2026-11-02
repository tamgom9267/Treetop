using UnityEngine;

/// <summary>La copia online solo tiene renderers y estelas, nunca WeaponObject ni colliders.</summary>
public sealed class MultiplayerWeaponVisual : MonoBehaviour
{
    private TrailRenderer[] trails;
    private ParticleSystem[] particles;

    public void SetTrail(bool emitting)
    {
        if (trails == null) trails = GetComponentsInChildren<TrailRenderer>(true);
        foreach (TrailRenderer trail in trails)
        {
            trail.emitting = emitting;
            if (!emitting) trail.Clear();
        }
        if (particles == null) particles = GetComponentsInChildren<ParticleSystem>(true);
        foreach (ParticleSystem particle in particles)
        {
            if (emitting) particle.Play(false);
            else particle.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private void OnDisable() { SetTrail(false); }
}
