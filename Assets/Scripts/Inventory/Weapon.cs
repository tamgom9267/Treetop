using UnityEngine;
using UnityEngine.UI;

[CreateAssetMenu(fileName = "Weapon", menuName = "Scriptable Objects/Weapon")]
public class Weapon : ScriptableObject
{
    public string name;
    public GameObject model;
    public Texture2D UIsprite;

    public Vector2Int weight = Vector2Int.one;

    //Para asignar animaciones según el tipo de arma
    public Animator animator;
    
}
