using UnityEngine;

public class Area5 : MonoBehaviour
{
    [Header("Puertas de esta area")]

    [SerializeField] private GameObject entranceDoor;
    [SerializeField] private GameObject exitDoor;

    [Header("Enemigos de esta area")]

    [SerializeField] private GameObject boss;
    [SerializeField] private GameObject[] enemies;

    [Header("Estados del cuarto")]

    public bool isActive;
    public bool isCompleted;

    [Header("Hint")]

    [SerializeField] private HintController hintController;

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

        // Mostramos el objetivo de el cuarto.
        hintController.ActivateHint("Objetivo:\nUtiliza todo lo aprendido para derrotar al jefe de la zona y completar el tutorial.");

        // Mientras se cimpleta el objetivo, las puertas se mantienen cerradas
        CloseDoor(entranceDoor);
        CloseDoor(exitDoor);
    }

    private void Update()
    {
        // Si no esta activo se sale
        if (!isActive || isCompleted)
            return;

        // Mientras el boss exista, el cuarto no puede terminar.
        if (boss != null)
            return;

        // Si al menos un enemigo sigue existiendo el cuarto no se completa
        foreach (GameObject enemy in enemies)
        {
            if (enemy != null)
                return;
        }

        // Si todos son nullos
        CompleteArea();
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
