using UnityEngine;

public class Campfire : MonoBehaviour
{
    // Guardamos la clase player para manejar vida
    [SerializeField] private PlayerClass playerClass;

    // Verificamos si el player esta dentro de el rango de la fogata
    private bool isPlayerInRange;

    // Contador para el tiempo de curacion
    private float healTimer;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        // El jugador entro en el rango de la fogata
        isPlayerInRange = true;

        // Reiniciamos el contador al entrar
        healTimer = 0f;
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        // El jugador salio del rango de la fogata
        isPlayerInRange = false;

        // Reiniciamos el contador al salir
        healTimer = 0f;
    }

    private void Update()
    {
        if (!isPlayerInRange)
            return;

        if (playerClass.health >= playerClass.maxHealth)
            return;

        healTimer += Time.deltaTime;

        if (healTimer < 0.5f)
            return;

        playerClass.health = Mathf.Min(playerClass.health + 1, playerClass.maxHealth);

        // Reiniciamos el contador para la siguiente curacion
        healTimer = 0f;
    }

}
