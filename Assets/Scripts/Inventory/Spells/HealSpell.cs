using System.Collections;
using UnityEngine;

public class HealSpell : SpellObject
{
    bool onCooldown = false;

    public override void SpellEffect()
    {
        if (!CanCast()) return;
        if(onCooldown == false)
        {
            PlayerClass playerClass = player.GetComponent<PlayerController>().playerClass;

            if(playerClass.MP -10 < 0 || playerClass.maxHealth == playerClass.health)
            {
                return;
            }

            playerClass.GetComponent<AudioSource>().PlayOneShot(Sound, 0.2f);
            playerClass.SpendMP(10);
            playerClass.Heal(20);
            PublishCast();
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
