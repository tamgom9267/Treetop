using UnityEngine;
using System.Collections.Generic;
using Unity.VisualScripting;

public class TreeRootAttack : MonoBehaviour
{
    [SerializeField] private GameObject enemyRoot;

    [Header("Daño")]
    [SerializeField] private int damage = 5;

    // registro de jugadores golpeados
    // Hashset: no permite cosas duplicadas y es más rápido que una lista
    // new() = new HashSet<PlayerController>
    private readonly HashSet<PlayerController> playersHit = new();

    private bool attackActive;

    public void EnableAttack()
    {
        attackActive = true;
        playersHit.Clear();
    }

    public void DisableAttack()
    {
        attackActive = false;
        playersHit.Clear();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!attackActive) return;

        if (!other.CompareTag("Player")) return;

        PlayerController playerController = other.GetComponent<PlayerController>();

        if (MultiplayerWorldSession.Instance != null &&
            (playerController != MultiplayerStartingRoomSpawns.LocalPlayer ||
             MultiplayerDeathSession.Instance != null &&
             !MultiplayerDeathSession.Instance.IsAlive(MultiplayerGameplayProtocol.LocalActor))) return;

        PlayerClass playerClass = other.GetComponent<PlayerClass>();

        if (playerController == null || playerClass == null) return;

        // aqui es donde se usa el hashset para evitar golpear varias veces a 1 mismo jugador
        // durante la misma ventana donde esta atacando
        if (!playersHit.Add(playerController)) return;

        playerClass.TakeDamage(damage);

        // El daño letal puede desactivar al jugador dentro de TakeDamage.
        if (playerClass.health > 0) playerController.ReceiveHit(enemyRoot);
    }
}
