using UnityEngine;

public class MultiplayerStartingRoomSpawns : MonoBehaviour
{
    [SerializeField] private Transform hostSpawn;
    [SerializeField] private Transform clientSpawn1;
    [SerializeField] private Transform clientSpawn2;

    public void FindLegacyPoints()
    {
        if (hostSpawn == null) hostSpawn = transform.Find("SpawnerHost");
        if (clientSpawn1 == null) clientSpawn1 = transform.Find("SpawnerCliente1");
        if (clientSpawn2 == null) clientSpawn2 = transform.Find("SpawnerCliente2");
    }

    public static PlayerController LocalPlayer => NewestSingleton.Instance != null
        ? NewestSingleton.Instance.GetComponentInChildren<PlayerController>(true) : null;

    public bool PlaceLocalPlayer(out string error)
    {
        FindLegacyPoints();
        PartyRole role = PhotonPartyService.Instance != null ? PhotonPartyService.Instance.LocalRole : PartyRole.None;
        Transform point = role == PartyRole.Host ? hostSpawn : role == PartyRole.Client1 ? clientSpawn1 :
            role == PartyRole.Client2 ? clientSpawn2 : null;
        PlayerController player = LocalPlayer;
        error = "Falta el personaje local o el spawn de su rol en el cuarto inicial.";
        if (point == null || player == null) return false;
        Transform root = NewestSingleton.Instance.transform;
        root.position += point.position - player.transform.position;
        player.transform.SetPositionAndRotation(point.position, point.rotation);
        if (player.rb != null)
        {
            player.rb.position = point.position;
            player.rb.rotation = point.rotation;
        }
        Physics.SyncTransforms();
        error = string.Empty;
        return true;
    }
}
