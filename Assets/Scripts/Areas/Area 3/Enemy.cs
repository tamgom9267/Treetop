using UnityEngine;

public class Enemy : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        // solo el arma puede eliminar a el enemigo
        if (other.CompareTag("Weapon"))
            Destroy(gameObject);
    }
}
