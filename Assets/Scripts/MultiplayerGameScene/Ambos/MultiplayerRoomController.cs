using UnityEngine;

// Existe exclusivamente en las copias multijugador.
public abstract class MultiplayerRoomController : MonoBehaviour
{
    public int enemyType;
    public GameObject[] spawners = new GameObject[0];
    public GameObject enterDoor, exitDoor;
    [SerializeField] private string roomId;
    private Collider[] floorTriggers;
    public string RoomId => roomId;
    public int Stage { get; private set; }
    public int Revision { get; private set; }
    public void Configure(int type, string unusedPath, string id = null)
    { enemyType = type; if (id != null) roomId = id; }
    protected virtual void Awake()
    {
        Transform floor = transform.Find("Plano");
        floorTriggers = floor != null ? floor.GetComponents<Collider>() : new Collider[0];
        if (MultiplayerWorldSession.Instance != null) MultiplayerWorldSession.Instance.RegisterRoom(this);
    }
    protected virtual void Start() { ApplyDoors(); }
    public bool ContainsLocal(PlayerController player)
    {
        if (player == null || floorTriggers == null) return false;
        foreach (Collider body in player.GetComponents<Collider>())
        {
            if (!body.enabled || body.isTrigger) continue;
            foreach (Collider floor in floorTriggers)
                if (floor.enabled && floor.isTrigger && floor.bounds.Intersects(body.bounds) &&
                    Physics.ComputePenetration(body, body.transform.position, body.transform.rotation,
                        floor, floor.transform.position, floor.transform.rotation, out _, out _)) return true;
        }
        return false;
    }
    public bool NearFloor(Vector3 position)
    {
        if (floorTriggers == null) return false;
        foreach (Collider floor in floorTriggers)
        {
            Bounds bounds = floor.bounds; bounds.Expand(new Vector3(2f, 12f, 2f));
            if (floor.isTrigger && bounds.Contains(position)) return true;
        }
        return false;
    }
    public void ApplyState(int stage, int revision)
    {
        if (stage < 0 || stage > 2 || revision <= Revision || stage < Stage) return;
        Stage = stage; Revision = revision; ApplyDoors();
    }
    private void ApplyDoors()
    {
        if (enterDoor != null) enterDoor.SetActive(Stage == 1);
        if (exitDoor != null) exitDoor.SetActive(Stage != 2);
    }
    public void HandleTriggerEnter(Collider other) { }
    public void HandleTriggerExit(Collider other) { }
}
