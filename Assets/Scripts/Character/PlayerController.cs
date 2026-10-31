using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using System.Collections.Generic;
using Unity.VisualScripting;

public class PlayerController : MonoBehaviour
{
    public Rigidbody rb;

    [SerializeField]
    public PlayerClass playerClass;
    public PlayerInventory Inventory;
    public Animator playerAnimation;
    private Vector2 _moveDirection;

    private Coroutine hitStopCoroutine;
    private float hitStopEndTime;
    private float animatorSpeedBeforeHitStop;

    // Guarda los slimes que ya fueron golpeados durante el ataque actual.
    private HashSet<GameObject> enemiesHitThisAttack = new HashSet<GameObject>();
    public bool isAttacking = false;
    private bool inAir = false;

    [Header("Rotacion")]
    [SerializeField] private float turnSpeed = 720f;

    private Quaternion targetRotation;
    private bool hasTargetRotation;

    // Collider que detecta el golpe de la espada.
    private BoxCollider equippedWeaponCollider;

    public GameObject weapon;

    private WeaponObject equippedWeapon;

    public InventoryObject Slot1;
    public InventoryObject Slot2;
    public InventoryObject Slot3;
    
    public InputActionReference move;
    public InputActionReference attack;
    public InputActionReference interact;

    public InputActionReference inventory;
    public InputActionReference rotate;

    public GameObject nearestWeapon;
    public InventoryObject nearestWeaponObj;
    public InventoryObject SelectedItem;
    public bool isSelected = false;

    public GameObject nearChest;

    [SerializeField] GameObject InventoryUI;

    [SerializeField] Transform handObj;

    public List<GameObject> nearWeapons = new List<GameObject>();
    

    private void Awake()
    {
        // Guarda la rotacion inicial
        targetRotation = transform.rotation;
    }

    private void Start()
    {
       rb = GetComponent<Rigidbody>();
       playerClass = GetComponent<PlayerClass>();

       playerAnimation = GetComponent<Animator>();

       move.action.Enable();
       attack.action.Enable();
       interact.action.Enable();
       inventory.action.Enable();
       rotate.action.Enable();

       Inventory.GetComponent<PlayerInventory>().AssignPlayer(this);

    }

    // Update is called once per frame
    private void Update()
    {
        if(InventoryUI.activeSelf == false)
        {
            UpdateNearestWeapon();
        }
        
        _moveDirection = move.action.ReadValue<Vector2>();
        if(InventoryUI.activeSelf == true)
        {
            rb.linearVelocity = new Vector3(0,0,0);
            playerAnimation.SetFloat("Speed", 0);
            if (move.action.WasPressedThisFrame())
            {
                InventoryUI.GetComponent<PlayerInventory>().MovingCursor(new Vector2(_moveDirection.x, _moveDirection.y));
            }
            if (Inventory.GetComponent<PlayerInventory>().cursor.GetComponent<CursorObject>().isPressed == false && Inventory.GetComponent<PlayerInventory>().cursor.GetComponent<CursorObject>().InventoryItemObj != null && attack.action.WasPressedThisFrame())
            {
                weapon = Inventory.GetComponent<PlayerInventory>().cursor.GetComponent<CursorObject>().InventoryItemObj.GetComponent<PanelManager>().itemObj;
                equipWeapon();
            }

            if (rotate.action.WasPressedThisFrame())
            {
                Inventory.GetComponent<PlayerInventory>().RotateObject();
            }
        }

        else
        {
            playerAnimation.SetFloat("Speed", _moveDirection.magnitude);

            if (!inAir)
                {
                    if (!isAttacking)
                    {
                        RotatePlayer(_moveDirection);    
                    }
                    rb.linearVelocity = new Vector3(_moveDirection.x * playerClass.speed, rb.linearVelocity.y ,_moveDirection.y * playerClass.speed);
                }

                if (attack.action.WasPressedThisFrame() && !isAttacking)
            {
                // leer teclas - ijkl
                Vector2 attackInput = attack.action.ReadValue<Vector2>();

                // Definir direccion de ataque - arriba, abajo, isquierda, derecha 
                // (no es posible hacer ataques diagonales una de las direcciones va a tomar prioridad)
                Vector2 attackDirection = GetAttackDirections(attackInput);

                // se rota el personaje a la direccion assignada
                RotatePlayer(attackDirection);

                Debug.Log("Attaque: " + attackDirection);

                // Un nuevo ataque puede volver a golpear a los slimes.
                enemiesHitThisAttack.Clear();
                
                isAttacking = true;

                playerAnimation.SetTrigger("isAttacking");
            }


            if (interact.action.WasPressedThisFrame() && nearChest != null && nearChest.GetComponent<Chest>().chestInteracted == false)
            {
                nearestWeapon = nearChest.GetComponent<Chest>().hasInteracted();
            }       
            else if(interact.action.WasPressedThisFrame() && InventoryUI.activeSelf == false && nearestWeaponObj != null)
            {
                InventoryUI.GetComponent<PlayerInventory>().InspectLoot(nearestWeapon);    
                InventoryUI.SetActive(true);

            }
        

            // Gira hacia la direccion indicada.
            if (hasTargetRotation)
            {
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    targetRotation,
                    turnSpeed * Time.deltaTime
                );
            }
        }

        if (inventory.action.WasPressedThisFrame())
            {
                if(InventoryUI.activeSelf == true)
                {
                    if(Inventory.Loot.Count > 0)
                    {
                        InventoryUI.GetComponent<PlayerInventory>().RemoveWeaponFromChestLootGrid(this.transform.position);
                        
                    }
                    
                    
                }
                InventoryUI.SetActive(!InventoryUI.activeSelf);
            }
    }
    
    private void RotatePlayer(Vector2 direction)
    {
        if (direction == Vector2.zero)
            return;
        
        Vector3 lookDirection = new Vector3(direction.x, 0, direction.y);

        // Guarda hacia donde debe mirar el jugador.
        targetRotation = Quaternion.LookRotation(lookDirection);
        hasTargetRotation = true;
    }

    Vector2 GetAttackDirections(Vector2 direction)
    {
        if (direction == Vector2.zero)
            return Vector2.zero;
        
        // Si predomina el eje horizontal
        if (Mathf.Abs(direction.x) >= Mathf.Abs(direction.y))
        {
            return new Vector2(Mathf.Sign(direction.x), 0);
        }

        // Si predomina el eje vertical
        return new Vector2(0, Mathf.Sign(direction.y));
    }

    // Devuelve true solo la primera vez que este ataque golpea al slime.
    public bool RegisterEnemyHit(GameObject enemy)
    {
        // No permite golpes si el jugador no está atacando.
        if (!isAttacking)
            return false;

        // No permite golpear dos veces al mismo slime en el mismo ataque.
        if (enemiesHitThisAttack.Contains(enemy))
            return false;

        // Registra al slime como golpeado.
        enemiesHitThisAttack.Add(enemy);

        return true;
    }

    public void TriggerHitStop(float duration)
    {
        if (duration <= 0f)
            return;

        hitStopEndTime = Mathf.Max(hitStopEndTime, Time.unscaledTime + duration);

        if (hitStopCoroutine != null)
            return;

        animatorSpeedBeforeHitStop = playerAnimation.speed;
        playerAnimation.speed = 0f;
        hitStopCoroutine = StartCoroutine(RestoreAnimationAfterHitStop());
    }

    private IEnumerator RestoreAnimationAfterHitStop()
    {
        while (Time.unscaledTime < hitStopEndTime)
            yield return null;

        playerAnimation.speed = animatorSpeedBeforeHitStop;
        hitStopCoroutine = null;
    }

    // Se llama en Animation Event cuando inicia el ataque.
    public void EnableWeaponCollider()
    {
        if (weapon)
        {
            equippedWeaponCollider.enabled = true;
            equippedWeapon.EnableTrail();  
        }
        
    }

    // esta funcion llama por Animation Event al terminar la animacion de ataque.
    public void EndAttack()
    {
        // Desactiva el collider al terminar el ataque.

        if (weapon)
        {
            equippedWeaponCollider.enabled = false;
            equippedWeapon.DisableTrail();
 
        }
        isAttacking = false;  
    }

    public void ApplyEnemyKnockback(GameObject enemy)
    {
        // No reinicia el knockback si el jugador esta en el aire
        if (inAir)
            return;
        
        float knockbackMultiplier;

        if (enemy.CompareTag("PhysicalEnemy"))
        {
            // Los enemigos fisicos aplican el knockback completo
            knockbackMultiplier = 1f;
        }
        else if (enemy.CompareTag("ProjectileEnemy"))
        {
            // Los enemigos de proyectiles aplican un tercio de el knockback
            knockbackMultiplier = 1f / 3f;
        }
        else
        {
            knockbackMultiplier = 10f;
            Debug.LogWarning("Tag no asignado");
        }

        // No se puede controlar el movimiento del jugador mientras esta en el aire
        inAir = true;

        // Direccion desde el enemigo hacia el jugador.
        Vector3 knockbackDirection = transform.position - enemy.transform.position;

        // El calculo horizontal solo usa X y Z.
        knockbackDirection.y = 0f;

        // Normaliza la direccion para obtener un vector unitario.
        knockbackDirection.Normalize();

        // Detiene el movimiento previo para que el impulso sea consistente.
        rb.linearVelocity = Vector3.zero;

        // Calcula la fuerza de knockback basada en la direccion y la fuerza definida en PlayerClass.
        Vector3 knockbackForce = new Vector3(
            knockbackDirection.x * playerClass.enemyKnockbackForce * knockbackMultiplier,
            playerClass.enemyKnockbackUpForce * knockbackMultiplier,
            knockbackDirection.z * playerClass.enemyKnockbackForce * knockbackMultiplier
        );

        // Aplica la fuerza de knockback al Rigidbody del jugador.
        rb.AddForce(knockbackForce, ForceMode.Impulse);
    }

    public void equipWeapon()
    {
     

       //    handObj = transform.Find("Hand");
       GameObject WeaponObj = Instantiate(weapon.GetComponent<WeaponObject>().weaponData.prefab,handObj);
       WeaponObj.transform.localPosition = new Vector3(0,0,0);
       WeaponObj.GetComponent<WeaponObject>().inHand = true;

       equippedWeapon = WeaponObj.GetComponent<WeaponObject>();

       WeaponObj.GetComponent<WeaponObject>().player = this;
       WeaponObj.GetComponent<SphereCollider>().enabled = false;

       // La espada no puede hacer damage si el jugador no esta atacando.
        equippedWeaponCollider = WeaponObj.GetComponent<BoxCollider>();
        equippedWeaponCollider.enabled = false;

        Inventory.gameObject.GetComponent<UIDisplay>().UIEquippedWeaponDisplay();
    }

    public void UnequipWeapon()
    {
        weapon = null;
        equippedWeapon = null;
        Destroy(handObj.GetChild(1).gameObject);
        Inventory.gameObject.GetComponent<UIDisplay>().UIEquippedWeaponDisplay();
    }

    private void OnCollisionEnter(Collision collision)
    {    
        // solo se aplica si el jugador no esta en el aire
        if (!inAir)
            return;

        // Si colisiona con el suelo, se termina el knockback
        if (collision.gameObject.CompareTag("Obstacle"))
        {
            inAir = false;
        }
    }

    public void SetNearestWeaponFalse()
    {
        if(nearestWeapon != null)
        {
            nearestWeaponObj.GetComponent<WeaponObject>().isSelected = false;
            nearestWeaponObj =null;
            nearestWeapon = null;    
        }
    }


    void UpdateNearestWeapon()
    {
        nearWeapons.RemoveAll(w => w == null || w.activeInHierarchy == false);

        GameObject closest = null;
        float closestDistance = 1000f;

        for(int i = 0; i < nearWeapons.Count; i++)
        {
            float distance = Vector3.Distance(transform.position, nearWeapons[i].transform.position);
            if(distance < closestDistance)
            {
                closestDistance = distance;
                closest = nearWeapons[i];
            }
        }

        if(closest == nearestWeapon)
        {
            return;
        }

        if(nearestWeaponObj != null)
        {
            nearestWeaponObj.GetComponent<WeaponObject>().isSelected = false;
        }

        nearestWeapon = closest;

        if(closest != null)
        {
            nearestWeaponObj = closest.GetComponent<WeaponObject>();
            nearestWeaponObj.GetComponent<WeaponObject>().isSelected = true;
        }
        else
        {
            nearestWeaponObj = null;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if(other.CompareTag("Weapon"))
        {
            if(nearWeapons.Contains(other.gameObject) == false)
            {
                nearWeapons.Add(other.gameObject);
            }
        }
        else if (other.CompareTag("Chest"))
        {
            nearChest = other.gameObject;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if(other.CompareTag("Weapon"))
        {
            nearWeapons.Remove(other.gameObject);
        }
    }

}