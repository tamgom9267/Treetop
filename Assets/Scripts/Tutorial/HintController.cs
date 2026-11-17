using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class HintController : MonoBehaviour
{
    [Header("Canvas")]
    [SerializeField] private GameObject HintCanvas;

    [SerializeField] private GameObject HealthCanvas;

    [Header("Texto")]

    // Texto de instucciones en HintCanvas
    [SerializeField] private TMP_Text hintText;

    [SerializeField] Slider TimeValue;

    private InputAction CloseHintAction;
    private bool hintActive;

    float value = 5.0f;


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

        value -= 1f * Time.deltaTime;
        TimeValue.value = value;
        

        if(value <= 0)
        {
            DeactivateHint();
        }
    }

    public void ActivateHint(string newtext)
    {
        value = 5;
        // Cambiamos el texto de el hint
        hintText.text = newtext;

        // Marcamos el hint como activo
        hintActive = true;

        // Mostramos el hint
        HintCanvas.SetActive(true);
    }

    private void DeactivateHint()
    {
        // Marcamos el hint como inactivo
        hintActive = false;

        // Mostramos el hint
        HintCanvas.SetActive(false);
    }
}
