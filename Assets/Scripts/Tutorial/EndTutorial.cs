using UnityEngine;
using UnityEngine.SceneManagement;

public class EndTutorial : MonoBehaviour
{
    private void OnTriggerEnter()
    {
        SceneManager.LoadScene("EndDemo");
    }
}
