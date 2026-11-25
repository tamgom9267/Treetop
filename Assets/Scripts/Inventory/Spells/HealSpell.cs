using System.Collections;
using UnityEngine;

public class HealSpell : SpellObject
{
    public override void SpellEffect()
    {
        if (!CanCast()) return;
        if(!IsOnCooldown)
        {
            PlayerClass playerClass = player.GetComponent<PlayerController>().playerClass;

            if(playerClass.MP -MP < 0 || playerClass.maxHealth == playerClass.health)
            {
                return;
            }

            playerClass.GetComponent<AudioSource>().PlayOneShot(Sound, 0.2f);
            playerClass.SpendMP(MP);
            playerClass.Heal(20);
            SetCooldown(true);
            player.StartCoroutine(EndEffect());
        }
    }

    public override IEnumerator EndEffect(float num = 5)
    {
        yield return new WaitForSeconds(num);
        SetCooldown(false);
    }
}
