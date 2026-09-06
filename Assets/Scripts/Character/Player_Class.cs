using UnityEngine;

public class Player_Class : MonoBehaviour
{

    public int health;
    public int MP;
    public int defense;
    public int AD;
    public int AP;
    public int speed;


    void Awake()
    {
        //Warrior Stats
        health = 100;
        MP = 20;
        defense = 10;
        AD = 10;
        AP = 1;
        speed = 5;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
