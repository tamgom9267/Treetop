using UnityEngine;

public class RoomTriggerForwarder : MonoBehaviour
{
    private GeneratedRoomController roomController;

    public void Initialize(GeneratedRoomController controller)
    {
        roomController = controller;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (roomController != null)
            roomController.HandleTriggerEnter(other);
    }
}
