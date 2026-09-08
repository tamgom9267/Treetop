using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class PlayerInventory : MonoBehaviour
{
    public List<InventoryObject> Inventory = new List<InventoryObject>();

    //Cuanto cabe en el inventario(basicamente grid**2)
    public int MaxWeight = 16;
    public int CurrentWeight = 0;

    [Header("Loot")]
    public InventoryObject LootObject;

    public int row = 4;
    public int column = 4;

    //Para generar el grid
    public int grid;

    //Para cuando Recogas un objeto
    public InventoryObject SelectedObject;
    public int lootWeight = 8;


    public void InspectLoot(InventoryObject worldObject)
    {
        LootObject = worldObject;
        LootObject.gameObject.SetActive(false); 

    }


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

    void UpdateSpace()
    {
        grid = row*column;
        MaxWeight = grid;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        UpdateSpace();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
