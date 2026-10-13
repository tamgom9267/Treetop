using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
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
    bool isMoving = false;
    PlayerInventory InventoryItem;
    public GameObject InventoryItemObj;
    
    Vector2 CopyCurrentsize;
    Vector2 CopyColliderSize;
    Vector2 CopyColliderOffset;
    void Start()
    {
        CopyCurrentsize = GetComponent<RectTransform>().sizeDelta;
        CopyColliderSize = GetComponent<BoxCollider2D>().size;
        CopyColliderOffset = GetComponent<BoxCollider2D>().offset;

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
                isMoving = true;
            }
            else
            {
                isMoving = false;
            }
            StartCoroutine(counter());
        }

    }

    void updateSize()
    {
        GetComponent<RectTransform>().sizeDelta = CopyCurrentsize;
        GetComponent<BoxCollider2D>().size = CopyColliderSize;
        GetComponent<BoxCollider2D>().offset = CopyColliderOffset;
    }

    void updateSize(GameObject obj)
    {
        GetComponent<RectTransform>().sizeDelta = obj.GetComponent<RectTransform>().sizeDelta;
        GetComponent<BoxCollider2D>().size = obj.GetComponent<BoxCollider2D>().size;
        GetComponent<BoxCollider2D>().offset = obj.GetComponent<BoxCollider2D>().offset;
    }

    IEnumerator counter()
    {
        yield return new WaitForSeconds(0.1f);
        isPressed = false;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if(other.gameObject.CompareTag("Item") && isMoving == false)
        {
            updateSize(other.gameObject);
            InventoryItem = Inventory.GetComponent<PlayerInventory>();
            InventoryItemObj = other.gameObject;
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if(isMoving == false || other == InventoryItemObj.GetComponent<Collider2D>())
        {
            GetComponent<RectTransform>().sizeDelta = CopyCurrentsize;
            GetComponent<BoxCollider2D>().size = CopyColliderSize;
            GetComponent<BoxCollider2D>().offset = CopyColliderOffset;

            InventoryItem = null;
            InventoryItemObj = null;
            isMoving = false;
            updateSize();
            gameObject.GetComponent<Image>().color = Color.green;   
        }
        
        
    }


}
