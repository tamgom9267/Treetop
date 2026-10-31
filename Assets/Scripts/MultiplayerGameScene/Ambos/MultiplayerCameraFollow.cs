using UnityEngine;

/// <summary>Agregar en MultiplayerGameScene. Utiliza la camara del personaje persistente.</summary>
public class MultiplayerCameraFollow : MonoBehaviour
{
    [SerializeField] private Vector3 offset = new Vector3(0f, 10f, -8f);
    [SerializeField] private Vector3 cameraRotation = new Vector3(45f, 0f, 0f);
    private Camera localCamera;
    private FollowPlayerNoRotation originalFollow;
    private bool originalEnabled;
    private bool captured;
    private Quaternion originalRotation;

    private void Start()
    {
        if (NewestSingleton.Instance == null) return;
        localCamera = NewestSingleton.Instance.GetComponentInChildren<Camera>(true);
        originalFollow = NewestSingleton.Instance.GetComponentInChildren<FollowPlayerNoRotation>(true);
        if (localCamera == null) { Debug.LogError("No se encontro la camara persistente del personaje.", this); return; }
        originalRotation = localCamera.transform.rotation;
        captured = true;
        if (originalFollow != null)
        {
            originalEnabled = originalFollow.enabled;
            originalFollow.enabled = false;
        }
        localCamera.transform.rotation = Quaternion.Euler(cameraRotation);
    }

    private void LateUpdate()
    {
        PlayerController player = MultiplayerStartingRoomSpawns.LocalPlayer;
        if (localCamera != null && player != null) localCamera.transform.position = player.transform.position + offset;
    }

    private void OnDestroy()
    {
        if (!captured) return;
        if (localCamera != null) localCamera.transform.rotation = originalRotation;
        if (originalFollow != null) originalFollow.enabled = originalEnabled;
    }
}
