using UnityEngine;
using System.Collections;
using Unity.VisualScripting;

public class FireBallSpell : SpellObject
{
    [SerializeField] GameObject FireBallParticle;
    GameObject Fireball;

    bool onCooldown = false;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public override void SpellEffect()
    {
        PlayerClass playerClass = player.GetComponent<PlayerController>().playerClass;
        if(playerClass.MP - 10 < 0)
        {
            return;
        }
        
        onCooldown = true;

        playerClass.SpendMP(10);
        Fireball = Instantiate(FireBallParticle, player.transform.position, player.transform.rotation);
        Fireball.transform.position = player.transform.position + Vector3.up;
        Rigidbody rigidbody = Fireball.GetComponent<Rigidbody>();
        rigidbody.AddForce(player.transform.forward*15,ForceMode.VelocityChange);

    
        
        player.StartCoroutine(EndEffect(5));

    }

    public override IEnumerator EndEffect(float num)
    {
        yield return new WaitForSeconds(num);
        onCooldown = false;
        
        
    }


}
