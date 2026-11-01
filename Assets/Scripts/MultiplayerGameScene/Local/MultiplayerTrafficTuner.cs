using System;
using ExitGames.Client.Photon;
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

    [Header("Medicion local en el Inspector")]
    [InspectorName("Medir trafico durante Play")]
    [Tooltip("Muestra estadisticas locales cada segundo en este Inspector. No envia mensajes adicionales y restaura la recopilacion anterior al desactivarse.")]
    [SerializeField] private bool measureTraffic = true;

    public struct TrafficSnapshot
    {
        public double sentKBps, receivedKBps, sentMessagesPerSecond, receivedMessagesPerSecond;
        public double resendsPerSecond, framesPerSecond, elapsedSeconds, sentMB, receivedMB;
        public int pingMs, pingVariationMs, queuedOutgoing, queuedReliableIncoming;
        public string connectionState;
    }

    public TrafficSnapshot Statistics { get; private set; }
    public bool HasTrafficSample { get; private set; }
    public bool IsMeasuringTraffic => statsPeer != null;

    private MultiplayerPlayers players;
    private MultiplayerPlayers.TrafficRates appliedRates;
    private bool ratesApplied;
    private bool sendRateCaptured;
    private int previousSendRate;
    private int appliedSendRate;
    private PhotonPeer statsPeer;
    private bool previousStatsEnabled;
    private double sampleAt, measurementStartedAt;
    private int sampleFrame, previousMessagesIn, previousMessagesOut, previousResends;
    private long previousBytesIn, previousBytesOut, measuredBytesIn, measuredBytesOut;

    private void OnEnable()
    {
        if (!Application.isPlaying) return;
        players = GetComponent<MultiplayerPlayers>();
        ApplyChanges();
        SampleTraffic();
    }

    private void Update() { ApplyChanges(); SampleTraffic(); }

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
        StopMeasuringTraffic();
        if (ratesApplied && players != null) players.ResetTrafficRates();
        ratesApplied = false;
        if (!sendRateCaptured) return;
        PhotonNetwork.SendRate = previousSendRate;
        sendRateCaptured = false;
    }

    private void SampleTraffic()
    {
        if (!measureTraffic || players == null || !players.isActiveAndEnabled)
        {
            StopMeasuringTraffic();
            return;
        }

        PhotonPeer peer = PhotonNetwork.NetworkingClient?.LoadBalancingPeer;
        if (peer == null) { StopMeasuringTraffic(); return; }
        if (statsPeer != peer)
        {
            StopMeasuringTraffic();
            statsPeer = peer;
            previousStatsEnabled = peer.TrafficStatsEnabled;
            if (!previousStatsEnabled) peer.TrafficStatsEnabled = true;
            ResetTrafficMeasurement();
        }
        else if (!peer.TrafficStatsEnabled)
        {
            peer.TrafficStatsEnabled = true;
            ResetTrafficMeasurement();
        }

        double now = Time.realtimeSinceStartupAsDouble;
        double elapsed = now - sampleAt;
        if (elapsed < 1d) return;
        TrafficStatsGameLevel messages = peer.TrafficStatsGameLevel;
        long bytesIn = peer.BytesIn, bytesOut = peer.BytesOut;
        int incoming = messages.TotalIncomingMessageCount, outgoing = messages.TotalOutgoingMessageCount;
        int resends = peer.ResentReliableCommands;
        // Una reconexion o un reinicio externo de contadores inicia una nueva medicion.
        if (bytesIn < previousBytesIn || bytesOut < previousBytesOut || incoming < previousMessagesIn ||
            outgoing < previousMessagesOut || resends < previousResends)
        { ResetTrafficMeasurement(); return; }

        long received = bytesIn - previousBytesIn, sent = bytesOut - previousBytesOut;
        measuredBytesIn += received; measuredBytesOut += sent;
        Statistics = new TrafficSnapshot
        {
            sentKBps = sent / 1000d / elapsed, receivedKBps = received / 1000d / elapsed,
            sentMessagesPerSecond = (outgoing - previousMessagesOut) / elapsed,
            receivedMessagesPerSecond = (incoming - previousMessagesIn) / elapsed,
            resendsPerSecond = (resends - previousResends) / elapsed,
            framesPerSecond = Math.Max(0, Time.frameCount - sampleFrame) / elapsed,
            elapsedSeconds = now - measurementStartedAt,
            sentMB = measuredBytesOut / 1000000d, receivedMB = measuredBytesIn / 1000000d,
            pingMs = peer.RoundTripTime, pingVariationMs = peer.RoundTripTimeVariance,
            queuedOutgoing = peer.QueuedOutgoingCommands, queuedReliableIncoming = peer.QueuedIncomingCommands,
            connectionState = PhotonNetwork.OfflineMode ? "Offline (sin trafico de red)" : PhotonNetwork.NetworkClientState.ToString()
        };
        HasTrafficSample = true;
        CaptureTrafficBaseline(now);
    }

    [ContextMenu("Reiniciar medicion local")]
    public void ResetTrafficMeasurement()
    {
        Statistics = default;
        HasTrafficSample = false;
        measuredBytesIn = measuredBytesOut = 0;
        measurementStartedAt = Time.realtimeSinceStartupAsDouble;
        if (statsPeer != null) CaptureTrafficBaseline(measurementStartedAt);
    }

    private void CaptureTrafficBaseline(double now)
    {
        sampleAt = now; sampleFrame = Time.frameCount;
        previousBytesIn = statsPeer.BytesIn; previousBytesOut = statsPeer.BytesOut;
        previousMessagesIn = statsPeer.TrafficStatsGameLevel.TotalIncomingMessageCount;
        previousMessagesOut = statsPeer.TrafficStatsGameLevel.TotalOutgoingMessageCount;
        previousResends = statsPeer.ResentReliableCommands;
    }

    private void StopMeasuringTraffic()
    {
        if (statsPeer == null) return;
        statsPeer.TrafficStatsEnabled = previousStatsEnabled;
        statsPeer = null;
    }

    private void OnDisable() { Restore(); }
    private void OnDestroy() { Restore(); }
}

#if UNITY_EDITOR
// El Inspector vive en este mismo archivo: al borrar el tuner se retira tambien su visor.
[UnityEditor.CustomEditor(typeof(MultiplayerTrafficTuner))]
internal sealed class MultiplayerTrafficTunerInspector : UnityEditor.Editor
{
    public override bool RequiresConstantRepaint() => Application.isPlaying;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        UnityEditor.EditorGUILayout.Space();
        UnityEditor.EditorGUILayout.LabelField("Estadisticas de esta instancia", UnityEditor.EditorStyles.boldLabel);
        var tuner = (MultiplayerTrafficTuner)target;
        if (!Application.isPlaying)
        {
            UnityEditor.EditorGUILayout.HelpBox("Durante la partida, selecciona MultiplayerPlayers(Clone) en la Hierarchy para ver la medicion. El prefab del panel Project no muestra las estadisticas de su instancia.", UnityEditor.MessageType.Info);
            return;
        }
        if (!tuner.IsMeasuringTraffic)
        {
            UnityEditor.EditorGUILayout.HelpBox("Activa este componente, su coordinador y Medir trafico durante Play para iniciar la medicion.", UnityEditor.MessageType.Info);
            return;
        }
        if (tuner.HasTrafficSample)
        {
            MultiplayerTrafficTuner.TrafficSnapshot s = tuner.Statistics;
            using (new UnityEditor.EditorGUI.DisabledScope(true))
            {
                UnityEditor.EditorGUILayout.TextField("Estado de conexion", s.connectionState);
                UnityEditor.EditorGUILayout.DoubleField("Medicion (segundos)", s.elapsedSeconds);
                UnityEditor.EditorGUILayout.DoubleField("Enviados (kB/s)", s.sentKBps);
                UnityEditor.EditorGUILayout.DoubleField("Recibidos (kB/s)", s.receivedKBps);
                UnityEditor.EditorGUILayout.DoubleField("Mensajes enviados/s", s.sentMessagesPerSecond);
                UnityEditor.EditorGUILayout.DoubleField("Mensajes recibidos/s", s.receivedMessagesPerSecond);
                UnityEditor.EditorGUILayout.DoubleField("Mensajes totales/s", s.sentMessagesPerSecond + s.receivedMessagesPerSecond);
                UnityEditor.EditorGUILayout.DoubleField("Acumulado enviado (MB)", s.sentMB);
                UnityEditor.EditorGUILayout.DoubleField("Acumulado recibido (MB)", s.receivedMB);
                UnityEditor.EditorGUILayout.IntField("Ping al servidor (ms)", s.pingMs);
                UnityEditor.EditorGUILayout.IntField("Variacion del ping (ms)", s.pingVariationMs);
                UnityEditor.EditorGUILayout.DoubleField("Retransmisiones/s", s.resendsPerSecond);
                UnityEditor.EditorGUILayout.IntField("Cola de salida", s.queuedOutgoing);
                UnityEditor.EditorGUILayout.IntField("Cola de entrada fiable", s.queuedReliableIncoming);
                UnityEditor.EditorGUILayout.DoubleField("FPS de la instancia", s.framesPerSecond);
            }
        }
        else UnityEditor.EditorGUILayout.HelpBox("Esperando la primera muestra (1 segundo).", UnityEditor.MessageType.Info);

        if (GUILayout.Button("Reiniciar medicion local")) tuner.ResetTrafficMeasurement();
        UnityEditor.EditorGUILayout.HelpBox("Lecturas locales cada segundo; suman todo el trafico Photon de esta instancia, no toda la sala. Los bytes excluyen cabeceras IP/UDP/TCP y no equivalen a una factura. El boton reinicia solo esta medicion, sin cambiar los Hz ni los contadores globales.", UnityEditor.MessageType.Info);
    }
}
#endif
