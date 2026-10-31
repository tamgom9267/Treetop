using UnityEngine;

/// <summary>Asignar todos los colliders que activan el portal. No incluir el trigger multijugador.</summary>
public class VillagePartyAccessController : MonoBehaviour
{
    [SerializeField] private Collider[] portalColliders = new Collider[0];
    private bool[] initialStates;
    private PhotonPartyService service;

    private void Awake()
    {
        initialStates = new bool[portalColliders.Length];
        for (int index = 0; index < portalColliders.Length; index++)
            initialStates[index] = portalColliders[index] != null && portalColliders[index].enabled;
    }

    private void OnEnable()
    {
        AttachService();
        ApplyAccess();
    }

    private void Start()
    {
        AttachService();
        ApplyAccess();
    }

    private void OnDisable()
    {
        if (service != null) service.PartyChanged -= ApplyAccess;
        service = null;
        SetCollidersBlocked(false);
    }

    private void AttachService()
    {
        if (service != null || PhotonPartyService.Instance == null) return;
        service = PhotonPartyService.Instance;
        service.PartyChanged += ApplyAccess;
    }

    private void ApplyAccess()
    {
        // Una entrada que ya iba en camino puede recibir el bloqueo antes de su rol.
        SetCollidersBlocked(service != null && service.IsInParty && service.IsPartyLocked && !service.IsHost);
    }

    private void SetCollidersBlocked(bool blocked)
    {
        if (initialStates == null) return;
        for (int index = 0; index < portalColliders.Length; index++)
            if (portalColliders[index] != null)
                portalColliders[index].enabled = !blocked && initialStates[index];
    }
}
