using UnityEngine;

/// <summary>Solo modifica clones en memoria, mientras su contenedor esta inactivo.</summary>
public static class MultiplayerPrefabAdapter
{
    public static HostGeneratedRoomController PrepareHostRoom(GameObject room, int enemyType, string enemyPath)
    {
        HostGeneratedRoomController controller = room.GetComponent<HostGeneratedRoomController>();
        if (controller == null) controller = room.AddComponent<HostGeneratedRoomController>();
        GeneratedRoomController original = room.GetComponent<GeneratedRoomController>();
        if (original != null) controller.spawners = original.spawners ?? new GameObject[0];
        controller.Configure(enemyType, enemyPath);
        RemoveLegacyRoomComponents(room);
        return controller;
    }

    public static ClientGeneratedRoomController PrepareClientRoom(GameObject room, int enemyType, string enemyPath)
    {
        ClientGeneratedRoomController controller = room.GetComponent<ClientGeneratedRoomController>();
        if (controller == null) controller = room.AddComponent<ClientGeneratedRoomController>();
        GeneratedRoomController original = room.GetComponent<GeneratedRoomController>();
        if (original != null) controller.spawners = original.spawners ?? new GameObject[0];
        controller.Configure(enemyType, enemyPath);
        RemoveLegacyRoomComponents(room);
        return controller;
    }

    private static void RemoveLegacyRoomComponents(GameObject room)
    {
        foreach (RoomTriggerForwarder forwarder in room.GetComponentsInChildren<RoomTriggerForwarder>(true))
        { forwarder.enabled = false; Object.Destroy(forwarder); }
        foreach (GeneratedRoomController controller in room.GetComponentsInChildren<GeneratedRoomController>(true))
        { controller.enabled = false; Object.Destroy(controller); }
        foreach (StartingRoomSpawns spawn in room.GetComponentsInChildren<StartingRoomSpawns>(true))
        { spawn.enabled = false; Object.Destroy(spawn); }
    }

    public static MultiplayerStartingRoomSpawns PrepareStartingRoom(GameObject room)
    {
        MultiplayerStartingRoomSpawns spawn = room.GetComponent<MultiplayerStartingRoomSpawns>();
        if (spawn == null) spawn = room.AddComponent<MultiplayerStartingRoomSpawns>();
        spawn.FindLegacyPoints();
        return spawn;
    }

    public static void PreparePortal(GameObject portal, bool host)
    {
        foreach (FloorPortal original in portal.GetComponentsInChildren<FloorPortal>(true))
        { original.enabled = false; Object.Destroy(original); }
        if (host)
        {
            if (portal.GetComponent<HostFloorPortal>() == null) portal.AddComponent<HostFloorPortal>();
        }
        else
            foreach (HostFloorPortal hostPortal in portal.GetComponentsInChildren<HostFloorPortal>(true))
            { hostPortal.enabled = false; Object.Destroy(hostPortal); }
    }
}
