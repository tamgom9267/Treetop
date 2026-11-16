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
    [SerializeField] private bool randomLoot = true;

    [Header("Particulas")]
    [SerializeField] private ParticleSystem[] chestParticles;

    private void Start()
    {
        animator = GetComponent<Animator>();
        if (chestInteracted) PresentOpen();
    }

    public GameObject hasInteracted()
    {
        var run = MultiplayerRunSession.Instance;
        if (run != null && run.IsInMultiplayerScene)
        {
            if (MultiplayerWorldSession.Instance != null)
                MultiplayerWorldSession.Instance.RequestChestOpen(this);
            return null;
        }
        if (chestInteracted) return null;
        GameObject weaponPrefab = ChooseWeaponPrefab();
        if (weaponPrefab == null) return null;
        ApplySharedOpen();

        GameObject weaponObj = Instantiate(weaponPrefab, transform.position, Quaternion.identity);
        weaponObj.GetComponent<WeaponObject>().isSelected = true;
        StartCoroutine(AnimacionArma(weaponObj, LootPosition));
        return weaponObj;
    }

    public Vector3 LootPosition => transform.position + Vector3.back * 3f;

    // En multijugador solo el host hace esta seleccion.
    public GameObject ChooseWeaponPrefab()
    {
        GameObject weaponPrefab = Weapon;
        if (randomLoot)
        {
            GameObject[] lootPrefabs = Resources.LoadAll<GameObject>("GeneracionDeCuartos/Armas");
            if (lootPrefabs.Length == 0)
            {
                Debug.LogError("No se encontraron prefabs de loot en Resources/GeneracionDeCuartos/Armas.", this);
                return null;
            }

            weaponPrefab = lootPrefabs[Random.Range(0, lootPrefabs.Length)];
        }

        if (weaponPrefab == null)
        {
            Debug.LogError("El cofre no tiene un prefab de arma asignado.", this);
            return null;
        }

        return weaponPrefab;
    }

    public void ApplySharedOpen()
    {
        if (chestInteracted) return;
        chestInteracted = true;
        PresentOpen();
    }

    private void PresentOpen()
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (animator != null) animator.SetBool("isOpen", true);
        if (Text != null) Text.SetActive(false);
        if (chestParticles == null) return;
        foreach (ParticleSystem particle in chestParticles)
            if (particle != null) particle.gameObject.SetActive(true);
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
            var player = other.GetComponent<PlayerController>();
            if (player != null && player.nearChest == gameObject) player.nearChest = null;
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
            // la funcion es una parábola que sube y baja, multiplicada por la altura
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
            // clamp 01 hace que el valor de t siempre este entre 0 y 1
            // para que no se pase del tiempo de rebote
            float t = Mathf.Clamp01(tiempoRebote / duracionRebote);

            // sube y baja rapido
            float rebote = alturaRebote * Mathf.Sin(t * Mathf.PI);

            weaponObj.transform.position = posFinal + Vector3.up * rebote;

            yield return null;
        }


        weaponObj.transform.position = posFinal;
    }
}
