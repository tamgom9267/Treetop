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

    PlayerController Player;

    public void AssignPlayer(PlayerController player)
    {
        Player = player;
    }

   public void PickUpWeapon()
    {
        CursorObject cursorObj = cursor.GetComponent<CursorObject>();
        List<List<int>> zone = (cursorObj.InventoryZone == 0) ? lootgrid : grid;
        int id = cursorObj.InventoryItemObj.GetComponent<PanelManager>().WeaponID;

        for (int y = 0; y < zone.Count; y++)
        {
            for (int x = 0; x < zone[y].Count; x++)
            {
                if (zone[y][x] == id)
                    zone[y][x] = 0;
            }
        }
    }

    public (int maxRow, int maxColumn, int minRow, int minColumn) GetShapeSize(GameObject itemObj)
    {
        int maxRow = 0;
        int maxColumn = 0;
        int minRow = 100;
        int minColumn = 100;

        for(int column = 0; column < itemObj.GetComponent<InventoryObject>().requieredCells.Count; column++)
        {
            for(int row = 0; row < itemObj.GetComponent<InventoryObject>().requieredCells[column].Count; row++)
            {
                if(itemObj.GetComponent<InventoryObject>().requieredCells[column][row] > 0)
                {
                    if(maxRow < row)
                    {
                        maxRow = row;
                    }
                    if(maxColumn < column)
                    {
                        maxColumn = column;
                    }
                    if(minRow > row)
                    {
                        minRow = row;
                    }
                    if(minColumn > column)
                    {
                        minColumn = column;
                    }
                }
            }
        }

        if(minRow == 100)
        {
            minRow = 0;
        }
        if(minColumn == 100)
        {
            minColumn = 0;
        }

        return (maxRow, maxColumn, minRow, minColumn);
    }

    public void PlaceCursor()
    {
        Transform Slots;

        if(cursor.GetComponent<CursorObject>().InventoryZone == 0)
        {
            Slots = ChestLoot.transform;
        }
        else
        {
            Slots = InventoryLoot.transform;
        }

        foreach(Transform panel in Slots)
        {
            if(panel.GetComponent<PanelManager>().location == cursor.GetComponent<CursorObject>().CurrentPosition)
            {
                RectTransform cursorRect = cursor.GetComponent<RectTransform>();
                cursorRect.transform.position = panel.transform.position;
            }
        }
    }

    public void SnapCursorToItem(GameObject itemPanel)
    {
        List<List<int>> DesignatedZone;

        if(cursor.GetComponent<CursorObject>().InventoryZone == 0)
        {
            DesignatedZone = lootgrid;
        }
        else
        {
            DesignatedZone = grid;
        }

        int foundRow = 100;
        int foundColumn = 100;

        for(int column = 0; column < DesignatedZone.Count; column++)
        {
            for(int row = 0; row < DesignatedZone[column].Count; row++)
            {
                if(DesignatedZone[column][row] == itemPanel.GetComponent<PanelManager>().WeaponID)
                {
                    if(foundRow > row)
                    {
                        foundRow = row;
                    }
                    if(foundColumn > column)
                    {
                        foundColumn = column;
                    }
                }
            }
        }

        if(foundRow == 100)
        {
            return;
        }

        (int maxRow, int maxColumn, int minRow, int minColumn) = GetShapeSize(itemPanel.GetComponent<PanelManager>().itemObj);

        cursor.GetComponent<CursorObject>().CurrentPosition = new Vector2(foundRow - minRow, foundColumn - minColumn);
        PlaceCursor();
    }

    #region LootWeapon
    public void InspectLoot(GameObject worldObject)
    {
        worldObject.SetActive(false);
        Loot.Add(worldObject);
        Createloot(worldObject);
    }

    public (List<Vector2> ObjImagePosition, bool canFit, int scaleheight, int scalewidth) CheckIfFit(GameObject imageObj)
    {
        CursorObject cursorObj = cursor.GetComponent<CursorObject>();
        List<List<int>> zone = (cursorObj.InventoryZone == 0) ? lootgrid : grid;

        var result = CheckFit(imageObj, zone, (int)cursorObj.CurrentPosition.x, (int)cursorObj.CurrentPosition.y);

        // Solo se marca rojo/verde si estás cargando un objeto.
        // Si solo estás pasando el cursor por encima, siempre verde.
        if (cursorObj.InventoryItemObj != null && isMoving)
            cursor.GetComponent<Image>().color = result.canFit ? Color.green : Color.red;
        else
            cursor.GetComponent<Image>().color = Color.green;

        return result;
    }

    public (List<Vector2> ObjImagePosition, bool canFit, int scaleheight, int scalewidth) CheckFit(GameObject imageObj, List<List<int>> zone, int offsetX, int offsetY)
    {
        List<Vector2> ObjImagePosition = new List<Vector2>();
        int scaleheight = 0;
        int scalewidth = 0;
        bool canFit = true;

        List<List<int>> cells = imageObj.GetComponent<InventoryObject>().requieredCells;

        for (int c = 0; c < cells.Count; c++)
        {
            for (int r = 0; r < cells[c].Count; r++)
            {
                if (cells[c][r] <= 0) continue;

                int gridY = c + offsetY;
                int gridX = r + offsetX;

                if (gridY < 0 || gridY >= zone.Count || gridX < 0 || gridX >= zone[gridY].Count)
                {
                    canFit = false;
                    continue;
                }

                if (zone[gridY][gridX] != 0)
                {
                    canFit = false;
                    continue;
                }

                scaleheight = Mathf.Max(scaleheight, c);
                scalewidth = Mathf.Max(scalewidth, r);
                ObjImagePosition.Add(new Vector2(r, c));
            }
        }

        return (ObjImagePosition, canFit, scaleheight, scalewidth);
    }
    
    public void Createloot(GameObject worldObject)
    {

        if (Inventory.Contains(worldObject))
        {
            Inventory.Remove(worldObject);
            Loot.Add(worldObject);
        }
        else
        {
            worldObject.GetComponent<InventoryObject>().ID = AvailableIDs[0];
            AvailableIDs.RemoveAt(0); 
        }
        
        
        (List<Vector2> ObjImagePosition, bool canFit, int scaleheight, int scalewidth) = CheckFit(worldObject, lootgrid, 0, 0);

        if (!canFit || ObjImagePosition.Count == 0)
        {
            Debug.LogWarning("Createloot: el objeto no cabe en el lootgrid (¿slot ocupado?)");
            return;
        }
        
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
        InvObjCollider.offset = new Vector2(InvObjCollider.size.x *0.66f  , InvObjCollider.size.y/-2 + (-5*scaleheight) );

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

            if (Player.weapon != null && Player.weapon == item)
            {
                Player.UnequipWeapon();
            }

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
        int shapeMaxRow = 0;
        int shapeMaxColumn = 0;

        if(direction == new Vector2(-1,0) && cursor.GetComponent<CursorObject>().InventoryZone == 1 && cursor.GetComponent<CursorObject>().CurrentPosition.x == 0 && isMoving)
        {
            ResetRotation();
        }

        if(cursor.GetComponent<CursorObject>().InventoryZone == 0)
        {
            currentGrid = lootgrid;
        }
        else
        {
            currentGrid = grid;
        }

        if(cursor.GetComponent<CursorObject>().InventoryItemObj != null)
        {
            (int maxRow, int maxColumn, int minRow, int minColumn) = GetShapeSize(cursor.GetComponent<CursorObject>().InventoryItemObj.GetComponent<PanelManager>().itemObj);
            shapeMaxRow = maxRow;
            shapeMaxColumn = maxColumn;
        }

        int limitRow = currentGrid[0].Count - 1 - shapeMaxRow;
        int limitColumn = currentGrid.Count - 1 - shapeMaxColumn;

        if(limitRow < 0)
        {
            limitRow = 0;
        }
        if(limitColumn < 0)
        {
            limitColumn = 0;
        }

        //Derecha Limite de loot
        if(direction == new Vector2(1,0) && cursor.GetComponent<CursorObject>().CurrentPosition.x >= limitRow && cursor.GetComponent<CursorObject>().InventoryZone == 0)
        {
            if(isMoving == false)
            {
                cursor.GetComponent<CursorObject>().ResetCursor();
                shapeMaxRow = 0;
                shapeMaxColumn = 0;
            }

            cursor.GetComponent<CursorObject>().InventoryZone = 1;
            cursor.GetComponent<CursorObject>().CurrentPosition.x = 0;

            if(cursor.GetComponent<CursorObject>().CurrentPosition.y > grid.Count - 1 - shapeMaxColumn)
            {
                cursor.GetComponent<CursorObject>().CurrentPosition.y = grid.Count - 1 - shapeMaxColumn;
            }
        }
        //Izquierda Limite de inventory
        else if (direction == new Vector2(-1,0) && cursor.GetComponent<CursorObject>().CurrentPosition.x == 0  && cursor.GetComponent<CursorObject>().InventoryZone == 1)
        {
            if(isMoving == false)
            {
                cursor.GetComponent<CursorObject>().ResetCursor();
                shapeMaxRow = 0;
                shapeMaxColumn = 0;
            }

            cursor.GetComponent<CursorObject>().InventoryZone = 0;
            cursor.GetComponent<CursorObject>().CurrentPosition.x = lootgrid[0].Count - 1 - shapeMaxRow;

            if(cursor.GetComponent<CursorObject>().CurrentPosition.x < 0)
            {
                cursor.GetComponent<CursorObject>().CurrentPosition.x = 0;
            }
            if(cursor.GetComponent<CursorObject>().CurrentPosition.y > lootgrid.Count - 1 - shapeMaxColumn)
            {
                cursor.GetComponent<CursorObject>().CurrentPosition.y = lootgrid.Count - 1 - shapeMaxColumn;
            }
        }
        //Derecha
        else if(direction == new Vector2(1,0) && cursor.GetComponent<CursorObject>().CurrentPosition.x < limitRow)
        {
            cursor.GetComponent<CursorObject>().CurrentPosition.x += 1;
        }
        //Izquierda
        else if(direction == new Vector2(-1,0) && cursor.GetComponent<CursorObject>().CurrentPosition.x > 0)
        {
            cursor.GetComponent<CursorObject>().CurrentPosition.x -= 1;
        }
        //Abajo
        else if(direction == new Vector2(0,-1) && cursor.GetComponent<CursorObject>().CurrentPosition.y < limitColumn)
        {
            cursor.GetComponent<CursorObject>().CurrentPosition.y += 1;
        }
        //Arriba
        else if(direction == new Vector2(0,1) && cursor.GetComponent<CursorObject>().CurrentPosition.y > 0)
        {
            cursor.GetComponent<CursorObject>().CurrentPosition.y -= 1;
        }

        PlaceCursor();

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
            PickUpWeapon();
        }
        else
        {
            if(cursor.GetComponent<CursorObject>().InventoryZone == 1 && AddObject(spritePanel))
            {
                if (SpriteObject != null)
                {
                    SpriteObject = null;
                }
                isMoving = false;
            }
            else if (cursor.GetComponent<CursorObject>().InventoryZone == 0 && RemoveObject(spritePanel.GetComponent<PanelManager>().WeaponID))
            {
                if (SpriteObject != null)
                {
                    SpriteObject = null;
                }
                isMoving = false;
                
            }
        }
        
    }

    #endregion


    #region Inventory
    public bool AddObject(GameObject spritePanel)
    {
        PanelManager panel = spritePanel.GetComponent<PanelManager>();
        var result = CheckIfFit(panel.itemObj);

        if (!result.canFit) return false;

        if (!Inventory.Contains(panel.itemObj))
        {
            Inventory.Add(panel.itemObj);
            Loot.Remove(panel.itemObj);
        }

        CursorObject cursorObj = cursor.GetComponent<CursorObject>();
        int offsetX = (int)cursorObj.CurrentPosition.x;
        int offsetY = (int)cursorObj.CurrentPosition.y;
        List<List<int>> cells = panel.itemObj.GetComponent<InventoryObject>().requieredCells;

        for (int c = 0; c < cells.Count; c++)
        {
            for (int r = 0; r < cells[c].Count; r++)
            {
                if (cells[c][r] == 1)
                    grid[offsetY + c][offsetX + r] = panel.WeaponID;
            }
        }

        Player.GetComponent<PlayerController>().SetNearestWeaponFalse();
        return true;
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



    public void RotateObject()
    {
        if(isMoving == false || cursor.GetComponent<CursorObject>().InventoryItemObj == null)
        {
            return;
        }

        GameObject itemObj = cursor.GetComponent<CursorObject>().InventoryItemObj.GetComponent<PanelManager>().itemObj;
        List<List<int>> cells = itemObj.GetComponent<InventoryObject>().requieredCells;

        int size = cells.Count;
        for(int column = 0; column < cells.Count; column++)
        {
            if(cells[column].Count > size)
            {
                size = cells[column].Count;
            }
        }

        while(cells.Count < size)
        {
            cells.Add(new List<int>());
        }

        for(int column = 0; column < size; column++)
        {
            while(cells[column].Count < size)
            {
                cells[column].Add(0);
            }
        }

        for(int layer = 0; layer < size / 2; layer++)
        {
            int first = layer;
            int last = size - 1 - layer;

            for(int i = first; i < last; i++)
            {
                int offset = i - first;
                int temp = cells[first][i];

                cells[first][i] = cells[last - offset][first];
                cells[last - offset][first] = cells[last][last - offset];
                cells[last][last - offset] = cells[i][last];
                cells[i][last] = temp;
            }
        }

        int minColumn = size;
        int minRow = size;

        for(int column = 0; column < size; column++)
        {
            for(int row = 0; row < size; row++)
            {
                if(cells[column][row] > 0)
                {
                    if(minColumn > column)
                    {
                        minColumn = column;
                    }
                    if(minRow > row)
                    {
                        minRow = row;
                    }
                }
            }
        }

        if(minColumn < size)
        {
            for(int column = 0; column < size; column++)
            {
                for(int row = 0; row < size; row++)
                {
                    if(column + minColumn < size && row + minRow < size)
                    {
                        cells[column][row] = cells[column + minColumn][row + minRow];
                    }
                    else
                    {
                        cells[column][row] = 0;
                    }
                }
            }
        }

        RectTransform rect = cursor.GetComponent<CursorObject>().InventoryItemObj.GetComponent<RectTransform>();
        Vector2 oldPivot = rect.pivot;
        float angle = Mathf.Round(rect.localEulerAngles.z / 90f) * 90f;
        angle = (angle - 90f + 360f) % 360f;
        rect.localRotation = Quaternion.Euler(0, 0, angle);

        if(angle == 0)
        {
            rect.pivot = new Vector2(0, 1);
        }
        else if(angle == 270)
        {
            rect.pivot = new Vector2(0, 0);
        }
        else if(angle == 180)
        {
            rect.pivot = new Vector2(1, 0);
        }
        else
        {
            rect.pivot = new Vector2(1, 1);
        }

        BoxCollider2D itemCollider = cursor.GetComponent<CursorObject>().InventoryItemObj.GetComponent<BoxCollider2D>();
        itemCollider.offset = new Vector2(
            itemCollider.offset.x + (oldPivot.x - rect.pivot.x) * rect.rect.width,
            itemCollider.offset.y + (oldPivot.y - rect.pivot.y) * rect.rect.height
        );

        cursor.GetComponent<CursorObject>().RotatedObject();

        List<List<int>> currentGrid;
        if(cursor.GetComponent<CursorObject>().InventoryZone == 0)
        {
            currentGrid = lootgrid;
        }
        else
        {
            currentGrid = grid;
        }

        (int maxRow, int maxColumn, int minR, int minC) = GetShapeSize(itemObj);

        if(cursor.GetComponent<CursorObject>().CurrentPosition.x > currentGrid[0].Count - 1 - maxRow)
        {
            cursor.GetComponent<CursorObject>().CurrentPosition.x = Mathf.Max(0, currentGrid[0].Count - 1 - maxRow);
        }
        if(cursor.GetComponent<CursorObject>().CurrentPosition.y > currentGrid.Count - 1 - maxColumn)
        {
            cursor.GetComponent<CursorObject>().CurrentPosition.y = Mathf.Max(0, currentGrid.Count - 1 - maxColumn);
        }

        PlaceCursor();
        CheckIfFit(itemObj);

        
    }

    public void ResetRotation()
    {
        if(cursor.GetComponent<CursorObject>().InventoryItemObj == null)
        {
            return;
        }

        RectTransform rect = cursor.GetComponent<CursorObject>().InventoryItemObj.GetComponent<RectTransform>();
        int turnsDone = (4 - Mathf.RoundToInt(rect.localEulerAngles.z / 90f) % 4) % 4;
        int missing = (4 - turnsDone) % 4;

        for(int i = 0; i < missing; i++)
        {
            RotateObject();
        }
    }

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
            grid.Add(new List<int>(row_array));
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
