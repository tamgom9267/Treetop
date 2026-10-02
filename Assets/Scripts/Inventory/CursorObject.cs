using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class CursorObject : MonoBehaviour
{
    public Vector2 CurrentPosition = new Vector2(0,0);
    //Zona 0 es el chestLoot y 1 es el inventario
    public int InventoryZone = 0;
    public PlayerInventory Inventory;
    [SerializeField] InputActionReference select;
    bool isPressed = false;
    PlayerInventory InventoryItem;
    GameObject InventoryItemObj;

    void Start()
    {
        select.action.Enable();
    }

    // Update is called once per frame
    void Update()
    {
        if (select.action.WasPressedThisFrame() && isPressed == false)
        {
            isPressed = true;
            if (InventoryItemObj != null)
            {
                InventoryItem.isMovingObject(InventoryItemObj.gameObject);
            }
            StartCoroutine(counter());
        }

    }

    IEnumerator counter()
    {
        yield return new WaitForSeconds(0.1f);
        isPressed = false;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if(other.gameObject.CompareTag("Item"))
        {
            InventoryItem = Inventory.GetComponent<PlayerInventory>();
            InventoryItemObj = other.gameObject;
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        InventoryItem = null;
        InventoryItemObj = null;
    }


}
