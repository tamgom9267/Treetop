using UnityEngine;

public class TutorialController : MonoBehaviour
{
    [Header("Areas del tutorial")]

    [SerializeField] private Area1 area1;
    [SerializeField] private Area2 area2;

    private bool area2Unlocked;

    private void Start()
    {
        // Bloqueamos todo
        area2Unlocked = false;
    }

    private void Update()
    {
        // Si area 1 se completo
        if (area1.isCompleted)
            UnlockArea2();
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
}
