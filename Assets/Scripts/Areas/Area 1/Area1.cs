using UnityEngine;
using UnityEngine.InputSystem;

public class Area1 : MonoBehaviour
{
    [Header("Puertas de esta area")]

    // Puertas de la area
    [SerializeField] private GameObject exitDoor;

    [Header("Estados del cuarto")]

    // Estados de el cuarto publicos para manejarlos en TutorialController.
    public bool isActive;
    public bool isCompleted;

    // Verificadores - Guardan si el jugador presiono cada tecla
    private bool presedW;
    private bool presedA;
    private bool presedS;
    private bool presedD;

    private void Start()
    {
        // Inicializamos los estados del cuarto
        isActive = false;
        isCompleted = false;

        // Inicializamos los verificadores
        presedW = false;
        presedA = false;
        presedS = false;
        presedD = false;

        // Al comenzar, las puertas se mantienen cerradas
        CloseDoor(exitDoor);
    }

    private void OnTriggerEnter(Collider other)
    {  
        // Solo inicamos el cuarto si el jugador "Player" entra.
        if (!other.CompareTag("Player"))
            return;

        StartArea();
    }

    public void StartArea()
    {
        // Evista iniciar el cuarto otra vez si ya esta activo o si ya fue completado
        if (isActive || isCompleted)
            return;

        // Activamos el cuarto
        isActive = true;

        // Mientras se cimpleta el objetivo, las puertas se mantienen cerradas
        CloseDoor(exitDoor);
    }

    private void Update()
    {
        // Solo revisamos si las teclas estan activas
        if (!isActive || isCompleted || Keyboard.current == null)
            return;
        
        // si se presiona una tecla se guarda como completada
        if (Keyboard.current.wKey.wasPressedThisFrame)
            presedW = true;

        if (Keyboard.current.aKey.wasPressedThisFrame)
            presedA = true;

        if (Keyboard.current.sKey.wasPressedThisFrame)
            presedS = true;

        if (Keyboard.current.dKey.wasPressedThisFrame)
            presedD = true;
        
        // Si ya se presionaron todas las teclas
        if (presedW && presedA && presedS && presedD)
            CompleteArea();
    }

    private void CompleteArea()
    {
        // El objeto deja de estar activo y el cuarto queda terminado.
        isActive = false;
        isCompleted = true;

        // Al terminar el cuarto, se abren las puertas
        OpenDoor(exitDoor);
    }

    private void OpenDoor(GameObject door)
    {
        // desactivamos la puerta
        if (door != null)
            door.SetActive(false);
    }

    private void CloseDoor(GameObject door)
    {
        // Activamos la puerta
        if (door != null)
            door.SetActive(true);
    }
}
