using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;


public class PlayerInventory : MonoBehaviour
{
    public List<InventoryObject> Inventory = new List<InventoryObject>();
    public List<int> AvailableIDs = new List<int>();

    //Cuanto cabe en el inventario(basicamente grid**2)
    public int MaxWeight = 16;
    public int CurrentWeight = 0;

    [SerializeField] GameObject InventorySlot;
    [SerializeField] GameObject ChestLoot;
    [SerializeField] GameObject ChestLootImages;
    [SerializeField] GameObject cursorPrefab;

    public GameObject cursor;
    public int cursorZone = 0;


    [Header("Loot")]
    public InventoryObject LootObject;

    public int row = 4;
    public int column = 4;

    //El espacio del inventario con los espacios integrados
    public List<List<int>> grid = new List<List<int>>();

    //Para cuando Recogas un objeto
    public InventoryObject SelectedObject;
    public int lootWeight = 8;
    public List<List<int>> lootgrid = new List<List<int>>();

    #region LootWeapon
    public void InspectLoot(InventoryObject worldObject)
    {
        LootObject = worldObject;
        LootObject.gameObject.SetActive(false);
        addToLoot(worldObject);
    }

    public void addToLoot(InventoryObject worldObject)
    {
        List<Vector2> ObjImagePosition = new List<Vector2>(); 
        int scaleheight = 0;
        int scalewidth = 0;
        //Checar si se puede poner en el chestloot grid
        bool canFit = true;
        for(int column = 0; column < lootgrid.Count; column++)
        {
            for(int row = 0; row < lootgrid[column].Count; row++)
            {
                if(LootObject.GetComponent<WeaponObject>().requieredCells[column][row] == 1 || LootObject.GetComponent<WeaponObject>().requieredCells[column][row] == 0 && lootgrid[column][row] == 0)
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

        //Agregar el objecto al chestloot grid
        if (canFit)
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
        GameObject InvObj = Instantiate(InventorySlot, ChestLootImages.transform);
        foreach(Transform obj in ChestLoot.transform)
        {
            if(obj.GetComponent<PanelManager>().location == ObjImagePosition[0])
            {
                RectTransform rect = obj.GetComponent<RectTransform>();
                RectTransform rectInv = InvObj.GetComponent<RectTransform>();
                rectInv.anchoredPosition = rect.anchoredPosition;
            }
        }
      
        if(worldObject is WeaponObject weapon)
        {
            InvObj.GetComponent<Image>().sprite = weapon.weaponData.UIsprite;    
            RectTransform rect = InvObj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(rect.sizeDelta.x *(scalewidth+1-ObjImagePosition[0].x), rect.sizeDelta.y * (scaleheight+1 - -ObjImagePosition[0].y));
        }
        
        
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
    }

    public void MovingCursor(InputActionReference direction)
    {
        if(direction)
        {
            
        }
    }

    public void isMovingObject()
    {
        
    }

    #endregion


    public void AddObject(InventoryObject NewObject)
    {
        if(CurrentWeight + NewObject.weight <= MaxWeight)
        {
            Inventory.Add(NewObject);
            CurrentWeight += NewObject.weight;
            NewObject.gameObject.SetActive(false);

            LootObject = null; 
        }

    }

    public void DiscartObject(InventoryObject SelectedObject)
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
            CurrentWeight -= SelectedObject.weight;
        }

        
    }


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        cursor = Instantiate(cursorPrefab, this.transform);
        RectTransform cursorRect = cursor.GetComponent<RectTransform>();
        

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

        cursorRect.anchoredPosition = ChestLoot.transform.GetChild(0).position;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
