using Microsoft.Unity.VisualStudio.Editor;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    public Rigidbody rb;
    [SerializeField]
    public PlayerClass playerClass;
    public PlayerInventory Inventory;
    public Animator playerAnimation;
    private Vector2 _moveDirection;
    public bool isAttacking = false;
    public WeaponData weapon;

    public InventoryObject Slot1;
    public InventoryObject Slot2;
    public InventoryObject Slot3;
    
    public InputActionReference move;
    public InputActionReference attack;
    public InputActionReference interact;

    public InputActionReference inventory;

    Transform handObj;

    


    WeaponObject nearestWeapon;


    [SerializeField]
    GameObject InventoryUI; 

    void Start()
    {
       rb = GetComponent<Rigidbody>();
       playerClass = GetComponent<PlayerClass>();
       Inventory = GetComponent<PlayerInventory>();

       playerAnimation = GetComponent<Animator>();

       move.action.Enable();
       attack.action.Enable();
       interact.action.Enable();
       inventory.action.Enable();

       handObj = transform.Find("Hand");
       GameObject equipedWeapon = Instantiate(weapon.prefab,handObj);
       equipedWeapon.GetComponent<WeaponObject>().player = this;
       equipedWeapon.GetComponent<SphereCollider>().enabled = false;
       equipedWeapon.GetComponent<Rigidbody>().isKinematic = true;
       
    }

    // Update is called once per frame
    void Update()
    {
        _moveDirection = move.action.ReadValue<Vector2>();
        if (!isAttacking)
        {
            RotatePlayer(_moveDirection);    
        }
        
        rb.linearVelocity = new Vector3(_moveDirection.x * playerClass.speed, rb.linearVelocity.y ,_moveDirection.y * playerClass.speed);
        
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
            
            isAttacking = true;

            playerAnimation.SetTrigger("isAttacking");
        }

        if (inventory.action.WasPressedThisFrame())
        {
            InventoryUI.SetActive(!InventoryUI.activeSelf);
        }

    }
    
    void RotatePlayer(Vector2 direction)
    {
        if (direction == Vector2.zero)
            return;
        
        Vector3 lookDirection = new Vector3(direction.x, 0, direction.y);

        transform.rotation = Quaternion.LookRotation(lookDirection);
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

    // esta funcion llama por Animation Event al terminar la animacion de ataque.
    public void EndAttack()
    {
        isAttacking = false;
    }


    private void OnTriggerExit()
    {
        Debug.Log("s");
    }
    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("aaaaa");
        if(other.CompareTag("Weapon"))
        {
            Debug.Log("a");
            WeaponObject weaponObj = other.gameObject.GetComponent<WeaponObject>();
            if(weaponObj == nearestWeapon)
            {
                nearestWeapon.isSelected = true;
            }
            else
            {
                nearestWeapon = weaponObj;
                Debug.Log("b");
                
            }
            
        }

        
    }

}
