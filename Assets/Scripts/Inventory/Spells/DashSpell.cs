using System.Collections;
using UnityEngine;

public class DashSpell : SpellObject
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public override void SpellEffect()
    {
        PlayerClass playerClass = player.GetComponent<PlayerController>().playerClass;
        playerClass.speed += speed;

        EndEffect( 3);

    }

    public override IEnumerator EndEffect(float num)
    {
        yield return new WaitForSeconds(num);
        PlayerClass playerClass = player.GetComponent<PlayerController>().playerClass;
        playerClass.speed -= speed;
        
    }
}
