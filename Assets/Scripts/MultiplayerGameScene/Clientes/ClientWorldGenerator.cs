using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Generacion del cliente. Configurar rutas sin el prefijo Assets/Resources.</summary>
public class ClientWorldGenerator : MonoBehaviour
{
    [SerializeField] private string roomsPath = "GeneracionDeCuartos/Cuartos";
    [SerializeField] private string wallsPath = "GeneracionDeCuartos/Paredes";
    [SerializeField] private string doorsPath = "GeneracionDeCuartos/Puertas";
    [SerializeField] private string bridgesPath = "GeneracionDeCuartos/Puentes";
    [SerializeField] private string portalPath = "GeneracionDeCuartos/Portal/Portal";
    [SerializeField] private string enemiesPath = "GeneracionDeCuartos/Enemigos";
    private GameObject generatedWorld;
    private bool building;
    private string lastBuildKey;
    private MultiplayerRunSession session;

    private void Start()
    {
        session = MultiplayerRunSession.Instance;
        if (session == null || !session.IsRunning) { MultiplayerRunSession.ReturnInvalidSceneToVillage(); return; }
        if (PhotonPartyService.Instance == null || PhotonPartyService.Instance.IsHost) { enabled = false; return; }
        session.RegisterClientGenerator(this);
    }

    public bool ValidateResources(MultiplayerFloorData data, out string error)
    {
        if (!MultiplayerMapMath.Validate(data, out error)) return false;
        string[] required =
        {
            wallsPath + "/ParedHorizontal", wallsPath + "/ParedVertical",
            wallsPath + "/ParedDePuertaHorizontal", wallsPath + "/ParedDePuertaVertical",
            doorsPath + "/PuertaHorizontal", doorsPath + "/PuertaVertical",
            bridgesPath + "/PuenteHorizontalCompleto", bridgesPath + "/PuenteVerticalCompleto",
            portalPath, enemiesPath + "/Enemigo1", enemiesPath + "/Enemigo2"
        };
        foreach (string path in required)
            if (Resources.Load<GameObject>(path) == null) { error = "No se encontro el prefab: " + path; return false; }
        foreach (MultiplayerRoomData room in data.rooms)
        {
            GameObject prefab = Resources.Load<GameObject>(roomsPath + "/" + room.roomID);
            if (prefab == null) { error = "No se encontro el cuarto: " + roomsPath + "/" + room.roomID; return false; }
            if (prefab.GetComponent<GeneratedRoomController>() == null && prefab.GetComponent<ClientGeneratedRoomController>() == null)
            { error = "El cuarto " + room.roomID + " no tiene un controlador compatible."; return false; }
        }
        error = string.Empty;
        return true;
    }

    public void BuildFloor(MultiplayerFloorData data)
    {
        string key = MultiplayerMapMath.Key(data.runId, data.floorNumber);
        if (building || lastBuildKey == key) return;
        building = true;
        lastBuildKey = key;
        StartCoroutine(BuildRoutine(data));
    }

    private IEnumerator BuildRoutine(MultiplayerFloorData data)
    {
        if (!ValidateResources(data, out string error)) { BuildFailed(error); yield break; }
        GameObject nextWorld = new GameObject("GeneratedMultiplayerWorld_Client_" + data.floorNumber);
        nextWorld.SetActive(false);
        MultiplayerStartingRoomSpawns spawn = null;
        bool constructed = false;
        try { spawn = ConstructWorld(data, nextWorld.transform); constructed = true; }
        catch (Exception exception) { error = "No se pudo construir el piso: " + exception.Message; }
        if (!constructed) { Destroy(nextWorld); BuildFailed(error); yield break; }

        // Destroy es diferido: no activar hasta que desaparezcan los componentes solitarios.
        yield return null;
        if (session == null || !session.IsRunning || data.runId != session.RunId || data.floorNumber != session.FloorNumber)
        { Destroy(nextWorld); building = false; yield break; }
        GameObject previous = generatedWorld;
        if (previous != null) previous.SetActive(false);
        ClearFloorLoot();
        generatedWorld = nextWorld;
        nextWorld.SetActive(true);
        if (spawn == null || !spawn.PlaceLocalPlayer(out error))
        {
            if (previous != null) Destroy(previous);
            BuildFailed(error ?? "No se encontro el cuarto inicial.");
            yield break;
        }
        if (previous != null) Destroy(previous);
        yield return null; // Awake/Start, colliders y puntos de aparicion preparados antes del ACK.
        building = false;
        session.ReportWorldReady(data.runId, data.floorNumber);
    }

    private MultiplayerStartingRoomSpawns ConstructWorld(MultiplayerFloorData data, Transform root)
    {
        MultiplayerDirection? previousExit = null;
        MultiplayerStartingRoomSpawns spawn = null;
        foreach (MultiplayerRoomData room in data.rooms)
        {
            GameObject prefab = Required(roomsPath + "/" + room.roomID);
            GameObject instance = Instantiate(prefab, room.gridPosition, Quaternion.identity, root);
            ClientGeneratedRoomController controller = MultiplayerPrefabAdapter.PrepareClientRoom(instance, room.enemyType, enemiesPath);
            if (room.roomID == "0") spawn = MultiplayerPrefabAdapter.PrepareStartingRoom(instance);
            MultiplayerDirection? entry = previousExit.HasValue ? MultiplayerMapMath.Opposite(previousExit.Value) : (MultiplayerDirection?)null;
            for (int direction = 0; direction < 4; direction++)
            {
                MultiplayerDirection side = (MultiplayerDirection)direction;
                bool horizontal = MultiplayerMapMath.IsHorizontalWall(side);
                Vector3 position = room.gridPosition + MultiplayerMapMath.WallOffset(side);
                if (side == room.exitDirection || entry == side)
                {
                    Instantiate(Required(wallsPath + (horizontal ? "/ParedDePuertaHorizontal" : "/ParedDePuertaVertical")), position, Quaternion.identity, root);
                    GameObject door = Instantiate(Required(doorsPath + (horizontal ? "/PuertaHorizontal" : "/PuertaVertical")),
                        position + new Vector3(0f, 2.2f, 0f), Quaternion.identity, root);
                    if (side == room.exitDirection) controller.exitDoor = door;
                    else controller.enterDoor = door;
                }
                else Instantiate(Required(wallsPath + (horizontal ? "/ParedHorizontal" : "/ParedVertical")), position, Quaternion.identity, root);
            }
            bool verticalBridge = MultiplayerMapMath.IsHorizontalWall(room.exitDirection);
            Vector3 bridgePosition = (Vector3)room.gridPosition + (Vector3)MultiplayerMapMath.Offset(room.exitDirection) / 2f;
            Instantiate(Required(bridgesPath + (verticalBridge ? "/PuenteVerticalCompleto" : "/PuenteHorizontalCompleto")), bridgePosition, Quaternion.identity, root);
            previousExit = room.exitDirection;
        }
        MultiplayerRoomData last = data.rooms[data.rooms.Count - 1];
        MultiplayerDirection portalSide = MultiplayerMapMath.Opposite(last.exitDirection);
        Vector3 portalPosition = last.gridPosition + MultiplayerMapMath.Offset(last.exitDirection) +
            MultiplayerMapMath.WallOffset(portalSide) + new Vector3(0f, 2.2f, 0f);
        Quaternion rotation = MultiplayerMapMath.IsHorizontalWall(portalSide) ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity;
        GameObject portal = Instantiate(Required(portalPath), portalPosition, rotation, root);
        MultiplayerPrefabAdapter.PreparePortal(portal, false);
        return spawn;
    }

    private static GameObject Required(string path)
    {
        GameObject prefab = Resources.Load<GameObject>(path);
        if (prefab == null) throw new InvalidOperationException("Falta el recurso " + path);
        return prefab;
    }

    private void BuildFailed(string error)
    {
        building = false;
        if (session != null) session.ReportBuildFailed(error);
        else Debug.LogError(error, this);
    }

    private static void ClearFloorLoot()
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            if (root.name == "Loot")
            {
                foreach (Transform item in root.transform) item.gameObject.SetActive(false);
                foreach (Transform item in root.transform) Destroy(item.gameObject);
            }
    }
}
