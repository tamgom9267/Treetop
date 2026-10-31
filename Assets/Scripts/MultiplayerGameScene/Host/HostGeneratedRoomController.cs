using System.Collections.Generic;
using UnityEngine;

/// <summary>Combate local provisional del host. No hereda del controlador solitario.</summary>
public class HostGeneratedRoomController : MonoBehaviour
{
    public int enemyType;
    public GameObject[] spawners = new GameObject[0];
    public GameObject enterDoor;
    public GameObject exitDoor;
    [SerializeField] private string enemyResourcesPath = "GeneracionDeCuartos/Enemigos";
    private readonly List<GameObject> enemies = new List<GameObject>();
    private GameObject enemy1;
    private GameObject enemy2;
    private bool active;
    private bool completed;
    private bool pendingActivation;

    public void Configure(int type, string enemyPath) { enemyType = type; enemyResourcesPath = enemyPath; }

    private void Awake()
    {
        Transform plane = transform.Find("Plano");
        if (plane != null)
        {
            bool hasTrigger = false;
            foreach (Collider collider in plane.GetComponents<Collider>()) hasTrigger |= collider.isTrigger && collider.enabled;
            if (hasTrigger)
            {
                HostRoomTriggerForwarder forwarder = plane.GetComponent<HostRoomTriggerForwarder>();
                if (forwarder == null) forwarder = plane.gameObject.AddComponent<HostRoomTriggerForwarder>();
                forwarder.Initialize(this);
            }
        }
        enemy1 = Resources.Load<GameObject>(enemyResourcesPath + "/Enemigo1");
        enemy2 = Resources.Load<GameObject>(enemyResourcesPath + "/Enemigo2");
    }

    private void Start()
    {
        SetDoor(enterDoor, false);
        SetDoor(exitDoor, true);
    }

    private void Update()
    {
        if (MultiplayerRunSession.Instance == null || !MultiplayerRunSession.Instance.IsPlaying) return;
        if (pendingActivation) { pendingActivation = false; ActivateRoom(); }
        if (!active || completed) return;
        foreach (GameObject enemy in enemies) if (enemy != null) return;
        completed = true;
        active = false;
        SetDoor(enterDoor, false);
        SetDoor(exitDoor, false);
    }

    private void OnTriggerEnter(Collider other) { HandleTriggerEnter(other); }
    public void HandleTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && other.GetComponentInParent<PlayerController>() == MultiplayerStartingRoomSpawns.LocalPlayer)
            pendingActivation = true;
    }

    private void ActivateRoom()
    {
        if (active || completed) return;
        active = true;
        SetDoor(enterDoor, true);
        SetDoor(exitDoor, true);
        GameObject prefab = enemyType == 1 ? enemy1 : enemyType == 2 ? enemy2 : null;
        if (prefab == null) return;
        foreach (GameObject point in spawners)
            if (point != null) enemies.Add(Instantiate(prefab, point.transform.position, point.transform.rotation, transform));
    }

    private static void SetDoor(GameObject door, bool closed) { if (door != null) door.SetActive(closed); }
}
