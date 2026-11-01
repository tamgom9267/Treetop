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

    [Header("Feedback Visual")]
    // [SerializeField] private Renderer playerRenderer;
    // [SerializeField] public Color flashColor = Color.red;
    // [SerializeField] private float flashDuration = 0.1f;

    // private Material[] playerMaterials;
    // private Color[] originalColors;
    // private Coroutine flashCoroutine;

    [SerializeField] private Renderer playerRenderer;
    
    [Header("Camera Feedback")]
    [SerializeField] private FollowPlayerNoRotation cameraFollow;
    
    // Use mejor el MaterialPropertyBlock y no el otro metodo para no hacer tantas copias de materiales
    [Header("Feedback de daño")]
    [SerializeField, ColorUsage(true, true)]
    private Color damageEmissionColor = Color.white;
    [SerializeField] private float damageFlashDuration = 0.1f;

    [Header("Feedback de curación")]
    [SerializeField, ColorUsage(true, true)]
    private Color healingEmissionColor = new Color(0.7f, 1f, 0.2f, 1f);
    [SerializeField] private float healingFlashDuration = 0.6f;

    private MaterialPropertyBlock[] originalPropertyBlocks;
    private MaterialPropertyBlock emissionPropertyBlock;

    private bool[] supportsEmission;
    private Coroutine emissionCoroutine;

    private float emissionEndTime;
    private int currentFeedbackPriority;
    
    private Color currentEmissionColor;

    private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");



    private PlayerController playerController;


    void Awake()
    {
       assignClass();

       playerController = GetComponent<PlayerController>();

       InitializeEmissionFeedback();
    }

    // void Start()
    // {
    //     playerMaterials = playerRenderer.materials;
    //     originalColors = new Color[playerMaterials.Length];

    //     for (int i = 0; i < playerMaterials.Length; i++)
    //     {
    //         originalColors[i] = playerMaterials[i].color;
    //     }
    // }



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
        // TriggerHitFeedback();
        TriggerEmissionFeedback(damageEmissionColor, damageFlashDuration, 2);

        //Screen Shake
        if (cameraFollow != null) cameraFollow.Shake();

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
        if (cantidad <= 0) return;

        int previousHealth = health;

        // Math.f.Min para que no puedas tener más de la vida máxima.
        health = Mathf.Min(maxHealth, health + cantidad);

        // solo cambiar color si se recupera vida en verdad
        // asi no sale si ya pasaste de 100 de vida
        if (health > previousHealth) TriggerEmissionFeedback(healingEmissionColor, healingFlashDuration, 1);

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
    
    // public void TriggerHitFeedback()
    // {
    //     if (flashCoroutine != null)
    //         StopCoroutine(flashCoroutine);

    //     flashCoroutine = StartCoroutine(DoFlash());
    // }

    // private IEnumerator DoFlash()
    // {
    //     // Aplica el color del flash a todos los materiales
    //     for (int i = 0; i < playerMaterials.Length; i++)
    //     {
    //         playerMaterials[i].color = flashColor;
    //     }

    //     yield return new WaitForSeconds(flashDuration);

    //     // Restaura los colores originales
    //     for (int i = 0; i < playerMaterials.Length; i++)
    //     {
    //         playerMaterials[i].color = originalColors[i];
    //     }

    //     flashCoroutine = null;
    // }

        // lo del feedback nuevo :D
    private void InitializeEmissionFeedback()
    {
        if (playerRenderer == null) return;

        Material[] materials = playerRenderer.sharedMaterials;

        originalPropertyBlocks = new MaterialPropertyBlock[materials.Length];
        supportsEmission = new bool[materials.Length];

        emissionPropertyBlock = new MaterialPropertyBlock();

        for (int i = 0; i < materials.Length; i++)
        {
            originalPropertyBlocks[i] = new MaterialPropertyBlock();

            playerRenderer.GetPropertyBlock(originalPropertyBlocks[i], i);

            supportsEmission[i] =
                materials[i] != null &&
                materials[i].HasProperty(EmissionColorID);
        }
    }

    private void TriggerEmissionFeedback(Color color, float duration, int priority)
    {
        if (playerRenderer == null || duration <= 0f) return;

        // que los efectos con menor prioridad no afecten a los demas
        if (emissionCoroutine != null && priority < currentFeedbackPriority) return;

        // si llega un efecto de mayor prioridad que cambie
        if (priority > currentFeedbackPriority) emissionEndTime = Time.unscaledTime + duration;
        else
        {
            // efectos de igual prioridad
            emissionEndTime = Mathf.Max(emissionEndTime, Time.unscaledTime + duration);
        }

        currentFeedbackPriority = priority;
        currentEmissionColor = color;

        ApplyEmissionColor(color);

        if (emissionCoroutine == null)
        {
            emissionCoroutine = StartCoroutine(EmissionFeedbackRoutine());
        }
    }

    private void ApplyEmissionColor(Color color)
    {
        if (playerRenderer == null || supportsEmission == null) return;

        for (int i = 0; i < supportsEmission.Length; i++)
        {
            if (!supportsEmission[i]) continue;

            playerRenderer.GetPropertyBlock(emissionPropertyBlock, i);
            emissionPropertyBlock.SetColor(EmissionColorID, color);
            playerRenderer.SetPropertyBlock(emissionPropertyBlock, i);
        }
    }

    private IEnumerator EmissionFeedbackRoutine()
    {
        while (Time.unscaledTime < emissionEndTime)
        {
            yield return null;
        }

        // restaurar al brillo normal
        for (int i = 0; i < originalPropertyBlocks.Length; i++)
        {
            playerRenderer.SetPropertyBlock(originalPropertyBlocks[i], i);
        }

        currentFeedbackPriority = 0;
        emissionCoroutine = null;
    }
}
