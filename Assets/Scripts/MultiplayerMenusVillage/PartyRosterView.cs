using UnityEngine;

/// <summary>Usar una copia en cada lista. Las filas sin boton de expulsion son informativas.</summary>
public class PartyRosterView : MonoBehaviour
{
    [SerializeField] private PartyMemberRow hostRow;
    [SerializeField] private PartyMemberRow client1Row;
    [SerializeField] private PartyMemberRow client2Row;
    private PhotonPartyService service;

    private void OnEnable()
    {
        AttachService();
        Render();
    }

    private void Start()
    {
        AttachService();
        Render();
    }

    private void OnDisable()
    {
        if (service != null) service.PartyChanged -= Render;
        service = null;
    }

    private void AttachService()
    {
        if (service != null || PhotonPartyService.Instance == null) return;
        service = PhotonPartyService.Instance;
        service.PartyChanged += Render;
    }

    private void Render()
    {
        BindRow(hostRow, PartyRole.Host);
        BindRow(client1Row, PartyRole.Client1);
        BindRow(client2Row, PartyRole.Client2);
    }

    private void BindRow(PartyMemberRow row, PartyRole role)
    {
        if (row == null) return;
        PartyMember member = null;
        if (service != null && service.IsInParty)
            foreach (PartyMember candidate in service.Members)
                if (candidate.Role == role) { member = candidate; break; }
        bool canKick = service != null && service.IsHost && !service.IsBusy &&
                       row.HasKickButton && role != PartyRole.Host && member != null;
        row.Bind(role, member, canKick, KickPlayer);
    }

    private void KickPlayer(int actorNumber)
    {
        if (service != null) service.KickPlayer(actorNumber);
    }
}
