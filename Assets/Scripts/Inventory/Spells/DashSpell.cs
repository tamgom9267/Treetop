using System.Collections;
using Photon.Realtime;
using UnityEngine;

public class DashSpell : SpellObject
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public override void SpellEffect()
    {
        if (!CanCast()) return;
        if (!IsOnCooldown)
        {
            PlayerClass playerClass = player.GetComponent<PlayerController>().playerClass;

            if(playerClass.MP < MP)
            {
                return;
            }

            playerClass.GetComponent<AudioSource>().PlayOneShot(Sound, 0.2f);
            playerClass.speed += 10;
            SetCooldown(true);

            playerClass.SpendMP(MP);
            player.StartCoroutine(EndEffect(3));   
        }
        
    }

    //No tiene cooldown por lo que se puede hacer el hechizo multiples veces
    public override IEnumerator EndEffect(float num)
    {
        yield return new WaitForSeconds(num);
        PlayerClass playerClass = player.GetComponent<PlayerController>().playerClass;
        playerClass.speed -= 10;
        SetCooldown(false);
        
    }
}
