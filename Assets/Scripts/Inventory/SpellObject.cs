using System.Collections;
using UnityEngine;

public abstract class SpellObject : InventoryObject
{
    [SerializeField] 
    public PlayerController player;

    [SerializeField]
    public ObjectData spellData;

    public int health;
    public int MP;
    public int defense;

    public int WD;

    public int PD;

    public int HB;
    public int speed;

    public AudioClip Sound;

    public abstract void SpellEffect();

    protected bool CanCast()
    {
        if (player == null) return false;
        MultiplayerWorldSession world = MultiplayerWorldSession.Instance;
        return world == null || world.Playing && world.Loot.CanUse(gameObject);
    }

    protected GameObject SpawnProjectile(GameObject prefab, Vector3 position)
    {
        WeaponObject equippedWeapon = player.EquippedWeaponInstance;
        int weaponPD = equippedWeapon != null ? equippedWeapon.PD : 0;
        int damage = player.playerClass.PD + PD + weaponPD;

        GameObject projectile = Instantiate(prefab, position, player.transform.rotation);
        SpellProjectile spellProjectile = projectile.GetComponent<SpellProjectile>();
        spellProjectile.Initialize(damage);
        MultiplayerWorldSession world = MultiplayerWorldSession.Instance;
        if (world != null) spellProjectile.InitializeMultiplayerCast(world.RequestSpellCast(this, damage, position, player.transform.rotation));
        return projectile;
    }

    public virtual IEnumerator EndEffect(float num)
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
        Sound = spellData.AttackSound;
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
