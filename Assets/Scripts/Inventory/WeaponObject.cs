using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class WeaponObject : InventoryObject
{
    [SerializeField]
    public PlayerController player;

    [SerializeField]
    private WeaponData weaponData;

    [Header("Trail/Smear de Arma")]
    [SerializeField]
    private TrailRenderer weaponTrail;



    public int health;
    public int MP;
    public int defense;
    
    public int WD;

    public int PD;

    public int HB;
    public int speed;

    public string GetInteractText() => $"Player picked up {gameObject.name}";

    public Transform GetTransform() => transform; 

    public bool isSelected = false;   


    public void looted()
    {
        player.Inventory.SelectedObject = this;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        name = weaponData.name;
        health = weaponData.health;
        MP = weaponData.MP;
        defense = weaponData.defense;
        WD = weaponData.WD;
        PD = weaponData.PD;
        HB = weaponData.HB;
        speed = weaponData.speed;

    }

    // Update is called once per frame
    void Update()
    {
        if(isSelected == true)
        {
            GameObject InteractCanva = transform.GetChild(4).gameObject;
            InteractCanva.SetActive(true);
           

        }
        else
        {
            GameObject InteractCanva = transform.GetChild(4).gameObject;
            InteractCanva.SetActive(false);
        }
        

    }

    public void EnableTrail()
    {
        weaponTrail.emitting = true;
    }

    public void DisableTrail()
    {
        weaponTrail.emitting = false;
    }
}
