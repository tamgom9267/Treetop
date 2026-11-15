using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DeathPanelView : MonoBehaviour
{
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private TMP_Text buttonText;
    [SerializeField] private Button actionButton;

    public bool IsConfigured => messageText != null && buttonText != null && actionButton != null;

    public void Configure(string message, string buttonLabel, Action onAction)
    {
        messageText.text = message;
        buttonText.text = buttonLabel;
        actionButton.onClick.AddListener(() => onAction());
    }
}
