using Photon.Realtime;
using Unity.Multiplayer.Center.Common.Analytics;
using UnityEngine;
using UnityEngine.InputSystem;

public class ManaFountain : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private GameObject interactionPrompt;

    [Header("Estado")]
    [SerializeField] private bool hasBeenUsed = false;

    private PlayerController playerCerca;

    private void Start()
    {
        if (interactionPrompt != null) interactionPrompt.SetActive(false);
    }

    public void Interact()
    {
        if (hasBeenUsed || playerCerca == null) return;

        PlayerClass playerStats = playerCerca.GetComponent<PlayerClass>();

        if (playerStats == null)
        {
            Debug.LogWarning("El jugador no tiene PlayerClass", playerCerca);
            return;
        }

        // no se puede usar fuente si tiene mana maximo
        if(playerStats.MP >= playerStats.maxMP) return;

        //restaurar mana
        playerStats.RestoreMP(playerStats.maxMP);

        hasBeenUsed = true;

        if (interactionPrompt != null) interactionPrompt.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasBeenUsed || !other.CompareTag("Player")) return;

        PlayerController player = other.GetComponent<PlayerController>();

        if (player == null) player = other.GetComponentInParent<PlayerController>();

        if (player == null) return;

        playerCerca = player;
        player.nearManaFountain = this;

        if (interactionPrompt != null) interactionPrompt.SetActive(true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        PlayerController player = other.GetComponent<PlayerController>();
        
        if (player == null) player = other.GetComponentInParent<PlayerController>();

        if (player == null || player != playerCerca) return;

        playerCerca = null;

        if (player.nearManaFountain == this) player.nearManaFountain = null;

        if (interactionPrompt != null) interactionPrompt.SetActive(false);
    }
}
