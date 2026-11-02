using System;
using ExitGames.Client.Photon;
using Photon.Pun;
using UnityEngine;

/// <summary>Medidor local. Las frecuencias permanentes pertenecen a MultiplayerPlayers.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(MultiplayerPlayers))]
[DefaultExecutionOrder(-200)]
[AddComponentMenu("Multiplayer/Medidor de trafico")]
public sealed class MultiplayerTrafficTuner : MonoBehaviour
{
    // Ajustes retirados: no volver a serializar ni aplicar valores guardados del tuner.
    // [SerializeField] private MultiplayerPlayers.TrafficRates rates;
    // [SerializeField] private int photonSendRateHz;

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
    private PhotonPeer statsPeer;
    private bool previousStatsEnabled;
    private double sampleAt, measurementStartedAt;
    private int sampleFrame, previousMessagesIn, previousMessagesOut, previousResends;
    private long previousBytesIn, previousBytesOut, measuredBytesIn, measuredBytesOut;

    private void OnEnable()
    {
        if (!Application.isPlaying) return;
        players = GetComponent<MultiplayerPlayers>();
        SampleTraffic();
    }

    private void Update() { SampleTraffic(); }

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

    private void OnDisable() { StopMeasuringTraffic(); }
    private void OnDestroy() { StopMeasuringTraffic(); }
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
        MultiplayerPlayers.TrafficRates rates = MultiplayerPlayers.TrafficRates.Defaults();
        UnityEditor.EditorGUILayout.LabelField("Frecuencias fijas por codigo", UnityEditor.EditorStyles.boldLabel);
        using (new UnityEditor.EditorGUI.DisabledScope(true))
        {
            UnityEditor.EditorGUILayout.FloatField("Jugadores (Hz)", rates.playerStateSendHz);
            UnityEditor.EditorGUILayout.FloatField("Enemigos (Hz)", rates.enemyStateSendHz);
            UnityEditor.EditorGUILayout.FloatField("Comprobar equipo (Hz)", rates.equipmentCheckHz);
            UnityEditor.EditorGUILayout.FloatField("Presencia por cuarto (Hz)", rates.roomPresenceSendHz);
            UnityEditor.EditorGUILayout.FloatField("Solicitar mundo (Hz)", rates.worldSnapshotRequestHz);
            UnityEditor.EditorGUILayout.FloatField("Escanear loot (Hz)", rates.lootScanHz);
            UnityEditor.EditorGUILayout.FloatField("Solicitar loot (Hz)", rates.lootSnapshotRequestHz);
            UnityEditor.EditorGUILayout.FloatField("Reintentar loot (Hz)", rates.lootRetryHz);
            UnityEditor.EditorGUILayout.IntField("Salida solicitada (Hz)", MultiplayerPlayers.PhotonSendRateHz);
            if (Application.isPlaying) UnityEditor.EditorGUILayout.IntField("Salida expuesta por Photon (Hz)", PhotonNetwork.SendRate);
        }
        UnityEditor.EditorGUILayout.HelpBox("Objetivos limitados por los FPS y el transporte. Photon convierte 120 Hz en 8 ms; puede mostrar 125 Hz. El medidor no modifica estas frecuencias.", UnityEditor.MessageType.Info);
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
