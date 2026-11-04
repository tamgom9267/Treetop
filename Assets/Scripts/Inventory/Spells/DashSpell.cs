using System.Collections;
using Photon.Realtime;
using UnityEngine;

public class DashSpell : SpellObject
{
    bool onCooldown = false;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public override void SpellEffect()
    {
        PlayerClass playerClass = player.GetComponent<PlayerController>().playerClass;
        playerClass.speed += 10;
        onCooldown = true;

        playerClass.SpendMP(5);
        player.StartCoroutine(EndEffect(3));

    }

    public override IEnumerator EndEffect(float num)
    {
        yield return new WaitForSeconds(num);
        PlayerClass playerClass = player.GetComponent<PlayerController>().playerClass;
        playerClass.speed -= 10;
        
    }
}
