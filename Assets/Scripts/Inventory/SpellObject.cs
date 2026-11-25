using System.Collections;
using UnityEngine;
using System;

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

    private GameObject interactCanvas;

    public abstract void SpellEffect();
    
    // cooldown lo quiero hacer observer para sacarlo del Update del HUD :P
    public event Action<bool> OnCooldownChanged;

    public bool IsOnCooldown { get; protected set; } = false;

    protected void SetCooldown(bool value)
    {
        if (IsOnCooldown == value)
            return;

        IsOnCooldown = value;
        OnCooldownChanged?.Invoke(IsOnCooldown);
    }

    private void OnEnable()
    {
        OnSelectionChanged += HandleSelectionChanged;
    }

    private void OnDisable()
    {
        OnSelectionChanged -= HandleSelectionChanged;
    }

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

    protected override void Awake()
    {
        base.Awake();

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

    private void Start()
    {
        if (transform.childCount > 0) interactCanvas = transform.GetChild(0).gameObject;

        HandleSelectionChanged(isSelected);
    }

    private void HandleSelectionChanged(bool selected)
    {
        if (interactCanvas != null) interactCanvas.SetActive(selected);
    }

    // void Update()
    // {
    //     if(transform.childCount > 0)
    //     {
    //         transform.GetChild(0).gameObject.SetActive(isSelected);
    //     }
    // }

}
