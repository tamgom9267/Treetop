using UnityEngine;

public class PlayerClass : MonoBehaviour
{

    public  ClassStats classStats;

    public int maxHealth;
    public int health;
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

    void Awake()
    {

       assignClass();
        
    }


    public void assignClass()
    {
         if (classStats)
        {
            maxHealth = classStats.health;
            health = maxHealth;
            MP = classStats.MP;
            defense = classStats.defense;
            WD = classStats.WD;
            PD = classStats.PD;
            speed = classStats.speed;
        }
        else
        {
            //For when the player doesn't have a class assigned
            health = 1;
            MP = 1;
            defense = 1;
            WD = 1;
            PD = 1;
            speed = 1;
        }
    }

    public void TakeDamage(int damage)
    {
        // Evita recibir dano si el dano es menor o igual a 0
        if (damage <= 0)
            return;
        
        health -= damage;

        // la vida no puede ser menor a 0
        if (health < 0)
            health = 0;

        Debug.Log("Player took " + damage + " damage. Current health: " + health);
    }
}
