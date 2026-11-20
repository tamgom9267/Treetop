using UnityEngine;
using System.Collections;

public class FireBallSpell : SpellObject
{
    [SerializeField] GameObject FireBallParticle;

    bool onCooldown = false;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public override void SpellEffect()
    {
        if (!CanCast()) return;
        if (onCooldown == false){
             PlayerClass playerClass = player.playerClass;
            if(playerClass.MP - 10 < 0)
            {
                return;
            }
            
            playerClass.GetComponent<AudioSource>().PlayOneShot(Sound, 0.2f);
            onCooldown = true;

            playerClass.SpendMP(10);
            GameObject fireball = SpawnProjectile(FireBallParticle, player.transform.position + Vector3.up);
            Rigidbody rigidbody = fireball.GetComponent<Rigidbody>();
            rigidbody.AddForce(player.transform.forward*15,ForceMode.VelocityChange);

        
            
            player.StartCoroutine(EndEffect(5));
        }
       

    }

    public override IEnumerator EndEffect(float num)
    {
        yield return new WaitForSeconds(num);
        onCooldown = false;
        
        
    }


}
