using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class PlayerInventory : MonoBehaviour
{
    public List<InventoryObject> Inventory;

    //Cuanto cabe en el inventario(basicamente grid**2)
    public int MaxWeight;

    public int CurrentWeight = 0;

    public int row = 4;
    public int column = 4;

    //Para generar el grid
    public int grid;

    //Para cuando Recogas un objeto
    public InventoryObject loot;
    public int lootWeight = 8;

    void AddObject(InventoryObject NewObject)
    {
        if(CurrentWeight + NewObject.weight <= MaxWeight)
        {
            Inventory.Add(NewObject);
            CurrentWeight += NewObject.weight;
        }

    }

    void DiscartObject(InventoryObject CurrentObject)
    {
        Inventory.Remove(CurrentObject);
        CurrentWeight -= CurrentObject.weight;
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
