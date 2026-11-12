using UnityEngine;

public class ActiveFireBall : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }


     void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player")) return;
        
        if (other.CompareTag("PhysicalEnemy"))
        {
            other.gameObject.GetComponent<EnemyCombat>().TakeDamage(11);
            Destroy(this.gameObject);
        }
        else
        {
            Destroy(this.gameObject);
        }
    }
}
