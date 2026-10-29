using System.Collections.Generic;
using UnityEngine;

public class GeneratedRoomController : MonoBehaviour
{
    public int enemyType;
    public GameObject[] spawners;
    public List<GameObject> enemies = new List<GameObject>();

    private GameObject enemy1;
    private GameObject enemy2;

    private int enemyCounter;

    public GameObject enterDoor;
    public GameObject exitDoor;
    
    public bool isActive = false;
    public bool isCompleted = false;

    public bool activateTemporal = false;

    void Awake()
    {
        Transform plane = transform.Find("Plano");
        bool hasTrigger = false;
        if (plane != null)
        {
            foreach (Collider planeCollider in plane.GetComponents<Collider>())
            {
                if (planeCollider.isTrigger && planeCollider.enabled)
                {
                    hasTrigger = true;
                    break;
                }
            }
        }

        if (hasTrigger)
        {
            RoomTriggerForwarder forwarder = plane.GetComponent<RoomTriggerForwarder>();
            if (forwarder == null)
                forwarder = plane.gameObject.AddComponent<RoomTriggerForwarder>();

            forwarder.Initialize(this);
        }
        else
        {
            Debug.LogWarning("No se encontro un trigger en el Plano del cuarto.", this);
        }

        enemy1 = Resources.Load<GameObject>(
            "GeneracionDeCuartos/Enemigos/Enemigo1"
        );

        enemy2 = Resources.Load<GameObject>(
            "GeneracionDeCuartos/Enemigos/Enemigo2"
        );

        if (enemy1 == null || enemy2 == null)
        {
            Debug.LogError("No se encontro Enemigo1 o Enemigo2 en Resources.");
            return;
        }
    }

    private void Start()
    {
        OpenDoor(enterDoor);
        CloseDoor(exitDoor);
    }

    private void Update()
    {
        if (activateTemporal)
        {
            activateTemporal = false;
            ActivateRoom();
        }
        
        if (!isActive || isCompleted)
            return;

        // Si todavía existe al menos un enemigo, el cuarto sigue activo.
        foreach (GameObject enemy in enemies)
        {
            if (enemy != null)
                return;
        }

        CompleteRoom();
    }

    private void OnTriggerEnter(Collider other)
    {
        HandleTriggerEnter(other);
    }

    public void HandleTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        ActivateRoom();
    }
    
    public void ActivateRoom()
    {
        // Un cuarto activo o terminado no puede activarse otra vez.
        if (isActive || isCompleted)
            return;

        isActive = true;

        // Al comenzar el combate, se cierran ambas puertas.
        CloseDoor(enterDoor);
        CloseDoor(exitDoor);
        SpawnEnemies();
    }

    private void CompleteRoom()
    {
        isCompleted = true;
        isActive = false;

        // Al terminar, se abren las puertas permanentemente.
        OpenDoor(enterDoor);
        OpenDoor(exitDoor);
    }

    private void OpenDoor(GameObject door)
    {
        if (door != null)
            door.SetActive(false);
    }

    private void CloseDoor(GameObject door)
    {
        if (door != null)
            door.SetActive(true);
    }

    private void SpawnEnemies()
    {
        GameObject enemyPrefab = null;

        if (enemyType == 1)
            enemyPrefab = enemy1;
        else if (enemyType == 2)
            enemyPrefab = enemy2;
        else
            return;

        foreach (GameObject spawner in spawners)
        {
            if (spawner == null)
                continue;

            enemyCounter++;

            GameObject newEnemy = Instantiate(
                enemyPrefab,
                spawner.transform.position,
                spawner.transform.rotation,
                transform
            );

            newEnemy.name = "enemy" + enemyType + "(" + enemyCounter + ")";

            enemies.Add(newEnemy);
        }
    }
}
