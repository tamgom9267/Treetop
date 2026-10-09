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
        UIEquippedWeaponDisplay();
    
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void UIEquippedWeaponDisplay()
    {
       if (player.weapon != null)
        {
            Transform weaponSlotObj = transform.Find("InventoryBackground/WeaponSlot");
            GameObject WeaponSprite = new GameObject("WeaponSprite");
            
            WeaponSprite.transform.SetParent(weaponSlotObj, false);
            Image Image = WeaponSprite.AddComponent<Image>();
            Image.sprite = player.weapon.GetComponent<WeaponObject>().weaponData.UIsprite;
            
        }
        else
        {
            Destroy(WeaponSprite);
        } 
    }
}
