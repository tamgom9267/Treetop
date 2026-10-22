using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.UIElements;
using UnityEngine.Rendering.UI;
using UnityEditor.ShaderKeywordFilter;

public class HealthBar : MonoBehaviour
{
    [SerializeField] private PlayerClass playerClass;

    [Header("Barras de vida y mana")]
    [SerializeField] private bool enablePotionBars = true;
    [SerializeField] private RawImage hpPotion;
    [SerializeField] private RawImage mpPotion;

    [SerializeField] private TextMeshProUGUI HPText;
    [SerializeField] private TextMeshProUGUI MPText;

    private Material hpPotionMaterial;
    private Material mpPotionMaterial;

    private static readonly int FillID = Shader.PropertyToID("_Fill");

    private void Start()
    {
        if (!enablePotionBars) { return; }

        if (!InitializePotionBars()) { return; }

        if (playerClass == null)
        {
            Debug.LogWarning("Asigna PlayerClass en el UI");
            return;
        }

        // suscribirse al evento
        playerClass.OnStatsChanged += UpdatePotionBars;

        // Actualizar UI por primera vez
        UpdatePotionBars(
            playerClass.health,
            playerClass.maxHealth,
            playerClass.MP,
            playerClass.maxMP
        );
    }

    private bool InitializePotionBars()
    {
        if (hpPotion == null || mpPotion == null)
        {
            Debug.LogWarning("Asigna las RawImage del UI");
            return false;
        }

        if (hpPotion.material == null || mpPotion.material == null)
        {
            Debug.LogWarning("Asiugna los materiales de las RawImage del UI");
            return false;
        }

        //instancias independientes de los materiales
        // asi no se modifica nada del material original
        hpPotionMaterial = new Material(hpPotion.material);
        mpPotionMaterial = new Material(mpPotion.material);

        hpPotion.material = hpPotionMaterial;
        mpPotion.material = mpPotionMaterial;

        return true;
    }

    private void UpdatePotionBars(
        int currentHP, int maxHP, int currentMP, int maxMP)
    {
        float hpFill = CalculateFill(currentHP, maxHP);
        float mpFill = CalculateFill(currentMP, maxMP);

        hpPotionMaterial.SetFloat(FillID, hpFill);
        mpPotionMaterial.SetFloat(FillID, mpFill);

        if (HPText != null)
        {
            HPText.text = currentHP.ToString();
        }

        if (MPText != null)
        {
            MPText.text = currentMP.ToString();
        }
    }

    private float CalculateFill(float current, float max)
    {
        if (max <= 0f){ return 0f; }

        // solo regresa un valor entre 0 y 1
        return Mathf.Clamp01(current/max);
    }

    private void OnDestroy()
    {
        // eliminar las suscripciones para evitar referencias inecesarias
        if (playerClass != null)
        {
            playerClass.OnStatsChanged -= UpdatePotionBars;
        }

        if (hpPotionMaterial != null)
        {
            Destroy(hpPotionMaterial);
        }

        if (mpPotionMaterial != null)
        {
            Destroy(mpPotionMaterial);
        }
    }
}
