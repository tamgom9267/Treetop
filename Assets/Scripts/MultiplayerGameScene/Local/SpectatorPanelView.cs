using TMPro;
using UnityEngine;

public sealed class SpectatorPanelView : MonoBehaviour
{
    [SerializeField] private TMP_Text messageText;

    public void SetMessage(string message)
    {
        if (messageText != null) messageText.text = message;
    }
}
