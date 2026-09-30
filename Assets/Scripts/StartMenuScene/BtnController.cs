using UnityEngine;
using UnityEngine.SceneManagement;

public class BtnController : MonoBehaviour
{
    public void BtnStart()
    {
        SceneManager.LoadScene("TutorialScene");
    }

    public void BtnExit()
    {
        Application.Quit();
    }
}
