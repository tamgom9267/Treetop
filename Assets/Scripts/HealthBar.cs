using System;
using UnityEngine;
using UnityEngine.UI;

public class HealthBar : MonoBehaviour
{
    [SerializeField]
    public Player_Class player_Class;

    public Slider HP;
    public Slider MP;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Transform hpObject = transform.Find("HP");
        HP = hpObject.GetComponentInChildren<Slider>();
        HP.maxValue = player_Class.health;

        Transform mpObject = transform.Find("MP");
        MP = mpObject.GetComponentInChildren<Slider>();
        MP.maxValue = player_Class.MP;
    }

    // Update is called once per frame
    void Update()
    {
        HP.value = player_Class.health;
        MP.value = player_Class.MP;
    }
}
