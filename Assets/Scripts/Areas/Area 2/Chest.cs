using System.Collections.Generic;
using UnityEngine;

public class Chest : MonoBehaviour
{
    // Estado para definir si se interacuo con el cofre
    public bool chestInteracted;
    public bool PlayerNear = false;

    [SerializeField] GameObject Weapon;
    [SerializeField] GameObject Text;

    private void Start()
    {
        // inicia como falso
        chestInteracted = false;
    }

    public GameObject hasInteracted()
    {
        // Se activa el estado de interaccion con el cofre
        chestInteracted = true;
        Text.SetActive(false);

        GameObject WeaponObj = Instantiate(Weapon);
        WeaponObj.GetComponent<WeaponObject>().isSelected = true;
        WeaponObj.transform.position = new Vector3(this.transform.position.x, this.transform.position.y, this.transform.position.z-3);
        return WeaponObj;
    }

    private void OnTriggerEnter(Collider other)
    {
        // Solo se activa si el jugador "Player" entra
        if (other.CompareTag("Player"))
        {
            if (chestInteracted == false)
            {
               Text.SetActive(true); 
               PlayerNear = true;  
            }
           
        }

    }
    
    private void OnTriggerExit(Collider other)
    {
        // Solo se activa si el jugador "Player" entra
        if (other.CompareTag("Player"))
        {
            Text.SetActive(false);
            PlayerNear = false;
        }
    }

   
}
