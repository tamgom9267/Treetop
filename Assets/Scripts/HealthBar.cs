using UnityEngine;
using UnityEngine.UI;

public class HealthBar : MonoBehaviour
{
    [SerializeField]
    public PlayerClass playerClass;

    public Slider HP;
    public Slider MP;

    public Text NumbHP;
    public Text NumbMP;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Transform hpObject = transform.Find("HP");
        HP = hpObject.GetComponentInChildren<Slider>();
        HP.maxValue = playerClass.health;

        Transform mpObject = transform.Find("MP");
        MP = mpObject.GetComponentInChildren<Slider>();
        MP.maxValue = playerClass.MP;
    }

    // Update is called once per frame
    void Update()
    {
        HP.value = playerClass.health;
        MP.value = playerClass.MP;

        NumbHP.text = playerClass.health.ToString();
        NumbMP.text = playerClass.MP.ToString();
    }
}
