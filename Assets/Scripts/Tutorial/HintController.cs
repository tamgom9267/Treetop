using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class HintController : MonoBehaviour
{
    [Header("Canvas")]
    [SerializeField] private GameObject HintCanvas;

    [SerializeField] private GameObject HealthCanvas;

    [Header("Texto")]

    // Texto de instucciones en HintCanvas
    [SerializeField] private TMP_Text hintText;

    private InputAction CloseHintAction;
    private bool hintActive;


    private void Start()
    {
        // Buscamos accion de NIP
        CloseHintAction = InputSystem.actions.FindAction("Player/Close Hint (Tutorial)");

        // Apagamos el canvas de Hint
        HintCanvas.SetActive(false);

        // Definimos que no esta activo para la logica.
        hintActive = false;
    }

    private void Update()
    {
        if (!hintActive)
            return;
        
        // Al presionar espacio cerramos hint
        if (CloseHintAction.WasPressedThisFrame())
        {
            DeactivateHint();
        }
    }

    public void ActivateHint(string newtext)
    {
        // Cambiamos el texto de el hint
        hintText.text = newtext;

        // Marcamos el hint como activo
        hintActive = true;

        // Detenemos el timepo para que no haya problemas con la escena
        Time.timeScale = 0f;

        // Ocultamos la barra de vida
        HealthCanvas.SetActive(false);

        // Mostramos el hint
        HintCanvas.SetActive(true);
    }

    private void DeactivateHint()
    {
        // Marcamos el hint como inactivo
        hintActive = false;

        // Detenemos el timepo para que no haya problemas con la escena
        Time.timeScale = 1f;

        // Ocultamos la barra de vida
        HealthCanvas.SetActive(true);

        // Mostramos el hint
        HintCanvas.SetActive(false);
    }
}
