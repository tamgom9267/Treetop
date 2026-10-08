using UnityEngine;
using UnityEngine.SceneManagement;

public class EndToAldea : MonoBehaviour
{
    private void OnTriggerEnter()
    {
        SceneManager.LoadScene("VillageMenuScene");
    }
}
