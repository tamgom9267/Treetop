using System.Collections;
using UnityEngine;

public class HealSpell : SpellObject
{
    bool onCooldown = false;

    public override void SpellEffect()
    {
        if(onCooldown == false)
        {
            PlayerClass playerClass = player.GetComponent<PlayerController>().playerClass;

            if(playerClass.MP < 10 || playerClass.maxHealth == playerClass.health)
            {
                return;
            }

            playerClass.SpendMP(10);
            playerClass.Heal(20);
            onCooldown = true;
            player.StartCoroutine(EndEffect());
        }
    }

    public override IEnumerator EndEffect(float num = 5)
    {
        yield return new WaitForSeconds(num);
        onCooldown = false;
    }
}