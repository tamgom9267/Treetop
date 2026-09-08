using UnityEngine;

public class PlayerClass : MonoBehaviour
{

    public  ClassStats classStats;

    public PlayerInventory Inventory;

    public InventoryObject Weapon;
    public InventoryObject Slot1;
    public InventoryObject Slot2;
    public InventoryObject Slot3;

    public int maxhHealth;
    public int health;
    public int MP;
    public int defense;
    
    //Daño físico(Attack Damage)
    public int AD;

    //Daño de proyectil(Ability Points)
    public int AP;

    //Poder de curación(Healing Bonus)
    public int HB;
    public int speed;


    void Awake()
    {

       assignClass();
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }


    public void assignClass()
    {
         if (classStats)
        {
            maxhHealth = classStats.health;
            health = maxhHealth;
            MP = classStats.MP;
            defense = classStats.defense;
            AD = classStats.AD;
            AP = classStats.AP;
            speed = classStats.speed;
        }
        else
        {
            //For when the player doesn't have a class assigned
            health = 1;
            MP = 1;
            defense = 1;
            AD = 1;
            AP = 1;
            speed = 1;
        }
    }
}
