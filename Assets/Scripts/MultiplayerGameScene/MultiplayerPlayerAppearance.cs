using JetBrains.Annotations;
using UnityEngine;

public class MultiplayerPlayerAppearance : MonoBehaviour
{
    [Header("Materiales del Jugador")]
    [SerializeField] private Material rojo;
    [SerializeField] private Material azul;
    [SerializeField] private Material blanco;

    [Header("Settings de Renderer")]
    [SerializeField] private string targetMaterialName = "Warrior_Cloth";

    private Renderer targetRend;
    private int materialIndex = -1;

    private Material original;


    [ContextMenu("Test Rojo")]
    private void TestRed() { ApplyRed(); }

    [ContextMenu("Test Azul")]
    private void TestBlue() { ApplyBlue(); }

    [ContextMenu("Test Blanco")]
    private void TestWhite() { ApplyWhite(); }

    [ContextMenu("Test Original")]
    private void TestRestore() { RestoreOriginalColor(); }


    private void Awake()
    {
        FindTargetMaterial();
    }

    private void FindTargetMaterial()
    {
        Renderer[] renderers  = GetComponentsInChildren<Renderer>(true);

        foreach (Renderer renderer in renderers)
        {
            Material[] materials = renderer.sharedMaterials;

            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] != null &&
                    materials[i].name == targetMaterialName)
                {
                    targetRend = renderer;
                    materialIndex = i;
                    original = materials[i];

                    return;
                }
            }
        }

        Debug.LogWarning(
            $"No se encontró el material {targetMaterialName} en {gameObject.name}"
        );
    }

    public void ApplyRed()
    {
        ApplyMaterial(rojo);
    }
    public void ApplyBlue()
    {
        ApplyMaterial(azul);
    }
    public void ApplyWhite()
    {
        ApplyMaterial(blanco);
    }

    private void ApplyMaterial(Material nuevo)
    {
        if (targetRend == null || nuevo == null) return;

        Material[] materials = targetRend.sharedMaterials;

        materials[materialIndex] = nuevo;

        targetRend.sharedMaterials = materials;
    }

    public void RestoreOriginalColor()
    {
        ApplyMaterial(original);
    }
}
