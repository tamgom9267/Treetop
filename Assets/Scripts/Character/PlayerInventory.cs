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

    [SerializeField] GameObject ObjectImagePanel;
    [SerializeField] GameObject cursorPrefab;
    [SerializeField] GameObject InventorySlot;
     //Gameobjects del canvas inventario
    [SerializeField] GameObject InventoryLoot;

    //Gameobjects del canvas chestloot inventario
    [SerializeField] GameObject ChestLoot;
    [SerializeField] GameObject ChestLootImages;
    
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
    public List<GameObject> Loot = new List<GameObject>();

    #region LootWeapon
    public void InspectLoot(GameObject worldObject)
    {
        worldObject.SetActive(false);
        Loot.Add(worldObject);
        Createloot(worldObject);
    }

    public (List<Vector2> ObjImagePosition, bool canFit,int scaleheight,int scalewidth) CheckIfFit(GameObject imageObj)
    {
        List<Vector2> ObjImagePosition = new List<Vector2>();
        int scaleheight = 0;
        int scalewidth = 0;
        //Checar si se puede poner en el grid
        bool canFit = true;
        int Zone = cursor.GetComponent<CursorObject>().InventoryZone;
        List<List<int>> DesignatedZone;

        if(Zone == 0)
        {
            DesignatedZone = lootgrid;
        }
        else
        {
            DesignatedZone = grid;
        }

        for(int column = 0; column < DesignatedZone.Count; column++)
        {
            for(int row = 0; row < imageObj.GetComponent<InventoryObject>().requieredCells[column].Count; row++)
            {
                if(DesignatedZone.Count-1 + (int)cursor.GetComponent<CursorObject>().CurrentPosition.y > DesignatedZone.Count-1)
                {
                    canFit = false;
                }
                else if(imageObj.GetComponent<InventoryObject>().requieredCells[column][row] > 0 || imageObj.GetComponent<InventoryObject>().requieredCells[column][row] == 0 && DesignatedZone[column][row] == 0)
                {
                    if(imageObj.GetComponent<InventoryObject>().requieredCells[column][row] > 0)
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
                else if(imageObj.GetComponent<InventoryObject>().requieredCells[column][row] == 0 && DesignatedZone[column][row] > 0)
                {
                    continue;
                }
                else
                {
                    canFit = false;
                }
            }

        }


        if(cursor.GetComponent<CursorObject>().InventoryItemObj != null)
        {
            if(canFit == false)
            {
                cursor.GetComponent<Image>().color = Color.red;
            }
            else
            {
                cursor.GetComponent<Image>().color = Color.green;
            }
        }
        else
        {
            cursor.GetComponent<Image>().color = Color.green;
        }

        return (ObjImagePosition, canFit, scaleheight, scalewidth);
    }
    
    public void Createloot(GameObject worldObject)
    {
        worldObject.GetComponent<InventoryObject>().ID = AvailableIDs[0];
        AvailableIDs.RemoveAt(0);
        
        (List<Vector2> ObjImagePosition, bool canFit, int scaleheight, int scalewidth) = CheckIfFit(worldObject);
        
        //Agregar el objecto al chestloot grid
        if(canFit)
        {
            for(int column = 0; column < lootgrid.Count; column++)
            {
                for(int row = 0; row < lootgrid[column].Count; row++)
                {
                    if(worldObject.GetComponent<WeaponObject>().requieredCells[column][row] == 1 && lootgrid[column][row] == 0)
                    {
                        lootgrid[column][row] = worldObject.GetComponent<WeaponObject>().ID;
                    }
                }
            }
        }

        //Reflejar el cambio en la UI
        GameObject InvObj = Instantiate(ObjectImagePanel, ChestLootImages.transform);
       
        InvObj.GetComponent<PanelManager>().WeaponID = worldObject.GetComponent<InventoryObject>().ID;
        InvObj.GetComponent<PanelManager>().itemObj = worldObject;

        BoxCollider2D InvObjCollider = InvObj.GetComponent<BoxCollider2D>();
        InvObjCollider.size = new Vector2(120 * (scalewidth+1), 150*(scaleheight+1));
        InvObjCollider.offset = new Vector2(InvObjCollider.size.x/2 + InvObjCollider.size.x/4, InvObjCollider.size.y/-2 -  InvObjCollider.size.y/-4);

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

    
    public void RemoveWeaponFromChestLootGrid(Vector3 position)
    {
        List<GameObject> itemsToDrop = new List<GameObject>(Loot);

        foreach (GameObject item in itemsToDrop)
        {
            if (item == null) continue;
            InventoryObject invObj = item.GetComponent<InventoryObject>();
            if (invObj == null) continue;
            int itemID = invObj.ID;

            for (int column = 0; column < lootgrid.Count; column++)
            {
                for (int row = 0; row < lootgrid[column].Count; row++)
                {
                    if (lootgrid[column][row] == itemID)
                    {
                        lootgrid[column][row] = 0;
                    }
                }
            }

            item.SetActive(true);
            item.transform.position = position; 

            foreach (Transform child in ChestLootImages.transform)
            {
                PanelManager panel = child.GetComponent<PanelManager>();
                if (panel != null && panel.WeaponID == itemID)
                {
                    Destroy(child.gameObject);
                }
            }

            if (!AvailableIDs.Contains(itemID))
            {
                AvailableIDs.Add(itemID);
            }
            invObj.ID = 0;
        }

        Loot.Clear();
    }

    public void MovingCursor(Vector2 direction)
    {
        List<List<int>> currentGrid;
        if(cursor.GetComponent<CursorObject>().InventoryZone == 0){
            currentGrid = lootgrid;
        }
        else
        {
            currentGrid = grid;
        }

        //Derecha Limite de loot
        if(direction == new Vector2(1,0) && cursor.GetComponent<CursorObject>().CurrentPosition.x == currentGrid[0].Count-1 && cursor.GetComponent<CursorObject>().InventoryZone == 0)
        {
            cursor.GetComponent<CursorObject>().InventoryZone = 1;
            cursor.GetComponent<CursorObject>().CurrentPosition.x = 0;
        }
        //Izquierda Limite de inventory
        else if (direction == new Vector2(-1,0) && cursor.GetComponent<CursorObject>().CurrentPosition.x == 0  && cursor.GetComponent<CursorObject>().InventoryZone == 1)
        {
            cursor.GetComponent<CursorObject>().InventoryZone = 0;
            cursor.GetComponent<CursorObject>().CurrentPosition.x = lootgrid[0].Count-1;
        }
        //Derecha
        else if(direction == new Vector2(1,0) && cursor.GetComponent<CursorObject>().CurrentPosition.x < currentGrid[0].Count-1)
        {
            cursor.GetComponent<CursorObject>().CurrentPosition.x += 1;
        }
        //Izquierda
        else if(direction == new Vector2(-1,0) && cursor.GetComponent<CursorObject>().CurrentPosition.x > 0)
        {
            cursor.GetComponent<CursorObject>().CurrentPosition.x -= 1;
        }
        //Abajo
        else if(direction == new Vector2(0,-1) && cursor.GetComponent<CursorObject>().CurrentPosition.y < currentGrid.Count-1)
        {
            cursor.GetComponent<CursorObject>().CurrentPosition.y += 1;
        }
        //Arriba
        else if(direction == new Vector2(0,1) && cursor.GetComponent<CursorObject>().CurrentPosition.y > 0)
        {
            cursor.GetComponent<CursorObject>().CurrentPosition.y -= 1;
        }        



        if (cursor.GetComponent<CursorObject>().InventoryZone == 0)
        {
           foreach(Transform panel in ChestLoot.transform)
            {
                if(panel.GetComponent<PanelManager>().location == cursor.GetComponent<CursorObject>().CurrentPosition)
                {
                    
                    RectTransform cursorRect = cursor.GetComponent<RectTransform>();
                    cursorRect.transform.position = panel.transform.position;
                }
            } 
        }
        else{
            foreach(Transform panel in InventoryLoot.transform)
            {

                if(panel.GetComponent<PanelManager>().location == cursor.GetComponent<CursorObject>().CurrentPosition)
                {
                    RectTransform cursorRect = cursor.GetComponent<RectTransform>();
                    cursorRect.transform.position = panel.transform.position;
                }
            } 
        }

        if (cursor.GetComponent<CursorObject>().InventoryItemObj != null)
        {
            CheckIfFit(cursor.GetComponent<CursorObject>().InventoryItemObj.GetComponent<PanelManager>().itemObj);    
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
            if(cursor.GetComponent<CursorObject>().InventoryZone == 1 && AddObject(spritePanel))
            {
                isMoving = false;
                SpriteObject = null;
            }
            else if (cursor.GetComponent<CursorObject>().InventoryZone == 0 && RemoveObject(spritePanel.GetComponent<PanelManager>().WeaponID))
            {
                isMoving = false;
                SpriteObject = null;
            }
        }
        
    }

    #endregion


    #region Inventory
    public bool AddObject(GameObject spritePanel)
    {
        (List<Vector2> ObjImagePosition, bool canFit, int scaleheight, int scalewidth) = CheckIfFit(spritePanel.GetComponent<PanelManager>().itemObj);
        
        int itemRow = 0;
        int itemColumn = 0;

        //Agregar el objecto al Inventory grid
        if(canFit)
        {
            if(Inventory.Contains(spritePanel.GetComponent<PanelManager>().itemObj) == false)
            {
                Inventory.Add(spritePanel.GetComponent<PanelManager>().itemObj);
                Loot.Remove(spritePanel.GetComponent<PanelManager>().itemObj);
            }
            
            for(int column = (int)cursor.GetComponent<CursorObject>().CurrentPosition.y; column < grid.Count; column++)
            {
                for(int row = (int)cursor.GetComponent<CursorObject>().CurrentPosition.x; row < grid[column].Count; row++)
                {
                    if(spritePanel.GetComponent<PanelManager>().itemObj.GetComponent<InventoryObject>().requieredCells[itemColumn][itemRow] == 1 && grid[column][row] == 0)
                    {
                        grid[column][row] = spritePanel.GetComponent<PanelManager>().WeaponID;
                        itemRow ++;
                        if(itemRow > spritePanel.GetComponent<PanelManager>().itemObj.GetComponent<InventoryObject>().requieredCells[itemColumn].Count - 1)
                        {
                            itemRow = 0;
                            itemColumn ++;
                        }
                    }
                }
            }

            
            return true;
        }
        return false;

    }

    public bool RemoveObject(int ID)
    {
        for(int column = 0; column < lootgrid.Count; column++)
        {
            for(int row = 0; row < lootgrid[column].Count; row++)
            {
                if (lootgrid[column][row] == ID)
                {
                    lootgrid[column][row] = 0;
                }
            }
        }

        GameObject targetPanel = null;
        foreach(Transform child in ChestLootImages.transform)
        {
            PanelManager panelManager = child.GetComponent<PanelManager>();
            if(panelManager != null && panelManager.WeaponID == ID)
            {
                targetPanel = child.gameObject;
                break;
            }
        }

        if (targetPanel != null)
        {
            PanelManager targetPanelManager = targetPanel.GetComponent<PanelManager>();
            
            if (targetPanelManager.itemObj != null && !Loot.Contains(targetPanelManager.itemObj))
            {
                Loot.Add(targetPanelManager.itemObj);
                Inventory.Remove(targetPanelManager.itemObj);
            }

            return true;
        }

        return false;
    }

    #endregion

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        cursor = Instantiate(cursorPrefab, this.transform.GetChild(0));
        cursor.GetComponent<CursorObject>().Inventory = this;
        RectTransform cursorRect = cursor.GetComponent<RectTransform>();
        
        //Hacer los available ID's en base a la cantidad maxima de objetos que un jugador podria tener
        for(int i = 1; i < MaxWeight + lootWeight; i++)
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

        row_counter = 0;
        column_counter = 0;
        for(int i = 0; i < MaxWeight; i++)
        {
            GameObject panelObj = Instantiate(InventorySlot, InventoryLoot.transform);   
            panelObj.GetComponent<PanelManager>().location = new Vector2Int(row_counter, column_counter);

            row_counter ++;
            if(row_counter >= row)
            {
              row_counter = 0;
              column_counter += 1;  
            } 
        }

        //Crear el inventario inicial por codigo
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
        if (isMoving && SpriteObject != null)
        {
            SpriteObject.transform.position = cursor.transform.position;
        }
    }


}
