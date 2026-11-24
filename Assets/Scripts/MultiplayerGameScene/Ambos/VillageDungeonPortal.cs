using System.Collections.Generic;
using UnityEngine;

/// <summary>Sustituye LoadProceduralGenScene en el portal de la aldea, mediante configuracion manual.</summary>
public class VillageDungeonPortal : MonoBehaviour
{
    [SerializeField] private SeedSelectionMenu seedMenu;
    [SerializeField] private Transform villageReturnPoint;
    private readonly HashSet<Collider> contacts = new HashSet<Collider>();
    private bool requiresExit;

    public bool TryGetReturnPose(out Vector3 position, out Quaternion rotation)
    {
        position = villageReturnPoint != null ? villageReturnPoint.position : Vector3.zero;
        rotation = villageReturnPoint != null ? villageReturnPoint.rotation : Quaternion.identity;
        return villageReturnPoint != null;
    }

    private void Awake()
    {
        if (GetComponent<LoadProceduralGenScene>() != null)
            Debug.LogError("Retira LoadProceduralGenScene del portal antes de probar VillageDungeonPortal.", this);
    }

    private void OnCollisionEnter(Collision collision) { Enter(collision.collider); }
    private void OnCollisionExit(Collision collision) { Exit(collision.collider); }
    private void OnTriggerEnter(Collider other) { Enter(other); }
    private void OnTriggerExit(Collider other) { Exit(other); }

    private void Enter(Collider other)
    {
        if (!other.CompareTag("Player") || other.GetComponentInParent<PlayerController>() != MultiplayerStartingRoomSpawns.LocalPlayer)
            return;
        bool firstContact = contacts.Count == 0;
        contacts.Add(other);
        if (!firstContact || requiresExit || seedMenu == null) return;
        requiresExit = true;
        seedMenu.TryOpen(this);
    }

    private void Exit(Collider other)
    {
        contacts.Remove(other);
        if (contacts.Count == 0) requiresExit = false;
    }

    private void OnDisable()
    {
        contacts.Clear();
        requiresExit = false;
    }
}
