using UnityEngine;
using UnityEngine.UI;

public class HotbarDisplay : MonoBehaviour
{
    [Header("Jugador")]
    [SerializeField] private PlayerController player;

    [Header("imagenes de Hotbar")]
    [SerializeField] private RawImage[] spellSlots = new RawImage[3];

    private void Start()
    {
        UpdateHotbar();
    }

    public void UpdateHotbar()
    {
        UpdateSpellSlot(0, player.Slot1);
        UpdateSpellSlot(1, player.Slot2);
        UpdateSpellSlot(2, player.Slot3);
    }

    private void UpdateSpellSlot(int index, InventoryObject spell)
    {
        RawImage slot = spellSlots[index];

        if (slot == null) return;

        if (spell == null)
        {
            slot.texture = null;
            slot.gameObject.SetActive(false);
            return;
        }

        Sprite icon = spell.GetUISprite();

        if (icon == null)
        {
            slot.gameObject.SetActive(false);
            return;
        }

        slot.texture = icon.texture;

        // para que la imagen tenga las dimensiones correctas
        slot.uvRect = new Rect(
            icon.rect.x / icon.texture.width,
            icon.rect.y / icon.texture.height,
            icon.rect.width / icon.texture.width,
            icon.rect.height / icon.texture.height
        );

        slot.gameObject.SetActive(true);
    }
}
