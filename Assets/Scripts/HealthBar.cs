using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HealthBar : MonoBehaviour
{
    [SerializeField]
    public PlayerClass playerClass;

    // parte del slider, se va a quitar //

    public Slider HP;
    public Slider MP;

    public Text NumbHP;
    public Text NumbMP;

    
    // NUEVO - para la barra circular //
    [Header("Potion Health Bars")]
    [SerializeField] private bool enablePotionBars = true;
    [SerializeField] private RawImage hpPotion;
    [SerializeField] private RawImage mpPotion;

    [SerializeField] private TextMeshProUGUI HPText;
    [SerializeField] private TextMeshProUGUI MPText;

    private Material hpPotionMaterial;
    private Material mpPotionMaterial;

    private static readonly int FillID = Shader.PropertyToID("_Fill"); 

    // // // 


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // sistema de barra anterior //
        Transform hpObject = transform.Find("HP");
        HP = hpObject.GetComponentInChildren<Slider>();
        HP.maxValue = playerClass.health;

        Transform mpObject = transform.Find("MP");
        MP = mpObject.GetComponentInChildren<Slider>();
        MP.maxValue = playerClass.MP;

        // nuevo sistema
        if (enablePotionBars) { InitializePotionBars(); }
    }

    // Update is called once per frame
    void Update()
    {
        // sistema anterior
        HP.value = playerClass.health;
        MP.value = playerClass.MP;

        NumbHP.text = playerClass.health.ToString();
        NumbMP.text = playerClass.MP.ToString();

        if (enablePotionBars) { UpdatePotionBars(); }
    }

    // funciones nuevas al final integrar a start y update :D
    private void InitializePotionBars()
    {
        if (hpPotion == null || mpPotion == null)
        {
            Debug.LogWarning("Asigna las RawImages para activar el sistema");
            enablePotionBars = false;
            return;
        }

        if (hpPotion.material == null || mpPotion.material == null)
        {
            Debug.LogWarning("Las RawImages elegidas no tienen el material correcto");

            enablePotionBars = false;
            return;
        }

        // materiales nuevos para no modificar los base
        hpPotionMaterial = new Material(hpPotion.material);
        mpPotionMaterial = new Material(mpPotion.material);

        hpPotion.material = hpPotionMaterial;
        mpPotion.material = mpPotionMaterial;
    }

    private void UpdatePotionBars()
    {
        if (playerClass == null || hpPotionMaterial == null || mpPotionMaterial == null ) { return; }

        // TO DO:
        // hacer esto más eficiente para no tener que sacarlo en cada update
        float maxHP = playerClass.maxHealth;
        // revisar:
        float maxMP = playerClass.classStats != null ? playerClass.classStats.MP : 0f;

        float hpFill = CalculateFill(playerClass.health, maxHP);
        float mpFill = CalculateFill(playerClass.MP, maxMP);

        hpPotionMaterial.SetFloat(FillID, hpFill);
        mpPotionMaterial.SetFloat(FillID, mpFill);

        if (HPText != null) { HPText.text = playerClass.health.ToString(); }
        if (MPText != null) { MPText.text = playerClass.MP.ToString(); }
    }

    private float CalculateFill(float current, float max)
    {
        if (max <= 0f) { return 0f; }
        
        // para que el resultado sea entre 0 y 1
        return Mathf.Clamp01(current / max);
    }

    // limpieza de los materiales nuevos
    private void OnDestroy()
    {
        if (hpPotionMaterial != null) { Destroy(hpPotionMaterial); }
        if (mpPotionMaterial != null) { Destroy(mpPotionMaterial); }
    }
}
