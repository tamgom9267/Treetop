using UnityEngine;

public class Lluvia : MonoBehaviour
{
    [SerializeField] private Transform targetCam;

    private void LateUpdate()
    {
        if(targetCam == null) return;

        Vector3 newPos = targetCam.position;
        newPos.y = transform.position.y;

        transform.position = newPos;
    }
}
