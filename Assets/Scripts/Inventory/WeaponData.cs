using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

[CreateAssetMenu(fileName = "WeaponData", menuName = "Scriptable Objects/Weapon")]
public class WeaponData : ScriptableObject
{
    public string Name;

    public GameObject model;
    public GameObject prefab;
    public Sprite UIsprite;

    public int weight;

   public List<int> requiredCells = new List<int>();

    //Stats que va a modificar
    public int health;
    public int MP;
    public int defense;
    
    public int WD;

    public int PD;

    public int HB;
    public int speed;
    
}
