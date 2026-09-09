using UnityEngine;
using UnityEngine.UI;

public class UIDisplay : MonoBehaviour
{

    [SerializeField]
    public PlayerController player;
    private GameObject WeaponSprite;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Transform weaponSlotObj = transform.Find("InventoryBackground/WeaponSlot");
        GameObject WeaponSprite = new GameObject("WeaponSprite");
        
        WeaponSprite.transform.SetParent(weaponSlotObj, false);
        RawImage Image = WeaponSprite.AddComponent<RawImage>();
        Image.texture = player.weapon.UIsprite;
    
    }

    // Update is called once per frame
    void Update()
    {
        if (player.weapon != null)
        {
            Transform weaponSlotObj = transform.Find("InventoryBackground/WeaponSlot");
            GameObject WeaponSprite = new GameObject("WeaponSprite");
            
            WeaponSprite.transform.SetParent(weaponSlotObj, false);
            RawImage Image = WeaponSprite.AddComponent<RawImage>();
            Image.texture = player.weapon.UIsprite;
        }
        else
        {
            Destroy(WeaponSprite);
        }
        
    }
}
