using System.Collections;
using UnityEngine;

public abstract class SpellObject : InventoryObject
{

    public int health;
    public int MP;
    public int defense;
    
    public int WD;

    public int PD;

    public int HB;
    public int speed;

    public abstract void SpellEffect(PlayerController player);

    public virtual IEnumerator EndEffect(float num, PlayerClass playerClass)
    {
        yield return new WaitForSeconds(num);
        
    }
}
