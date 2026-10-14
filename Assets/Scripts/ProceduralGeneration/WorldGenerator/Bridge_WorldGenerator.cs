using UnityEngine;

public partial class WorldGenerator : MonoBehaviour
{
    // Genera el piso de todos los cuartos en la posicion adecuada
    private void GenerateBridge()
    {
        // Obtiene los prefabs
        GameObject puenteHorizontal = Resources.Load<GameObject>("GeneracionDeCuartos/Puentes/PuenteHorizontalCompleto");
        GameObject puenteVertical = Resources.Load<GameObject>("GeneracionDeCuartos/Puentes/PuenteVerticalCompleto");

        // Verifica que existe el prefab
        if (puenteHorizontal == null || puenteVertical == null)
        {
            Debug.LogError("No se encontro el prefab de el puente");
        }
        
        foreach (RoomData room in floorData.rooms)
        {
            if (room.exitDirection == Direction.North)
            {
                Vector3 bridgeLocation = room.gridPosition + new Vector3(0, 0, 41/2f);
                GameObject newBridge = Instantiate(puenteVertical, bridgeLocation, Quaternion.identity, generatedWorld.transform);
            }
            else if (room.exitDirection == Direction.East)
            {
                Vector3 bridgeLocation = room.gridPosition + new Vector3(57/2f, 0, 0);
                GameObject newBridge = Instantiate(puenteHorizontal, bridgeLocation, Quaternion.identity, generatedWorld.transform);
            }
            else if (room.exitDirection == Direction.South)
            {
                Vector3 bridgeLocation = room.gridPosition + new Vector3(0, 0, -41/2f);
                GameObject newBridge = Instantiate(puenteVertical, bridgeLocation, Quaternion.identity, generatedWorld.transform);
            }
            else if (room.exitDirection == Direction.West)
            {
                Vector3 bridgeLocation = room.gridPosition + new Vector3(-57/2f, 0, 0);
                GameObject newBridge = Instantiate(puenteHorizontal, bridgeLocation, Quaternion.identity, generatedWorld.transform);
            }
        }
    }
}
