using UnityEngine;

public class LuzFogata : MonoBehaviour
{
    [SerializeField] private Light luz;
    [SerializeField] private float intensidadBase = 20.0f;
    [SerializeField] private float variacion = 2.5f;
    [SerializeField] private float velocidad = 2f;

    private float semilla;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (luz == null)
        {
            luz = GetComponent<Light>();
        }

        semilla = Random.Range(0f, 100f);
    }

    // Update is called once per frame
    void Update()
    {
        if (luz == null) { return; }

        // perlin noise genera un numero entre 0 y 1
        // el ruido se multiplica por la variacion y se suma a la intensidad base para obtener la intensidad final de la luz
        float ruido = Mathf.PerlinNoise(semilla, Time.time * velocidad);

        luz.intensity = intensidadBase + (ruido - 0.5f) * 2f * variacion;
    }
}
