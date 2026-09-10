using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[CreateAssetMenu(fileName = "WeaponData", menuName = "Scriptable Objects/Weapon")]
public class WeaponData : ScriptableObject
{
    public string name;

    public GameObject model;
    public GameObject prefab;
    public Texture2D UIsprite;

    public int weight;

    public List<List<int>> requiredCells;

    //Stats que va a modificar
    public int health;
    public int MP;
    public int defense;
    
    public int AD;

    public int AP;

    public int HB;
    public int speed;
    
}
