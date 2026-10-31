using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Agregar a cada fila ya existente; asignar sus textos y su boton en el Inspector.</summary>
public class PartyMemberRow : MonoBehaviour
{
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private TMP_Text roleText;
    [SerializeField] private Button kickButton;

    public bool HasKickButton => kickButton != null;

    private int actorNumber;
    private Action<int> kick;

    private void Awake()
    {
        if (kickButton != null) kickButton.onClick.AddListener(OnKickClicked);
        if (playerNameText != null) playerNameText.richText = false;
    }

    private void OnDestroy()
    {
        if (kickButton != null) kickButton.onClick.RemoveListener(OnKickClicked);
    }

    public void Bind(PartyRole slot, PartyMember member, bool canKick, Action<int> kickAction)
    {
        actorNumber = member != null ? member.ActorNumber : 0;
        kick = canKick ? kickAction : null;
        if (playerNameText != null) playerNameText.text = member != null ? member.NickName : "Disponible";
        if (roleText != null) roleText.text = PartyRules.RoleLabel(slot);
        if (kickButton != null) kickButton.interactable = canKick && member != null && slot != PartyRole.Host;
    }

    private void OnKickClicked()
    {
        if (actorNumber > 0) kick?.Invoke(actorNumber);
    }
}
