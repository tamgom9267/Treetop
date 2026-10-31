using System;
using UnityEngine;
using System.Collections;


public class PlayerClass : MonoBehaviour
{

    public  ClassStats classStats;

    public int maxHealth;
    public int health;

    public int maxMP;
    public int MP;


    public int defense;

    //Dano fisico(Weapon Damage)
    public int WD;

    //Dano de proyectil(Projectile Damage)
    public int PD;

    //Poder de curacion(Healing Bonus)
    public int HB;
    public int speed;

    // Fuerza fija de knockback que todos los enemigos aplican al jugador.
    public float enemyKnockbackForce = 8f;

    // Fuerza vertical fija de knockback que todos los enemigos aplican al jugador.
    public float enemyKnockbackUpForce = 3f;


    // Patron observer - cuando cambia la vida notifica al HUD
    public event Action<int, int, int, int> OnStatsChanged;

    [Header("Hit Feedback")]
    [SerializeField] private Renderer playerRenderer;
    [SerializeField] public Color flashColor = Color.red;
    [SerializeField] private float flashDuration = 0.1f;

    private Material[] playerMaterials;
    private Color[] originalColors;
    private Coroutine flashCoroutine;


    private PlayerController playerController;


    void Awake()
    {
       assignClass();

       playerController = GetComponent<PlayerController>();
    }

    void Start()
    {
        playerMaterials = playerRenderer.materials;
        originalColors = new Color[playerMaterials.Length];

        for (int i = 0; i < playerMaterials.Length; i++)
        {
            originalColors[i] = playerMaterials[i].color;
        }
    }


    public void assignClass()
    {
         if (classStats)
        {
            maxHealth = classStats.health;
            health = maxHealth;

            maxMP = classStats.MP;
            MP= maxMP;

            defense = classStats.defense;
            WD = classStats.WD;
            PD = classStats.PD;
            speed = classStats.speed;
        }
        else
        {
            // Si por alguna razon el jugador no tiene clase
            health = 1;
            MP = 1;
            defense = 1;
            WD = 1;
            PD = 1;
            speed = 1;
        }

        // para observer
        NotifyStatsChanged();
    }

    public void TakeDamage(int damage)
    {
        // Evita recibir dano si el dano es menor o igual a 0
        if (damage <= 0 || health <= 0) { return; }
        
        // health -= damage;
        

        // la vida no puede ser menor a 0
        // if (health < 0)
        //    health = 0;


        // Cambio a Mathf para que la vida no pueda ser menor a 0 y evitar errores en el HUD
        health = Mathf.Max(0, health - damage);

        Debug.Log("Player took " + damage + " damage. Current health: " + health);

        // feedback visual
        TriggerHitFeedback();

        //hit stop
        if (playerController != null)
        {
            playerController.TriggerReceivedHitStop();
        }

        // para observer
        NotifyStatsChanged();
    }

    public void Heal(int cantidad)
    {
        if (cantidad <= 0) { return; }

        // Math.f.Min para que no puedas tener más de la vida máxima.
        health = Mathf.Min(maxHealth, health + cantidad);

        // para observer
        NotifyStatsChanged();
    }

    public void SpendMP(int cantidad)
    {
        if (cantidad <= 0) { return; }

        MP = Mathf.Max(0, MP - cantidad);

        // para observer
        NotifyStatsChanged();
    }

    public void RestoreMP(int cantidad)
    {
        if (cantidad <= 0) { return; }

        MP = Mathf.Min(maxMP, MP + cantidad);

        NotifyStatsChanged();
    }

    private void NotifyStatsChanged()
    {
        if (OnStatsChanged != null)
        {
            OnStatsChanged.Invoke(health, maxHealth, MP, maxMP);
        }
    }
    
    public void TriggerHitFeedback()
    {
        if (flashCoroutine != null)
            StopCoroutine(flashCoroutine);

        flashCoroutine = StartCoroutine(DoFlash());
    }

    private IEnumerator DoFlash()
    {
        // Aplica el color del flash a todos los materiales
        for (int i = 0; i < playerMaterials.Length; i++)
        {
            playerMaterials[i].color = flashColor;
        }

        yield return new WaitForSeconds(flashDuration);

        // Restaura los colores originales
        for (int i = 0; i < playerMaterials.Length; i++)
        {
            playerMaterials[i].color = originalColors[i];
        }

        flashCoroutine = null;
    }
}
