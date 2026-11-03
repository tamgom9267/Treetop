using System.Collections;
using UnityEngine;

public abstract class SpellObject : InventoryObject
{
    [SerializeField]
    public ObjectData spellData;

    public int health;
    public int MP;
    public int defense;

    public int WD;

    public int PD;

    public int HB;
    public int speed;

    public abstract void SpellEffect(PlayerController player);

    public virtual IEnumerator EndEffect(float num, PlayerClass playerClass)
    {
        yield return new WaitForSeconds(num);

    }

    public override Sprite GetUISprite()
    {
        return spellData.UIsprite;
    }

    void Awake()
    {
        Name = spellData.Name;
        health = spellData.health;
        MP = spellData.MP;
        defense = spellData.defense;
        WD = spellData.WD;
        PD = spellData.PD;
        HB = spellData.HB;
        speed = spellData.speed;
        getRealList(spellData.requiredCells);
    }

    void Update()
    {
        if(transform.childCount > 0)
        {
            transform.GetChild(0).gameObject.SetActive(isSelected);
        }
    }
}
