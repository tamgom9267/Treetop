using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class CursorObject : MonoBehaviour
{
    public Vector2 CurrentPosition = new Vector2(0,0);
    //Zona 0 es el chestLoot y 1 es el inventario
    public int InventoryZone = 0;
    public PlayerInventory Inventory;
    [SerializeField] InputActionReference select;
    public bool isPressed = false;
    PlayerInventory InventoryItem;
    public GameObject InventoryItemObj;
    
    Vector2 CopyCurrentsize;

    void Start()
    {
        CopyCurrentsize = transform.GetComponent<RectTransform>().sizeDelta;
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

    void updateSize(Vector2 copy)
    {
        transform.GetComponent<RectTransform>().sizeDelta = copy;  
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
            updateSize(other.transform.GetComponent<RectTransform>().sizeDelta);
            InventoryItem = Inventory.GetComponent<PlayerInventory>();
            InventoryItemObj = other.gameObject;
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        updateSize(CopyCurrentsize);
        InventoryItem = null;
        InventoryItemObj = null;
        gameObject.GetComponent<Image>().color = Color.green;
    }


}
