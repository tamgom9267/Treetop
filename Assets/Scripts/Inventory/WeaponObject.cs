using UnityEngine;

public class WeaponObject : InventoryObject
{
    [SerializeField]
    public PlayerClass player;

    [SerializeField]
    private Weapon weaponData;

    public int AttackStat;

    public string GetInteractText() => $"Player picked upñ{gameObject.name}";

    public Transform GetTransform() => transform;    


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        name = weaponData.name;

    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
