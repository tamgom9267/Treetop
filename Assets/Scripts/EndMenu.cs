using UnityEngine;
using UnityEngine.SceneManagement;

public class EndMenu : MonoBehaviour
{
    public void GoToMainMenu()
    {
        SceneManager.LoadScene("StartMenuScene");
    }
}