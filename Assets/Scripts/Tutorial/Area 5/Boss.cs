using UnityEngine;

public class Boss : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        // solo el arma puede eliminar a el enemigo
        if (other.CompareTag("Weapon"))
            Destroy(gameObject);
    }
}
