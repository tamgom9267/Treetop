using System;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

/// <summary>Lee el personaje persistente; no lo sustituye ni ejecuta sus acciones.</summary>
public sealed class MultiplayerLocalPlayerPublisher : MonoBehaviour
{
    private MultiplayerPlayers owner;
    private PlayerController player;
    private readonly Dictionary<GameObject, int> itemIds = new Dictionary<GameObject, int>();
    private readonly HashSet<WeaponData> warnedWeapons = new HashSet<WeaponData>();
    private readonly List<GameObject> deadItems = new List<GameObject>();
    private readonly List<MultiplayerPlayerProtocol.Item> inventory = new List<MultiplayerPlayerProtocol.Item>();
    private readonly HashSet<int> scannedIds = new HashSet<int>();
    private MultiplayerPlayerProtocol.Item[] previousItems = Array.Empty<MultiplayerPlayerProtocol.Item>();
    private int nextItemId = 1;
    private int previousEquipped = -1;
    private double nextPose;
    private double nextInventory;
    private bool attackPending;

    public bool Bind(MultiplayerPlayers coordinator, PlayerController localPlayer, out string error)
    {
        error = "Falta el personaje, su Animator o su inventario para sincronizar jugadores.";
        if (localPlayer == null || localPlayer.playerAnimation == null || localPlayer.Inventory == null) return false;
        if (player != localPlayer)
        {
            Detach();
            player = localPlayer;
            player.AttackStarted += QueueAttack;
        }
        owner = coordinator;
        attackPending = false;
        nextPose = nextInventory = 0d;
        previousEquipped = -1;
        error = string.Empty;
        return true;
    }

    public void Detach()
    {
        if (player != null) player.AttackStarted -= QueueAttack;
        player = null;
        owner = null;
        attackPending = false;
    }

    public void Suspend() { attackPending = false; }
    private void OnDestroy() { Detach(); }
    private void QueueAttack() { attackPending = true; }

    public void Tick(float frequency)
    {
        if (player == null || owner == null) return;
        double now = Time.realtimeSinceStartupAsDouble;
        if (now >= nextInventory)
        {
            nextInventory = now + 0.1d;
            MultiplayerPlayerProtocol.Equipment state = CaptureEquipment();
            if (Changed(state) && owner.SendEquipment(state)) Remember(state);
        }
        // LateUpdate observa la rotacion final del Update que disparo el ataque.
        if (attackPending)
        {
            MultiplayerPlayerProtocol.Item item = Describe(player.weapon);
            if (owner.SendAttack(new MultiplayerPlayerProtocol.Attack { pose = CapturePose(), item = item })) attackPending = false;
        }
        if (now >= nextPose)
        {
            nextPose = now + 1d / Mathf.Clamp(frequency, 1f, 30f);
            owner.SendPose(CapturePose());
        }
    }

    public bool SendFullState(int targetActor = 0)
    {
        if (player == null || owner == null) return false;
        MultiplayerPlayerProtocol.Equipment state = CaptureEquipment();
        if (!owner.SendEquipment(state, targetActor)) return false;
        // Una respuesta dirigida no reemplaza el ultimo estado difundido al grupo.
        if (targetActor == 0) Remember(state);
        return true;
    }

    private MultiplayerPlayerProtocol.Pose CapturePose()
    {
        Animator animator = player.playerAnimation;
        bool blocked = !player.enabled || player.IsExternalUIBlocked || player.IsInventoryOpen || Time.timeScale <= 0f;
        return new MultiplayerPlayerProtocol.Pose
        {
            time = PhotonNetwork.Time,
            position = player.transform.position,
            rotation = player.transform.rotation,
            speed = blocked ? 0f : Mathf.Clamp(animator.GetFloat("Speed"), 0f, 2f),
            playbackRate = Time.timeScale <= 0f ? 0f : Mathf.Clamp(animator.speed * Time.timeScale, 0f, 10f)
        };
    }

    private MultiplayerPlayerProtocol.Equipment CaptureEquipment()
    {
        inventory.Clear();
        scannedIds.Clear();
        // El flujo existente tambien permite equipar desde la ventana de loot.
        MultiplayerPlayerProtocol.Item equipped = AddItem(player.weapon);
        foreach (GameObject item in player.Inventory.Inventory) AddItem(item);
        inventory.Sort((left, right) => left.id.CompareTo(right.id));
        deadItems.Clear();
        foreach (GameObject item in itemIds.Keys) if (item == null) deadItems.Add(item);
        foreach (GameObject item in deadItems) itemIds.Remove(item);
        return new MultiplayerPlayerProtocol.Equipment
        { pose = CapturePose(), items = inventory.ToArray(), equippedId = equipped.id };
    }

    private MultiplayerPlayerProtocol.Item AddItem(GameObject item)
    {
        MultiplayerPlayerProtocol.Item description = Describe(item);
        if (description.id > 0 && scannedIds.Add(description.id) && inventory.Count < MultiplayerPlayerProtocol.MaxItems)
            inventory.Add(description);
        return description;
    }

    private MultiplayerPlayerProtocol.Item Describe(GameObject item)
    {
        WeaponObject weapon = item != null ? item.GetComponent<WeaponObject>() : null;
        if (weapon == null || weapon.weaponData == null) return default;
        if (!owner.TryGetType(weapon.weaponData, out string type))
        {
            if (warnedWeapons.Add(weapon.weaponData))
                Debug.LogWarning("Multiplayer: registra el arma en el catalogo visual: " + weapon.weaponData.name, this);
            return default;
        }
        if (!itemIds.TryGetValue(item, out int id))
        {
            id = nextItemId++;
            itemIds.Add(item, id);
        }
        return new MultiplayerPlayerProtocol.Item(id, type);
    }

    private bool Changed(MultiplayerPlayerProtocol.Equipment state)
    {
        if (previousEquipped != state.equippedId || previousItems.Length != state.items.Length) return true;
        for (int i = 0; i < previousItems.Length; i++)
            if (previousItems[i].id != state.items[i].id || previousItems[i].type != state.items[i].type) return true;
        return false;
    }

    private void Remember(MultiplayerPlayerProtocol.Equipment state)
    {
        previousEquipped = state.equippedId;
        previousItems = state.items;
    }
}
