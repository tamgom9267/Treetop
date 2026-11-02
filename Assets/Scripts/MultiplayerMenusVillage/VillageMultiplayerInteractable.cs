using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>Usar un Collider Is Trigger. Lee la misma accion Interact que el personaje (E).</summary>
[DefaultExecutionOrder(-50)]
[RequireComponent(typeof(Collider))]
public class VillageMultiplayerInteractable : MonoBehaviour
{
    [SerializeField] private VillageMultiplayerMenu menu;
    [SerializeField] private GameObject interactionHint;
    [SerializeField] private TMP_Text interactionText;

    private readonly HashSet<Collider> playerColliders = new HashSet<Collider>();
    private PlayerController player;

    private void Awake()
    {
        SetHint(false);
        Collider trigger = GetComponent<Collider>();
        if (trigger == null || !trigger.isTrigger)
            Debug.LogWarning("Activa Is Trigger en el collider del objeto multijugador.", this);
    }

    private void Update()
    {
        playerColliders.RemoveWhere(collider => collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy);
        bool canInteract = player != null && playerColliders.Count > 0 && menu != null &&
            !menu.IsModalOpen && !player.IsInventoryOpen && !player.IsExternalUIBlocked &&
            !player.isAttacking && Time.timeScale > 0f;
        PhotonPartyService service = PhotonPartyService.Instance;
        canInteract &= service == null || !service.IsBusy;
        SetHint(canInteract);
        if (!canInteract) return;
        if (interactionText != null)
            interactionText.text = service != null && service.IsInParty && !service.IsHost
                ? "E: Abandonar sala" : "E: Multijugador";
        // Corre antes de PlayerController.Update: abrir el panel consume esta interaccion
        // mediante el bloqueo de controles, sin abrir tambien un cofre o inspeccionar un arma.
        if (player.interact != null && player.interact.action.WasPressedThisFrame())
            menu.OpenFromInteraction();
    }

    private void OnTriggerEnter(Collider other)
    {
        PlayerController candidate = other.GetComponentInParent<PlayerController>();
        if (candidate == null || !other.CompareTag("Player")) return;
        if (player != null && player != candidate && playerColliders.Count > 0) return;
        player = candidate;
        playerColliders.Add(other);
    }

    private void OnTriggerExit(Collider other)
    {
        playerColliders.Remove(other);
        if (playerColliders.Count == 0)
        {
            player = null;
            SetHint(false);
        }
    }

    private void OnDisable()
    {
        playerColliders.Clear();
        player = null;
        SetHint(false);
    }

    private void SetHint(bool visible)
    {
        if (interactionHint != null && interactionHint != gameObject)
            interactionHint.SetActive(visible);
        else if (interactionText != null)
            interactionText.enabled = visible;
    }
}
