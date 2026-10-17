using System.Collections.Generic;
using UnityEngine;
using System.Collections;

public class Chest : MonoBehaviour
{
    // Estado para definir si se interacuo con el cofre
    public bool chestInteracted;
    public bool PlayerNear = false;

    private Animator animator;

    [SerializeField] GameObject Weapon;
    [SerializeField] GameObject Text;

    private void Start()
    {
        // inicia como falso
        chestInteracted = false;

        animator = GetComponent<Animator>();
    }

    public GameObject hasInteracted()
    {
        // Activar la animación de abrir cofre
        animator.SetBool("isOpen", true);

        // Se activa el estado de interaccion con el cofre
        chestInteracted = true;
        Text.SetActive(false);

        // posicion donde va a estar el arma cuando el jugador la pueda agarrar
        Vector3 weaponPos = new Vector3(
            transform.position.x,
            transform.position.y,
            transform.position.z - 3
        ); 

        GameObject WeaponObj = Instantiate(Weapon);
        WeaponObj.GetComponent<WeaponObject>().isSelected = true;

        // que inicie dentro del cofre el arma
        WeaponObj.transform.position = transform.position;

        // coroutine para que el arma salte del cofre :D
        StartCoroutine(AnimacionArma(WeaponObj, weaponPos));

        // implementacion antigua, no tenía la animación de saltar del cofre, borrar despues
        // WeaponObj.transform.position = new Vector3(this.transform.position.x, this.transform.position.y, this.transform.position.z-3);
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

    private IEnumerator AnimacionArma(GameObject weaponObj, Vector3 posFinal)
    {
        float duracion = 0.4f;
        float altura = 1.2f;

        Vector3 posInicial = weaponObj.transform.position;
        float tiempo = 0f;

        // salto inicial
        while (tiempo < duracion)
        {
            tiempo += Time.deltaTime;
            float t = Mathf.Clamp01(tiempo / duracion);

            // movimiento horizontal y vertical
            weaponObj.transform.position = Vector3.Lerp(
                posInicial,
                posFinal,
                t
            );

            // 4 es el modificador, t es el tiempo y (1-t) es para que vuelva a bajar
            float salto = 4f * altura * t * (1f - t);
            weaponObj.transform.position += Vector3.up * salto;

            yield return null;
        }

        // pequeño rebote al final para que no se vea tan plano
        float alturaRebote = 0.2f; // 20 cm
        float duracionRebote = 0.15f;
        float tiempoRebote = 0f;

        while (tiempoRebote < duracionRebote)
        {
            tiempoRebote += Time.deltaTime;
            float t = Mathf.Clamp01(tiempoRebote / duracionRebote);

            // sube y baja rapido
            float rebote = alturaRebote * Mathf.Sin(t * Mathf.PI);

            weaponObj.transform.position = posFinal + Vector3.up * rebote;

            yield return null;
        }


        weaponObj.transform.position = posFinal;
    }
}
