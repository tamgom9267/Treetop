using UnityEngine;

public class Area2 : MonoBehaviour
{
    [Header("Puertas de esta area")]

    [SerializeField] private GameObject entranceDoor;
    [SerializeField] private GameObject exitDoor;

    [Header("Objetivo de esta area")]

    [SerializeField] private Chest chest;

    [Header("Estados del cuarto")]

    // El jugador yya completo el objetivo
    public bool isActive;

    // El cuarto ya fue completado
    public bool isCompleted;

    private void Start()
    {
        // Inicializamos los estados del cuarto
        isActive = false;
        isCompleted = false;

        // Al comenzar, las puertas se mantienen cerradas
        CloseDoor(entranceDoor);
        CloseDoor(exitDoor);
    }

    // TutorialController llama a este metodo para abrir la puerta de entrada al cuarto
    public void OpenEntranceDoor()
    {
        if (isActive || isCompleted)
            return;
        
        OpenDoor(entranceDoor);
    }

    private void OnTriggerEnter(Collider other)
    {
        // Solo el Player puede activar el cuarto
        if (!other.CompareTag("Player"))
            return;
        
        StartArea();
    }

    private void StartArea()
    {
        // Evita iniciar el cuarto otra vez si ya esta activo o si ya fue completado
        if (isActive || isCompleted)
            return;

        // Activamos el cuarto
        isActive = true;

        // Mientras se cimpleta el objetivo, las puertas se mantienen cerradas
        CloseDoor(entranceDoor);
        CloseDoor(exitDoor);
    }

    private void Update()
    {
        // Solo revisamos el cofre mientras el cuarto esta activo
        if (!isActive || isCompleted)
            return;

        // El cofre cambia chestInteracted a true al ser tocado
        if (chest.chestInteracted)
        {
            CompleteArea();
        }
    }

    private void CompleteArea()
    {
        // el objetivo fue completado
        isActive = false;
        isCompleted = true;

        // Se abren todas las puertas
        OpenDoor(entranceDoor);
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
        // activamos la puerta
        if (door != null)
            door.SetActive(true);
    }
}