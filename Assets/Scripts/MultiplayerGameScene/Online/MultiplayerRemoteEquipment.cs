using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class MultiplayerRemoteEquipment : MonoBehaviour
{
    [SerializeField] private Transform hand;
    private MultiplayerVisualDefinition[] catalog = Array.Empty<MultiplayerVisualDefinition>();
    private readonly Dictionary<int, Entry> entries = new Dictionary<int, Entry>();
    private readonly HashSet<string> warnedTypes = new HashSet<string>();
    private readonly List<int> removed = new List<int>();
    private int equippedId;
    private int actionId;
    private bool actionActive;

    private sealed class Entry
    {
        public string type;
        public MultiplayerWeaponVisual visual;
        public bool retained;
    }

    public Transform Hand => hand;
    public int CachedCount => entries.Count;

    public void Configure(Transform anchor, MultiplayerVisualDefinition[] definitions)
    {
        hand = anchor;
        catalog = definitions ?? Array.Empty<MultiplayerVisualDefinition>();
    }

    public void Apply(MultiplayerPlayerProtocol.Equipment state)
    {
        foreach (Entry entry in entries.Values) entry.retained = false;
        foreach (MultiplayerPlayerProtocol.Item item in state.items)
        {
            Entry entry = Ensure(item);
            if (entry != null) entry.retained = true;
        }
        equippedId = state.equippedId;
        Prune();
        RefreshVisibility();
    }

    public void BeginAction(MultiplayerPlayerProtocol.Item item)
    {
        EndAction();
        actionActive = true;
        actionId = item.id;
        if (item.id > 0) Ensure(item);
        RefreshVisibility();
    }

    public void SetActionTrail(bool emitting)
    {
        if (actionActive && entries.TryGetValue(actionId, out Entry entry)) entry.visual.SetTrail(emitting);
    }

    public void EndAction()
    {
        foreach (Entry entry in entries.Values) entry.visual.SetTrail(false);
        actionActive = false;
        actionId = 0;
        Prune();
        RefreshVisibility();
    }

    private Entry Ensure(MultiplayerPlayerProtocol.Item item)
    {
        if (entries.TryGetValue(item.id, out Entry existing))
        {
            if (existing.type == item.type) return existing;
            Remove(item.id);
        }
        MultiplayerVisualDefinition definition = Array.Find(catalog, candidate => candidate != null && candidate.typeId == item.type);
        if (definition == null || definition.remotePrefab == null || hand == null)
        {
            if (warnedTypes.Add(item.type ?? string.Empty))
                Debug.LogWarning("Multiplayer: objeto visual no registrado: " + item.type, this);
            return null;
        }
        MultiplayerWeaponVisual visual = Instantiate(definition.remotePrefab, hand, false);
        visual.name = "Item_" + item.id + "_" + item.type;
        visual.transform.localPosition = Vector3.zero;
        visual.SetTrail(false);
        visual.gameObject.SetActive(false);
        Entry entry = new Entry { type = item.type, visual = visual };
        entries.Add(item.id, entry);
        return entry;
    }

    private void RefreshVisibility()
    {
        int visibleId = actionActive ? actionId : equippedId;
        foreach (KeyValuePair<int, Entry> pair in entries)
        {
            bool visible = pair.Key == visibleId;
            if (!visible) pair.Value.visual.SetTrail(false);
            if (pair.Value.visual.gameObject.activeSelf != visible) pair.Value.visual.gameObject.SetActive(visible);
        }
    }

    private void Prune()
    {
        removed.Clear();
        foreach (KeyValuePair<int, Entry> pair in entries)
            if (!pair.Value.retained && !(actionActive && pair.Key == actionId)) removed.Add(pair.Key);
        foreach (int id in removed) Remove(id);
    }

    private void Remove(int id)
    {
        Entry entry = entries[id];
        entry.visual.gameObject.SetActive(false);
        #if UNITY_EDITOR
        if (!Application.isPlaying) DestroyImmediate(entry.visual.gameObject);
        else
        #endif
        Destroy(entry.visual.gameObject);
        entries.Remove(id);
    }
}
