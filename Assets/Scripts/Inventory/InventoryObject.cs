using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class InventoryObject : MonoBehaviour
{

    public string Name;
    public string Objectname;

    //En caso de tener dos objetos con el mismo nombres
    public int ID;

    //Cuanto espacio ocupa
    public int weight;

    public List<List<int>> requieredCells = new List<List<int>>();

    public bool isSelected = false;

    public virtual Sprite GetUISprite()
    {
        return null;
    }

    protected void getRealList(List<int> cells)
    {
        for(int i = 0; i < cells.Count; i+=2)
        {
            requieredCells.Add(new List<int> {cells[i],cells[i+1]});
        }
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
