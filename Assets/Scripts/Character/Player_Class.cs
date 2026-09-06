using UnityEngine;

public class Player_Class : MonoBehaviour
{

    public  ClassStats classStats;

    public int health;
    public int MP;
    public int defense;
    public int AD;
    public int AP;
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
            health = classStats.health;
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
