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

    bool isRotating = false;
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

    public void ResetCursor()
    {
        InventoryItem = null;
        InventoryItemObj = null;
        isMoving = false;
        updateSize();
        gameObject.GetComponent<Image>().color = Color.green;
    }

    void updateSize()
    {
        GetComponent<RectTransform>().sizeDelta = CopyCurrentsize;
        GetComponent<BoxCollider2D>().size = CopyColliderSize;
        GetComponent<BoxCollider2D>().offset = CopyColliderOffset;
    }

    void updateSize(GameObject obj)
    {
        float angle = (Mathf.Round(obj.GetComponent<RectTransform>().localEulerAngles.z / 90f) * 90f) % 360f;
        Vector2 newSize = obj.GetComponent<RectTransform>().sizeDelta;
        float margin = 10f;

        if(angle == 90 || angle == 270)
        {
            newSize = new Vector2(newSize.y, newSize.x);
        }

        GetComponent<RectTransform>().sizeDelta = newSize;
        GetComponent<BoxCollider2D>().size = new Vector2(newSize.x - margin * 2, newSize.y - margin * 2);
        GetComponent<BoxCollider2D>().offset = new Vector2((0.5f - GetComponent<RectTransform>().pivot.x) * newSize.x, (0.5f - GetComponent<RectTransform>().pivot.y) * newSize.y);
    }

    IEnumerator counter()
    {
        yield return new WaitForSeconds(0.1f);
        isPressed = false;
    }

    public void RotatedObject()
    {
        isRotating = true;
        updateSize(InventoryItemObj);
        StartCoroutine(rotateCounter());
    }

    IEnumerator rotateCounter()
    {
        yield return new WaitForSeconds(0.2f);
        isRotating = false;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if(other.gameObject.CompareTag("Item") && isMoving == false)
        {
            updateSize(other.gameObject);
            InventoryItem = Inventory.GetComponent<PlayerInventory>();
            InventoryItemObj = other.gameObject;
            Inventory.SnapCursorToItem(other.gameObject);
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if(isRotating == true)
        {
            return;
        }

        if(isMoving == false || (InventoryItemObj != null && other == InventoryItemObj.GetComponent<Collider2D>()))
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
