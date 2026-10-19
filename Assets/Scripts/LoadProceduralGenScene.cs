using UnityEngine;
using UnityEngine.SceneManagement;

public class LoadProceduralGenScene : MonoBehaviour
{
    private void OnCollisionEnter(Collision collision)
    {
        SceneManager.LoadScene("GameScene");
    }
}
