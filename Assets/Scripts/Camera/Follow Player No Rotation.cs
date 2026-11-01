using Unity.VisualScripting;
using UnityEngine;
using System.Collections;

public class FollowPlayerNoRotation : MonoBehaviour
{
    // Camara que queremos mover
    [SerializeField] private Camera cameraToFollow;

    // Personaje que la camara debe seguir
    [SerializeField] private Transform player;

    // Distancia y rotacion que queremos mantener entre la camara y el personaje
    [Header("Posicion de Camara")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 10f, -8f);
    [SerializeField] private Vector3 cameraRotation = new Vector3(45f, 0f, 0f);

    [UnitHeaderInspectable("ScreenShake")]
    [SerializeField] private float shakeDuration = 0.15f;
    [SerializeField] private float shakeMagnitude = 0.15f;

    private Vector3 shakeOffset;
    private Coroutine shakeCoroutine;

    private void Start()
    {
        cameraToFollow.transform.rotation = Quaternion.Euler(cameraRotation);
    }

    void LateUpdate()
    {
        // Calculamos la posicion donde deberia de estar la camara.
        Vector3 newPosition = player.position + offset;

        // Cambiamos la posicion de la camara
        cameraToFollow.transform.position = newPosition + shakeOffset;
    }

    public void Shake()
    {
        if (shakeCoroutine != null) StopCoroutine(shakeCoroutine);

        shakeCoroutine = StartCoroutine(ShakeRoutine());
    }

    private IEnumerator ShakeRoutine()
    {
        float elapsed = 0f;

        while (elapsed < shakeDuration)
        {
            float strength = 1f - (elapsed / shakeDuration);

            float x = Random.Range(-1f, 1f) * shakeMagnitude * strength;
            float y = Random.Range(-1f, 1f) * shakeMagnitude * strength;

            shakeOffset = new Vector3(x, y, 0f);

            elapsed += Time.unscaledDeltaTime;

            yield return null;
        }

        shakeOffset = Vector3.zero;
        shakeCoroutine = null;
    }
}
