using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using Unity.VisualScripting;
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

    private void OnDisable()
    {
        isPressed = false;
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
        if (InventoryItemObj  != null)
        {
            PanelManager panel = InventoryItemObj.GetComponent<PanelManager>();

            if (panel != null && panel.itemObj != null)
            {
                InventoryObject item = panel.itemObj.GetComponent<InventoryObject>();

                if (item != null && Inventory.SelectedObject == item) Inventory.SelectedObject = null;
            }
        } 
        
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
        Vector2 newColliderSize = obj.GetComponent<BoxCollider2D>().size;
        Vector2 itemOffset = obj.GetComponent<BoxCollider2D>().offset;
        Vector2 newOffset = itemOffset;

        if(angle == 90)
        {
            newOffset = new Vector2(-itemOffset.y, itemOffset.x);
        }
        else if(angle == 180)
        {
            newOffset = new Vector2(-itemOffset.x, -itemOffset.y);
        }
        else if(angle == 270)
        {
            newOffset = new Vector2(itemOffset.y, -itemOffset.x);
        }

        if(angle == 90 || angle == 270)
        {
            newSize = new Vector2(newSize.y, newSize.x);
            newColliderSize = new Vector2(newColliderSize.y, newColliderSize.x);
        }

        GetComponent<RectTransform>().sizeDelta = newSize;
        GetComponent<BoxCollider2D>().size = newColliderSize;
        GetComponent<BoxCollider2D>().offset = newOffset;
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
        // no cambiar de seleccion mientras se mueve
        if (isMoving) return;
        PanelManager panel = other.GetComponent<PanelManager>();
        if (panel == null) panel = other.GetComponentInParent<PanelManager>();
        if (panel == null || panel.itemObj == null) return;

        InventoryObject item = panel.itemObj.GetComponent<InventoryObject>();
        if (item == null) return;

        // Conservar las referencias que utiliza el sistema del cursor.
        InventoryItem = Inventory;
        InventoryItemObj = panel.gameObject;

        // Mantener el comportamiento del cursor.
        updateSize(panel.gameObject);
        Inventory.SnapCursorToItem(panel.gameObject);

        // Actualizar la selección y disparar el Observer.
        Inventory.SelectedObject = item;

        // Debug.Log("Objeto seleccionado: " + item.Name);

        // if(other.gameObject.CompareTag("Item") && isMoving == false)
        // {
        //     updateSize(other.gameObject);
        //     InventoryItem = Inventory.GetComponent<PlayerInventory>();
        //     InventoryItemObj = other.gameObject;
        //     Inventory.SnapCursorToItem(other.gameObject);
        // }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (isMoving || isRotating) return;

        PanelManager panel = other.GetComponent<PanelManager>();
        if (panel == null) panel = other.GetComponentInParent<PanelManager>();
        if (panel == null || panel.itemObj == null) return;

        InventoryObject item = panel.gameObject.GetComponent<InventoryObject>();
        if (item == null) return;

        // solo se quita la seleccion si todavia es el mismo objeto seleccionado
        if (Inventory.SelectedObject == item) Inventory.SelectedObject = null;

        if (InventoryItemObj == panel.gameObject)
        {
            InventoryItemObj = null;
            InventoryItem = null;
            updateSize();
        }

        // if(isMoving == false || (InventoryItemObj != null && other == InventoryItemObj.GetComponent<Collider2D>()))
        // {
        //     GetComponent<RectTransform>().sizeDelta = CopyCurrentsize;
        //     GetComponent<BoxCollider2D>().size = CopyColliderSize;
        //     GetComponent<BoxCollider2D>().offset = CopyColliderOffset;

        //     InventoryItem = null;
        //     InventoryItemObj = null;
        //     isMoving = false;
        //     updateSize();
        //     gameObject.GetComponent<Image>().color = Color.green;
        // }
    }

}
