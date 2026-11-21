using UnityEngine;
using UnityEngine.SceneManagement;

public class EndDemo : MonoBehaviour
{
    private void OnTriggerEnter()
    {
        SceneManager.LoadScene("EndDemo");
    }
}
