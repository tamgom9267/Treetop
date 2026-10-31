using UnityEngine;

public class ClientRoomTriggerForwarder : MonoBehaviour
{
    private ClientGeneratedRoomController room;
    public void Initialize(ClientGeneratedRoomController controller) { room = controller; }
    private void OnTriggerEnter(Collider other) { if (room != null) room.HandleTriggerEnter(other); }
}
