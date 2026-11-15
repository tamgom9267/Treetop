using UnityEngine;

public class HostFloorPortal : MonoBehaviour
{
    private bool activated;
    private void OnTriggerEnter(Collider other) { TryAdvance(other); }
    private void OnCollisionEnter(Collision collision) { TryAdvance(collision.collider); }

    private void TryAdvance(Collider other)
    {
        if (activated || !other.CompareTag("Player") ||
            other.GetComponentInParent<PlayerController>() != MultiplayerStartingRoomSpawns.LocalPlayer) return;
        PhotonPartyService party = PhotonPartyService.Instance;
        MultiplayerRunSession session = MultiplayerRunSession.Instance;
        MultiplayerDeathSession deaths = MultiplayerDeathSession.Instance;
        if (party == null || session == null || !session.IsPlaying || deaths == null ||
            !deaths.IsAlive(MultiplayerGameplayProtocol.LocalActor)) return;
        if (party.IsHost) activated = session.TryAdvanceFromActor(MultiplayerGameplayProtocol.LocalActor);
        else activated = deaths.RequestFloorAdvance();
    }
}
