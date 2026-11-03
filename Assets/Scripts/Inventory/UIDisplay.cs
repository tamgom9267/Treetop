using UnityEngine;
using UnityEngine.UI;

public class UIDisplay : MonoBehaviour
{

    [SerializeField]
    public PlayerController player;
    private GameObject WeaponSprite;

    private GameObject SpellSprite1;
    private GameObject SpellSprite2;
    private GameObject SpellSprite3;

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
            WeaponSprite = new GameObject("WeaponSprite");
            
            WeaponSprite.transform.SetParent(weaponSlotObj, false);
            Image Image = WeaponSprite.AddComponent<Image>();
            Image.sprite = player.weapon.GetComponent<WeaponObject>().weaponData.UIsprite;
            
        }
        else
        {
            if (WeaponSprite)
            {
                Destroy(WeaponSprite);
            }
        } 
    }

    public void UIEquippedSpellsDisplay()
    {
        SpellSprite1 = UpdateSpellSlot(SpellSprite1, GetComponent<PlayerInventory>().UISlot1, player.Slot1);
        SpellSprite2 = UpdateSpellSlot(SpellSprite2, GetComponent<PlayerInventory>().UISlot2, player.Slot2);
        SpellSprite3 = UpdateSpellSlot(SpellSprite3, GetComponent<PlayerInventory>().UISlot3, player.Slot3);
    }

    GameObject UpdateSpellSlot(GameObject spriteObj, GameObject slotObj, InventoryObject spell)
    {
        if (spriteObj != null)
        {
            Destroy(spriteObj);
        }

        if (spell == null || slotObj == null)
        {
            return null;
        }

        GameObject newSprite = new GameObject("SpellSprite");
        newSprite.transform.SetParent(slotObj.transform, false);

        Image image = newSprite.AddComponent<Image>();
        image.sprite = spell.GetUISprite();
        image.preserveAspect = true;

        RectTransform rect = newSprite.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        return newSprite;
    }
}
