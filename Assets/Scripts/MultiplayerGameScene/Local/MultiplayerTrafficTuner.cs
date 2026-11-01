using Photon.Pun;
using UnityEngine;

/// <summary>Ajustes locales de prueba. Se puede retirar y borrar sin cambiar los scripts de envio.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(MultiplayerPlayers))]
[DefaultExecutionOrder(-200)]
[AddComponentMenu("Multiplayer/Ajustes temporales de trafico")]
public sealed class MultiplayerTrafficTuner : MonoBehaviour
{
    [Header("Pruebas locales: se pueden ajustar durante Play")]
    [Tooltip("Los valores afectan solo a esta instancia. Para conservarlos, copia el componente durante Play y pega sus valores en el prefab al salir.")]
    [SerializeField] private MultiplayerPlayers.TrafficRates rates = MultiplayerPlayers.TrafficRates.Defaults();

    [Header("Salida global de Photon en esta instancia")]
    [InspectorName("Salida de Photon (Hz)"), Range(1, 120)]
    [Tooltip("Frecuencia para vaciar las colas de esta instancia hacia Photon. Puede agrupar varios mensajes por salida y depende de los FPS. No cambia SerializationRate ni la frecuencia de recepcion.")]
    [SerializeField] private int photonSendRateHz = 30;

    private MultiplayerPlayers players;
    private MultiplayerPlayers.TrafficRates appliedRates;
    private bool ratesApplied;
    private bool sendRateCaptured;
    private int previousSendRate;
    private int appliedSendRate;

    private void OnEnable()
    {
        if (!Application.isPlaying) return;
        players = GetComponent<MultiplayerPlayers>();
        ApplyChanges();
    }

    private void Update() { ApplyChanges(); }

    private void OnValidate()
    {
        rates = rates.Validated();
        photonSendRateHz = Mathf.Clamp(photonSendRateHz, 1, 120);
    }

    private void ApplyChanges()
    {
        if (players == null || !players.isActiveAndEnabled)
        {
            Restore();
            return;
        }

        rates = rates.Validated();
        photonSendRateHz = Mathf.Clamp(photonSendRateHz, 1, 120);
        if (!ratesApplied || !rates.Equals(appliedRates))
        {
            players.ApplyTrafficRates(rates);
            appliedRates = rates;
            ratesApplied = true;
        }

        if (!sendRateCaptured)
        {
            previousSendRate = PhotonNetwork.SendRate;
            sendRateCaptured = true;
            PhotonNetwork.SendRate = photonSendRateHz;
            appliedSendRate = photonSendRateHz;
        }
        else if (appliedSendRate != photonSendRateHz)
        {
            // Comparar el valor solicitado: Photon lo redondea a milisegundos internamente.
            PhotonNetwork.SendRate = photonSendRateHz;
            appliedSendRate = photonSendRateHz;
        }
    }

    private void Restore()
    {
        if (ratesApplied && players != null) players.ResetTrafficRates();
        ratesApplied = false;
        if (!sendRateCaptured) return;
        PhotonNetwork.SendRate = previousSendRate;
        sendRateCaptured = false;
    }

    private void OnDisable() { Restore(); }
    private void OnDestroy() { Restore(); }
}
