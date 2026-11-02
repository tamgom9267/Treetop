using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class PauseController : MonoBehaviour
{
    [SerializeField] private GameObject CanvasHealthBar;
    [SerializeField] private GameObject CanvasPause;
    private InputAction PauseAction;
    private bool isPaused = false;
    private bool externalUIBlocked;

    public void SetExternalUIBlocked(bool blocked)
    {
        externalUIBlocked = blocked;
    }

    private void OnEnable()
    {
        // PlayerInput deshabilita su mapa al destruir el personaje anterior.
        // La pausa usa una accion propia para sobrevivir a ese reemplazo.
        if (PauseAction == null)
            PauseAction = InputSystem.actions.FindAction("Player/Pause", true).Clone();
        PauseAction.Enable();
    }

    private void OnDisable()
    {
        PauseAction?.Disable();
    }

    private void OnDestroy()
    {
        PauseAction?.Dispose();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
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
        if (externalUIBlocked) return;

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

        // Escondemos la barra de vida
        CanvasHealthBar.SetActive(false);

        // Mostramos el menu de pausa
        CanvasPause.SetActive(true);
    }

    public void ResumeGame()
    {
        // Ya no estamos pausados
        isPaused = false;

        // Mostramos nuevamente la barra de vida
        CanvasHealthBar.SetActive(true);

        // Escondemos el menu de pausa
        CanvasPause.SetActive(false);
    }

    public void GoToMainMenu()
    {
        SceneManager.LoadScene("StartMenuScene");
    }
}
