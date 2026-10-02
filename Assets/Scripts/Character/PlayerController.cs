using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using System.Collections.Generic;

public class PlayerController : MonoBehaviour
{
    public Rigidbody rb;

    [SerializeField]
    public PlayerClass playerClass;
    public PlayerInventory Inventory;
    public Animator playerAnimation;
    private Vector2 _moveDirection;

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

    public WeaponData weapon;

    private WeaponObject equippedWeapon;

    public InventoryObject Slot1;
    public InventoryObject Slot2;
    public InventoryObject Slot3;
    
    public InputActionReference move;
    public InputActionReference attack;
    public InputActionReference interact;

    public InputActionReference inventory;

    public GameObject nearestWeapon;
    public InventoryObject nearestWeaponObj;
    public InventoryObject SelectedItem;
    public bool isSelected = false;

    [SerializeField] GameObject InventoryUI;

    [SerializeField] Transform handObj;

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

    //    handObj = transform.Find("Hand");
       GameObject equipedWeapon = Instantiate(weapon.prefab,handObj);

       equippedWeapon = equipedWeapon.GetComponent<WeaponObject>();

       equipedWeapon.GetComponent<WeaponObject>().player = this;
       equipedWeapon.GetComponent<SphereCollider>().enabled = false;

       // La espada no puede hacer damage si el jugador no esta atacando.
        equippedWeaponCollider = equipedWeapon.GetComponent<BoxCollider>();
        equippedWeaponCollider.enabled = false;
    }

    // Update is called once per frame
    private void Update()
    {
        
        _moveDirection = move.action.ReadValue<Vector2>();
        if(InventoryUI.activeSelf == true)
        {
            if (move.action.WasPressedThisFrame())
            {
                InventoryUI.GetComponent<PlayerInventory>().MovingCursor(new Vector2(_moveDirection.x, _moveDirection.y));
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

        if (inventory.action.WasPressedThisFrame())
        {
            if(InventoryUI.activeSelf == true && nearestWeaponObj != null)
            {
                if(Inventory.Loot.Count > 0)
                {
                    InventoryUI.GetComponent<PlayerInventory>().RemoveWeaponFromChestLootGrid(nearestWeapon.GetComponent<InventoryObject>().ID, this.transform.position);
                }
                
                
            }
            InventoryUI.SetActive(!InventoryUI.activeSelf);
        }

        if(interact.action.WasPressedThisFrame() && InventoryUI.activeSelf == false && nearestWeaponObj != null)
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

    // Se llama en Animation Event cuando inicia el ataque.
    public void EnableWeaponCollider()
    {
        equippedWeaponCollider.enabled = true;
        equippedWeapon.EnableTrail();
    }

    // esta funcion llama por Animation Event al terminar la animacion de ataque.
    public void EndAttack()
    {
        // Desactiva el collider al terminar el ataque.
        equippedWeaponCollider.enabled = false;
        equippedWeapon.DisableTrail();

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

    private void OnTriggerEnter(Collider other)
    {
        if(other.CompareTag("Weapon"))
        {
            Debug.Log("a");
            WeaponObject weaponObj = other.gameObject.GetComponent<WeaponObject>();
            nearestWeaponObj =weaponObj;
            nearestWeapon = other.gameObject;
            if(weaponObj == nearestWeaponObj)
           {
                nearestWeaponObj.GetComponent<WeaponObject>().isSelected = true;
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
         if(other.CompareTag("Weapon"))
        {
            nearestWeaponObj.GetComponent<WeaponObject>().isSelected = false;
            nearestWeaponObj =null;
            nearestWeapon = null;
        }
    }
}
