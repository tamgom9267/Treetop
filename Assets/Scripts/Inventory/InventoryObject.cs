using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class InventoryObject : MonoBehaviour
{

    public string name;

    //En caso de tener dos objetos con el mismo nombres
    public string ID;

    //Cuanto espacio ocupa
    public int weight;

    public float4x4 requieredCells;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
