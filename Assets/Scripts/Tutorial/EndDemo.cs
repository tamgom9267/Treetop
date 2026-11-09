using UnityEngine;
using UnityEngine.SceneManagement;

public class EndDemo : MonoBehaviour
{
    public void GoToMainMenu()
    {
        SceneManager.LoadScene("StartMenuScene");
    }
}
