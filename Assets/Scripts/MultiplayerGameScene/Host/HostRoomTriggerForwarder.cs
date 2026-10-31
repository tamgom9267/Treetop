using UnityEngine;

public class HostRoomTriggerForwarder : MonoBehaviour
{
    private HostGeneratedRoomController room;
    public void Initialize(HostGeneratedRoomController controller) { room = controller; }
    private void OnTriggerEnter(Collider other) { if (room != null) room.HandleTriggerEnter(other); }
}
