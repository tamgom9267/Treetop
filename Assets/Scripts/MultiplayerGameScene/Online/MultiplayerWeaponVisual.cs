using UnityEngine;

/// <summary>La copia online solo tiene renderers y estelas, nunca WeaponObject ni colliders.</summary>
public sealed class MultiplayerWeaponVisual : MonoBehaviour
{
    private TrailRenderer[] trails;

    public void SetTrail(bool emitting)
    {
        if (trails == null) trails = GetComponentsInChildren<TrailRenderer>(true);
        foreach (TrailRenderer trail in trails)
        {
            trail.emitting = emitting;
            if (!emitting) trail.Clear();
        }
    }

    private void OnDisable() { SetTrail(false); }
}
