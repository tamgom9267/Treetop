using UnityEngine;

public class TutorialController : MonoBehaviour
{
    [Header("Areas del tutorial")]

    [SerializeField] private Area1 area1;
    [SerializeField] private Area2 area2;
    [SerializeField] private Area3 area3;
    [SerializeField] private Area4 area4;
    [SerializeField] private Area5 area5;

    [Header("Estados de desbloqueo")]
    private bool area2Unlocked;
    private bool area3Unlocked;
    private bool area4Unlocked;
    private bool area5Unlocked;

    private void Start()
    {
        // Bloqueamos todo
        area2Unlocked = false;
        area3Unlocked = false;
        area4Unlocked = false;
        area5Unlocked = false;
    }

    private void Update()
    {
        // Area 1 completada: desbloquea Area 2.
        if (area1 != null && area1.isCompleted)
            UnlockArea2();

        // Area 2 completada: desbloquea Area 3.
        if (area2 != null && area2.isCompleted)
            UnlockArea3();

        // Area 3 completada: desbloquea Area 4.
        if (area3 != null && area3.isCompleted)
            UnlockArea4();

        // Area 4 completada: desbloquea Area 5.
        if (area4 != null && area4.isCompleted)
            UnlockArea5();
    }

    private void UnlockArea2()
    {
        // Evita desbloquear el area 2 otra vez
        if (area2Unlocked)
            return;

        // define el area 2 como desbloqueada
        area2Unlocked = true;

        // Abrimos la puerta de entrada al area 2
        area2.OpenEntranceDoor();
    }

    private void UnlockArea3()
    {
        // Evita desbloquear el area 3 otra vez
        if (area3Unlocked)
            return;

        // define el area 3 como desbloqueada
        area3Unlocked = true;

        // Abrimos la puerta de entrada al area 3
        area3.OpenEntranceDoor();
    }

    private void UnlockArea4()
    {
        // Evita desbloquear el area 4 otra vez
        if (area4Unlocked)
            return;

        // define el area 4 como desbloqueada
        area4Unlocked = true;

        // Abrimos la puerta de entrada al area 4
        area4.OpenEntranceDoor();
    }

    private void UnlockArea5()
    {
        // Evita desbloquear el area 5 otra vez
        if (area5Unlocked)
            return;

        // define el area 5 como desbloqueada
        area5Unlocked = true;

        // Abrimos la puerta de entrada al area 5
        area5.OpenEntranceDoor();
    }
}
