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


    public int health;
    public int MP;
    public int defense;
    
    public int AD;

    public int AP;

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
    void Start()
    {
        name = weaponData.name;
        health = weaponData.health;
        MP = weaponData.MP;
        defense = weaponData.defense;
        AD = weaponData.AD;
        AP = weaponData.AP;
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
}
