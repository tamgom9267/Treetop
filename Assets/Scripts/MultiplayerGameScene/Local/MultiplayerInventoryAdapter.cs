using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

// Intercepta E antes del PlayerController sin cambiar su archivo ni su inventario.
[DefaultExecutionOrder(-100)]
public sealed class MultiplayerInventoryAdapter : MonoBehaviour
{
    [SerializeField] private GameObject itemPanelPrefab;
    [SerializeField] private string lootImagesPath, lootSlotsPath;
    private static readonly FieldInfo Moving = typeof(PlayerInventory).GetField("isMoving", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo DragImage = typeof(PlayerInventory).GetField("SpriteObject", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo HitDuration = typeof(PlayerController).GetField("receivedHitStopDuration", BindingFlags.Instance | BindingFlags.NonPublic);
    private MultiplayerWorldSession world;
    private PlayerController player;
    private PlayerInventory inventory;
    private float previousHitDuration;
    private bool bound, ownBlock, previousBlock;
    private DragSnapshot stable;
    private sealed class DragSnapshot
    {
        public List<List<int>> grid, loot;
        public readonly Dictionary<GameObject, ItemPose> items = new Dictionary<GameObject, ItemPose>();
    }
    private sealed class ItemPose
    {
        public MultiplayerItemData data;
        public Vector3 position;
        public Quaternion rotation;
        public Vector2 pivot, colliderOffset;
    }
    public bool Validate(out string error)
    {
        error = "El adaptador no coincide con el inventario original o faltan sus referencias UI. Regenera los prefabs de fase 4.";
        if (Moving == null || Moving.FieldType != typeof(bool) || DragImage == null || DragImage.FieldType != typeof(GameObject) ||
            HitDuration == null || HitDuration.FieldType != typeof(float) || itemPanelPrefab == null ||
            string.IsNullOrEmpty(lootImagesPath) || string.IsNullOrEmpty(lootSlotsPath)) return false;
        error = string.Empty; return true;
    }
    public bool Bind(MultiplayerWorldSession owner, PlayerController local, out string error)
    {
        error = "Faltan personaje, inventario inicializado o contenedores de loot de la UI.";
        if (local == null || local.Inventory == null || local.Inventory.cursor == null ||
            local.Inventory.transform.Find(lootImagesPath) == null || local.Inventory.transform.Find(lootSlotsPath) == null) return false;
        world = owner;
        if (bound && player == local) { error = string.Empty; return true; }
        Shutdown(); world = owner; player = local; inventory = local.Inventory;
        previousHitDuration = (float)HitDuration.GetValue(player); HitDuration.SetValue(player, 0f); bound = true;
        CaptureStable(); error = string.Empty; return true;
    }
    private static List<List<int>> Clone(List<List<int>> source)
    { var copy = new List<List<int>>(); foreach (var row in source) copy.Add(new List<int>(row)); return copy; }
    private void CaptureStable()
    {
        if (inventory == null || (bool)Moving.GetValue(inventory)) return;
        stable = new DragSnapshot { grid = Clone(inventory.grid), loot = Clone(inventory.lootgrid) };
        foreach (PanelManager panel in inventory.GetComponentsInChildren<PanelManager>(true))
        {
            if (panel.itemObj == null || panel.itemObj.GetComponent<InventoryObject>() == null) continue;
            RectTransform rect = panel.GetComponent<RectTransform>(); var collider = panel.GetComponent<BoxCollider2D>();
            stable.items[panel.gameObject] = new ItemPose { data = MultiplayerItemData.Capture(panel.itemObj.GetComponent<InventoryObject>()),
                position = rect.position, rotation = rect.localRotation, pivot = rect.pivot, colliderOffset = collider != null ? collider.offset : Vector2.zero };
        }
    }
    private void Block()
    {
        if (!ownBlock) { previousBlock = player.IsExternalUIBlocked; ownBlock = true; }
        player.SetExternalUIBlocked(true);
    }
    private void Unblock()
    { if (ownBlock && player != null) player.SetExternalUIBlocked(previousBlock); ownBlock = false; }
    private void Update()
    {
        if (!bound || world == null) return;
        if (world.Loot.Settling || world.Loot.ClaimPending) { Block(); return; }
        if (!world.Playing || !player.enabled || player.IsExternalUIBlocked && !ownBlock) return;
        if (player.inventory.action.WasPressedThisFrame() && player.IsInventoryOpen) CancelDrag();
        CaptureStable();
        if (!player.IsInventoryOpen && player.interact.action.WasPressedThisFrame() && player.nearChest == null && player.nearestWeapon != null)
        {
            // Consume tambien el intento rechazado: el metodo original nunca puede recoger sin permiso.
            Block(); world.Loot.TryClaim(player.nearestWeapon);
        }
        if (player.IsInventoryOpen && player.attack.action.WasPressedThisFrame())
        {
            var cursor = inventory.cursor.GetComponent<CursorObject>();
            var panel = cursor.InventoryItemObj != null ? cursor.InventoryItemObj.GetComponent<PanelManager>() : null;
            if (panel != null && !world.Loot.CanUse(panel.itemObj)) Block();
        }
    }
    private void LateUpdate()
    {
        if (!bound || world == null) return;
        if (!world.Loot.Settling && !world.Loot.ClaimPending) Unblock();
    }
    public bool Inspect(InventoryObject item, Sprite icon)
    {
        if (!bound || item == null || inventory.AvailableIDs.Count == 0 || inventory.Loot.Count != 0) return false;
        MultiplayerItemData original = MultiplayerItemData.Capture(item);
        var fit = inventory.CheckFit(item.gameObject, inventory.lootgrid, 0, 0);
        for (int turn = 0; turn < 3 && !fit.canFit; turn++)
        {
            RotateShape(item);
            fit = inventory.CheckFit(item.gameObject, inventory.lootgrid, 0, 0);
        }
        if (!fit.canFit || fit.ObjImagePosition.Count == 0) { original.Apply(item); return false; }
        item.ID = inventory.AvailableIDs[0]; inventory.AvailableIDs.RemoveAt(0);
        inventory.Loot.Add(item.gameObject); item.gameObject.SetActive(false);
        for (int c = 0; c < item.requieredCells.Count; c++)
            for (int r = 0; r < item.requieredCells[c].Count; r++)
                if (item.requieredCells[c][r] == 1) inventory.lootgrid[c][r] = item.ID;
        GameObject image = Instantiate(itemPanelPrefab, inventory.transform.Find(lootImagesPath));
        PanelManager panel = image.GetComponent<PanelManager>(); panel.WeaponID = item.ID; panel.itemObj = item.gameObject;
        var box = image.GetComponent<BoxCollider2D>();
        box.size = new Vector2(120 * (fit.scalewidth + 1), 150 * (fit.scaleheight + 1));
        box.offset = new Vector2(box.size.x * 0.66f, box.size.y / -2 - 5 * fit.scaleheight);
        foreach (Transform slot in inventory.transform.Find(lootSlotsPath))
            if (slot.GetComponent<PanelManager>().location == fit.ObjImagePosition[0])
                image.GetComponent<RectTransform>().anchoredPosition = slot.GetComponent<RectTransform>().anchoredPosition;
        if (item is WeaponObject weapon) icon = weapon.weaponData.UIsprite;
        image.GetComponent<Image>().sprite = icon;
        RectTransform rect = image.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(rect.sizeDelta.x * (fit.scalewidth + 1), rect.sizeDelta.y * (fit.scaleheight + 1));
        inventory.gameObject.SetActive(true); CaptureStable(); return true;
    }
    // El loot tiene dos columnas: una espada soltada horizontal debe poder inspeccionarse otra vez.
    public static void RotateShape(InventoryObject item)
    {
        int height = item.requieredCells.Count, width = 0;
        foreach (var row in item.requieredCells) width = Mathf.Max(width, row.Count);
        var rotated = new List<List<int>>();
        for (int y = 0; y < width; y++)
        {
            var row = new List<int>();
            for (int x = 0; x < height; x++)
            { var old = item.requieredCells[height - 1 - x]; row.Add(y < old.Count ? old[y] : 0); }
            rotated.Add(row);
        }
        int minX = height, minY = width, maxX = -1, maxY = -1;
        for (int y = 0; y < width; y++) for (int x = 0; x < height; x++) if (rotated[y][x] > 0)
        { minX = Mathf.Min(minX,x); minY = Mathf.Min(minY,y); maxX = Mathf.Max(maxX,x); maxY = Mathf.Max(maxY,y); }
        if (maxX < 0) return;
        item.requieredCells.Clear();
        for (int y = minY; y <= maxY; y++) item.requieredCells.Add(rotated[y].GetRange(minX,maxX-minX+1));
    }
    public void CancelDrag()
    {
        if (!bound || !(bool)Moving.GetValue(inventory)) return;
        GameObject image = DragImage.GetValue(inventory) as GameObject;
        if (image != null && stable != null && stable.items.TryGetValue(image, out var pose))
        {
            var panel = image.GetComponent<PanelManager>();
            if (panel.itemObj != null)
            {
                pose.data.Apply(panel.itemObj.GetComponent<InventoryObject>());
                inventory.grid = Clone(stable.grid); inventory.lootgrid = Clone(stable.loot);
                RectTransform rect = image.GetComponent<RectTransform>(); rect.position = pose.position; rect.localRotation = pose.rotation; rect.pivot = pose.pivot;
                var collider = image.GetComponent<BoxCollider2D>(); if (collider != null) collider.offset = pose.colliderOffset;
            }
        }
        Moving.SetValue(inventory, false); DragImage.SetValue(inventory, null);
        if (inventory.cursor != null) inventory.cursor.GetComponent<CursorObject>().ResetCursor();
    }
    public void CloseForTransition()
    {
        if (!bound) return;
        CancelDrag();
        if (inventory.Loot.Count > 0) inventory.RemoveWeaponFromChestLootGrid(player.transform.position);
        inventory.gameObject.SetActive(false); player.EndAttack(); Block();
    }
    private static void ClearCells(List<List<int>> grid, int id)
    { if (id <= 0) return; foreach (var row in grid) for (int i = 0; i < row.Count; i++) if (row[i] == id) row[i] = 0; }
    public void Revoke(InventoryObject item, bool destroy)
    {
        if (item == null) return;
        GameObject target = item.gameObject;
        if (bound)
        {
            if (player.weapon == target) { player.EndAttack(); player.UnequipWeapon(); }
            inventory.Inventory.RemoveAll(x => x == target); inventory.Loot.RemoveAll(x => x == target);
            ClearCells(inventory.grid, item.ID); ClearCells(inventory.lootgrid, item.ID);
            if (inventory.SelectedObject == item) inventory.SelectedObject = null;
            if (inventory.LootObject == item) inventory.LootObject = null;
            if (player.Slot1 == item) player.Slot1 = null; if (player.Slot2 == item) player.Slot2 = null; if (player.Slot3 == item) player.Slot3 = null;
            if (player.SelectedItem == item) player.SelectedItem = null;
            player.nearWeapons.RemoveAll(x => x == target);
            if (player.nearestWeapon == target || player.nearestWeaponObj == item) { player.nearestWeapon = null; player.nearestWeaponObj = null; }
            GameObject drag = DragImage.GetValue(inventory) as GameObject;
            if (drag != null && drag.GetComponent<PanelManager>().itemObj == target)
            { Moving.SetValue(inventory, false); DragImage.SetValue(inventory, null); }
            var cursor = inventory.cursor != null ? inventory.cursor.GetComponent<CursorObject>() : null;
            if (cursor != null && cursor.InventoryItemObj != null && cursor.InventoryItemObj.GetComponent<PanelManager>().itemObj == target) cursor.ResetCursor();
            foreach (PanelManager panel in inventory.GetComponentsInChildren<PanelManager>(true))
                if (panel.itemObj == target)
                { panel.itemObj = null; panel.gameObject.SetActive(false); RemoveRuntimeObject(panel.gameObject); }
            if (stable != null)
            { ClearCells(stable.grid, item.ID); ClearCells(stable.loot, item.ID); }
            if (item.ID > 0 && !inventory.AvailableIDs.Contains(item.ID)) inventory.AvailableIDs.Add(item.ID);
        }
        item.ID = 0;
        if (destroy) { target.SetActive(false); RemoveRuntimeObject(target); }
    }
    private static void RemoveRuntimeObject(UnityEngine.Object target)
    {
        if (Application.isPlaying) Destroy(target);
        else DestroyImmediate(target); // Comprobaciones nativas en escenas de vista previa, nunca assets.
    }
    public void Shutdown()
    {
        if (!bound) return;
        CancelDrag(); Unblock();
        if (player != null) HitDuration.SetValue(player, previousHitDuration);
        bound = false; player = null; inventory = null; stable = null;
    }
    private void OnDestroy() { Shutdown(); }
}
