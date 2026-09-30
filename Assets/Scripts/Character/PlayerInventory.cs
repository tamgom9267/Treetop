using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;


public class PlayerInventory : MonoBehaviour
{
    
    //El espacio del inventario con los espacios integrados
    public List<List<int>> grid = new List<List<int>>();
    public List<GameObject> Inventory = new List<GameObject>();
    public List<int> AvailableIDs = new List<int>();

    //Cuanto cabe en el inventario(basicamente grid**2)
    public int MaxWeight = 16;
    public int CurrentWeight = 0;

    [SerializeField] GameObject InventorySlot;
    [SerializeField] GameObject ObjectImagePanel;
    [SerializeField] GameObject ChestLoot;
    [SerializeField] GameObject ChestLootImages;
    [SerializeField] GameObject cursorPrefab;


    bool isMoving = false;
    GameObject SpriteObject;
    public GameObject cursor;

    [Header("Loot")]
    public InventoryObject LootObject;

    public int row = 4;
    public int column = 4;

    //Para cuando Recogas un objeto
    public InventoryObject SelectedObject;
    public int lootWeight = 8;
    public List<List<int>> lootgrid = new List<List<int>>();
    public List<InventoryObject> Loot = new List<InventoryObject>();




    #region LootWeapon
    public void InspectLoot(GameObject worldObject)
    {
        LootObject = worldObject.GetComponent<InventoryObject>();
        LootObject.gameObject.SetActive(false);
        Loot.Add(LootObject);
        Createloot(worldObject);
    }

    public (List<Vector2> ObjImagePosition, bool canFit,int scaleheight,int scalewidth) CheckIfFit()
    {
        List<Vector2> ObjImagePosition = new List<Vector2>();
        int scaleheight = 0;
        int scalewidth = 0;
        //Checar si se puede poner en el chestloot grid
        bool canFit = true;
        for(int column = 0; column < lootgrid.Count; column++)
        {
            for(int row = 0; row < LootObject.GetComponent<WeaponObject>().requieredCells[column].Count; row++)
            {
                if(lootgrid.Count-1 + (int)cursor.GetComponent<CursorObject>().CurrentPosition.y > lootgrid.Count-1)
                {
                    canFit = false;
                }
                else if(LootObject.GetComponent<WeaponObject>().requieredCells[column][row] == 1 || LootObject.GetComponent<WeaponObject>().requieredCells[column][row] == 0 && lootgrid[column][row] == 0)
                {
                    if(LootObject.GetComponent<WeaponObject>().requieredCells[column][row] == 1)
                    {
                        if(scaleheight < column)
                        {
                            scaleheight = column;
                        }
                        if(scalewidth < row)
                        {
                            scalewidth = row;
                        }
                        ObjImagePosition.Add(new Vector2(row,column));   
                    }
                    continue;
                }
                else if(LootObject.GetComponent<WeaponObject>().requieredCells[column][row] == 0 && lootgrid[column][row] == 1)
                {
                    continue;
                }
                else
                {
                    canFit = false;
                }
            }
        }

        return (ObjImagePosition, canFit, scaleheight, scalewidth);
    }
    
    public void Createloot(GameObject worldObject)
    {
        
        (List<Vector2> ObjImagePosition, bool canFit, int scaleheight, int scalewidth) = CheckIfFit();

        //Agregar el objecto al chestloot grid
        if(canFit)
        {
            for(int column = 0; column < lootgrid.Count; column++)
            {
                for(int row = 0; row < lootgrid[column].Count; row++)
                {
                    if(LootObject.GetComponent<WeaponObject>().requieredCells[column][row] == 1 && lootgrid[column][row] == 0)
                    {
                        lootgrid[column][row] = 1;
                    }
                }
            }
        }

        //Reflejar el cambio en la UI
        GameObject InvObj = Instantiate(ObjectImagePanel, ChestLootImages.transform);
        worldObject.GetComponent<InventoryObject>().ID = AvailableIDs[0];
        AvailableIDs.Remove(0);
        InvObj.GetComponent<PanelManager>().WeaponID = worldObject.GetComponent<InventoryObject>().ID;
        InvObj.GetComponent<PanelManager>().itemObj = worldObject;

        BoxCollider2D InvObjCollider = InvObj.GetComponent<BoxCollider2D>();
        InvObjCollider.size = new Vector2(160 * (scalewidth+1), 160*(scaleheight+1));
        InvObjCollider.offset = new Vector2(InvObjCollider.size.x/2, InvObjCollider.size.y/-2);
        Debug.Log((scalewidth, scaleheight));

        foreach(Transform obj in ChestLoot.transform)
        {
            if(obj.GetComponent<PanelManager>().location == ObjImagePosition[0])
            {
                RectTransform rect = obj.GetComponent<RectTransform>();
                RectTransform rectInv = InvObj.GetComponent<RectTransform>();
                rectInv.anchoredPosition = rect.anchoredPosition;
            }
        }
      
        if(worldObject.CompareTag("Weapon"))
        {
            InvObj.GetComponent<Image>().sprite = worldObject.GetComponent<WeaponObject>().weaponData.UIsprite;    
            RectTransform rect = InvObj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(rect.sizeDelta.x *(scalewidth+1-ObjImagePosition[0].x), rect.sizeDelta.y * (scaleheight+1 - -ObjImagePosition[0].y));
        }
    }

    public bool addToLoot(GameObject spritePanel)
    {
        (List<Vector2> ObjImagePosition, bool canFit, int scaleheight, int scalewidth) = CheckIfFit();

        //Agregar el objecto al chestloot grid
        if(canFit)
        {
            for(int column = (int)cursor.GetComponent<CursorObject>().CurrentPosition.y; column < lootgrid.Count; column++)
            {
                for(int row = (int)cursor.GetComponent<CursorObject>().CurrentPosition.x; row < lootgrid[column].Count; row++)
                {
                    if(spritePanel.GetComponent<PanelManager>().itemObj.GetComponent<InventoryObject>().requieredCells[column][row] == 1 && lootgrid[column][row] == 0)
                    {
                        lootgrid[column][row] = 1;
                    }
                }
            }
            return true;
        }
        return false;
    }

    public void ResetChestLootGrid()
    {
        //Poner la matriz de Chestloot en 0s
        lootgrid = new List<List<int>>();
        for(int i = 0; i < 4; i++)
        {
            lootgrid.Add(new List<int> {0,0});
        }

        //Quitar la imagen de ChestLootImage
        foreach(Transform child in ChestLootImages.transform)
        {
            Destroy(child.gameObject);
        }

        //Quitarles el ID al los objetos descartados y hacer los IDs disponibles otra vez
        foreach(InventoryObject item in Loot)
        {
            AvailableIDs.Add(item.GetComponent<InventoryObject>().ID);
            item.GetComponent<InventoryObject>().ID = 0;
        }
        Loot = new List<InventoryObject>();
    }

    public void MovingCursor(Vector2 direction)
    {
        //Derecha
        if(direction == new Vector2(1,0) && cursor.GetComponent<CursorObject>().CurrentPosition.x < lootgrid[0].Count-1)
        {
            cursor.GetComponent<CursorObject>().CurrentPosition.x += 1;
        }
        //Izquierda
        else if(direction == new Vector2(-1,0) && cursor.GetComponent<CursorObject>().CurrentPosition.x > 0)
        {
            cursor.GetComponent<CursorObject>().CurrentPosition.x -= 1;
        }
        //Abajo
        else if(direction == new Vector2(0,-1) && cursor.GetComponent<CursorObject>().CurrentPosition.y < lootgrid.Count-1)
        {
            cursor.GetComponent<CursorObject>().CurrentPosition.y += 1;
        }
        //Arriba
        else if(direction == new Vector2(0,1) && cursor.GetComponent<CursorObject>().CurrentPosition.y > 0)
        {
            cursor.GetComponent<CursorObject>().CurrentPosition.y -= 1;
        }


        foreach(Transform panel in ChestLoot.transform)
        {
            if(panel.GetComponent<PanelManager>().location == cursor.GetComponent<CursorObject>().CurrentPosition)
            {
                RectTransform cursorRect = cursor.GetComponent<RectTransform>();
                cursorRect.transform.position = panel.transform.position;
            }
        }
    }

    public void isMovingObject(GameObject spritePanel)
    {
        if (isMoving == false)
        {
            isMoving = true;
            SpriteObject = spritePanel;
        }
        else
        {
            if (addToLoot(spritePanel))
            {
                isMoving = false;
                SpriteObject = null;
            }
        }
        
    }

    #endregion


    public void AddObject(GameObject NewObject)
    {
        if(CurrentWeight + NewObject.GetComponent<InventoryObject>().weight <= MaxWeight)
        {
            Inventory.Add(NewObject);
            CurrentWeight += NewObject.GetComponent<InventoryObject>().weight;
            NewObject.gameObject.SetActive(false);

            LootObject = null; 
        }

    }

    public void DiscartObject(GameObject SelectedObject)
    {
        Vector3 dropPosition = transform.position + transform.forward * 1.5f + Vector3.up * 0.5f;

        if(SelectedObject == LootObject)
        {
            LootObject.transform.position = dropPosition;
            LootObject.transform.rotation = Quaternion.identity;
            LootObject.gameObject.SetActive(true);
            LootObject = null;
        }
        else
        {
            SelectedObject.transform.position = dropPosition;
            SelectedObject.transform.rotation = Quaternion.identity;
            SelectedObject.gameObject.SetActive(true);

            Inventory.Remove(SelectedObject);
            CurrentWeight -= SelectedObject.GetComponent<InventoryObject>().weight;
        }

        
    }


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        cursor = Instantiate(cursorPrefab, this.transform);
        cursor.GetComponent<CursorObject>().Inventory = this;
        RectTransform cursorRect = cursor.GetComponent<RectTransform>();
        
        //Hacer los available ID's en base a la cantidad maxima de objetos que un jugador podria tener
        for(int i = 0; i < MaxWeight + lootWeight; i++)
        {
            AvailableIDs.Add(i);
        }

        //Hacer que cada panel de inventorySlot tenga su propia coordenada(Esto es unicamente para el Chestloot)
        int row_counter = 0;
        int column_counter = 0;
        for(int i = 0; i < lootWeight; i++)
        {
            GameObject panelObj = Instantiate(InventorySlot, ChestLoot.transform);   
            panelObj.GetComponent<PanelManager>().location = new Vector2Int(row_counter, column_counter);

            row_counter ++;
            if(row_counter >= 2)
            {
              row_counter = 0;
              column_counter += 1;  
            } 
        }

        //Crear el inventario inicial
        MaxWeight = row*column;
        List<int> row_array = new List<int>();

        for(int i = 0; i<row; i++)
        {
            row_array.Add(0);
        }

        for (int i = 0; i < column; i++)
        {
            grid.Add(row_array);
        }

        for(int i = 0; i < 4; i++)
        {
            lootgrid.Add(new List<int> {0,0});
        }

        gameObject.SetActive(false);

        cursorRect.transform.position = ChestLoot.transform.GetChild(0).position;
    }

    // Update is called once per frame
    void Update()
    {
        if (isMoving)
        {
            SpriteObject.transform.position = cursor.transform.position;
        }
    }


}
