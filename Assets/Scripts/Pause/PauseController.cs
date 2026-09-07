using UnityEngine;
using UnityEngine.InputSystem;

public class PauseController : MonoBehaviour
{
    [SerializeField] private GameObject CanvasHealthBar;
    [SerializeField] private GameObject CanvasPause;
    private InputAction PauseAction;
    private bool isPaused = false;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        PauseAction = InputSystem.actions.FindAction("Player/Pause");

        // Al iniciar el juego, el tiempo es normal
        Time.timeScale = 1f;

        // Mostramos el canvas de vida
        CanvasHealthBar.SetActive(true);
        
        // Ocultamos el canvas de pausa
        CanvasPause.SetActive(false);

    }

    void Update()
    {
        if (PauseAction.WasPressedThisFrame())
        {
            Debug.Log("ESC PRESIONADO");
            TogglePause();
        }
    }

    public void OnPause(InputValue value)
    {
        Debug.Log("OnPause fue llamado");
        // Solo hacemos algo cuando ESC es presionado
        if (value.isPressed)
        {
            Debug.Log("ESC PRESIONADO");
            TogglePause();
        }
    }

    public void TogglePause()
    {
        if (isPaused)
        {
            ResumeGame();
        }
        else
        {
            PauseGame();
        }
    }

    private void PauseGame()
    {
        // Indicamos que estamos pausados
        isPaused = true;

        // Detenemos el tiempo del juego
        Time.timeScale = 0f;

        // Escondemos la barra de vida
        CanvasHealthBar.SetActive(false);

        // Mostramos el menú de pausa
        CanvasPause.SetActive(true);
    }

    public void ResumeGame()
    {
        // Ya no estamos pausados
        isPaused = false;

        // Volvemos a activar el tiempo
        Time.timeScale = 1f;

        // Mostramos nuevamente la barra de vida
        CanvasHealthBar.SetActive(true);

        // Escondemos el menú de pausa
        CanvasPause.SetActive(false);
    }
}
