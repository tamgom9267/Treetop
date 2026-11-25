using System;
using UnityEngine;
using UnityEngine.UI;

public class HotbarDisplay : MonoBehaviour
{
    [Header("Jugador")]
    [SerializeField] private PlayerController player;
    [SerializeField] private PlayerClass playerClass;

    [Header("imagenes de Hotbar")]
    [SerializeField] private RawImage[] spellSlots = new RawImage[3];

    private Action<bool>[] cooldownCallbacks = new Action<bool>[3];
    private SpellObject[] subscribedSpells = new SpellObject[3];

    // private void Start()
    // {
    //     UpdateHotbar();
    // }

    public void UpdateHotbar()
    {
        // quitar suscripciones anteriores
        UnsubscribeSpells();

        // actualizar imagenes
        UpdateSpellSlot(0, player.Slot1);
        UpdateSpellSlot(1, player.Slot2);
        UpdateSpellSlot(2, player.Slot3);

        // suscribirse a nueva magia equipada
        SubscribeSpell(player.Slot1, 0);
        SubscribeSpell(player.Slot2, 1);
        SubscribeSpell(player.Slot3, 2);
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

        // el color para el cooldown
        // slot.color = Color.white;
        slot.gameObject.SetActive(true);
    }

    private void UpdateCooldown()
    {
        UpdateSlotCooldown(0, player.Slot1);
        UpdateSlotCooldown(1, player.Slot2);
        UpdateSlotCooldown(2, player.Slot3);
    }

    private void UpdateSlotCooldown(int index, InventoryObject spell)
    {
        RawImage slot = spellSlots[index];

        if (slot == null || spell == null) return;

        SpellObject spellObject = spell as SpellObject;

        if (spellObject == null) return;

        slot.color = spellObject.IsOnCooldown ? Color.gray : Color.white;
    } 
    private void OnEnable()
    {
        // observer para los cambios de mana
        if (playerClass != null) playerClass.OnStatsChanged += HandleStatsChanged;

        // observer para hotbar
        UpdateHotbar();
    }

    private void OnDisable()
    {
        if (playerClass != null) playerClass.OnStatsChanged -= HandleStatsChanged;
        UnsubscribeSpells();
    }

    // observer para los cambios de stats del jugador
    private void HandleStatsChanged(int health, int maxHealth, int currentMP, int maxMP)
    {
        UpdateAllSpellColors();
    }

    private void UpdateAllSpellColors()
    {
        for (int i = 0; i < subscribedSpells.Length; i++)
        {
            if (subscribedSpells[i] != null) UpdateSpellColor(i, subscribedSpells[i]);
        }
    }
    
    private void UpdateSpellColor(int index, SpellObject spell)
    {
        RawImage slot = spellSlots[index];

        if (slot == null || spell == null)
            return;

        bool onCooldown = spell.IsOnCooldown;
        bool noMana = playerClass.MP < spell.MP;

        slot.color = (onCooldown || noMana)
            ? Color.gray
            : Color.white;

        // Debug.Log(
        // $"Hechizo: {spell.name} | MP jugador: {playerClass.MP} | " +
        // $"Coste hechizo: {spell.MP} | Cooldown: {spell.IsOnCooldown}");
    }

    private void SubscribeSpell(InventoryObject spell, int index)
    {
        if (spell is not SpellObject spellObject) return;

        subscribedSpells[index] = spellObject;

        cooldownCallbacks[index] = (_) => UpdateSpellColor(index, spellObject);

        spellObject.OnCooldownChanged += cooldownCallbacks[index];

        UpdateSpellColor(index, spellObject);
    }

    private void UnsubscribeSpells()
    {
        for (int i = 0; i < subscribedSpells.Length; i++)
        {
            if (subscribedSpells[i] != null && cooldownCallbacks != null)
            {
                subscribedSpells[i].OnCooldownChanged -= cooldownCallbacks[i];
            }

            subscribedSpells[i] = null;
            cooldownCallbacks[i] = null;
        }
    }
}
