using System.Collections;
using UnityEngine;

public class DashSpell : SpellObject
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public override void SpellEffect(PlayerController Player)
    {
        PlayerClass playerClass = Player.GetComponent<PlayerController>().playerClass;
        playerClass.speed += speed;

        EndEffect(3, playerClass);

    }

    public override IEnumerator EndEffect(float num, PlayerClass playerClass)
    {
        yield return new WaitForSeconds(num);
        playerClass.speed -= speed;
        
    }
}
