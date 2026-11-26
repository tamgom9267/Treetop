using UnityEngine;
using UnityEngine.SceneManagement;

public class BtnController : MonoBehaviour
{
    [Header("Creditos")]
    [SerializeField] private GameObject creditsPanel;
    [SerializeField] private GameObject[] creditsPage;

    private int currentPage = 0;

    public void Start()
    {
        creditsPanel.SetActive(false);
    }

    public void BtnStart()
    {
        SceneManager.LoadScene("TutorialScene");
    }

    public void BtnExit()
    {
        Application.Quit();
    }

    public void BtnAldea()
    {
        SceneManager.LoadScene("VillageMenuScene");
    }

    public void BtnCredits()
    {
        creditsPanel.SetActive(true);
        currentPage = 0;

        ShowCreditsPage();
    }

    public void BtnCreditsNext()
    {
        currentPage++;

        // si se llega al final
        if (currentPage >= creditsPage.Length)
        {
            currentPage = 0;
        }

        ShowCreditsPage();
    }

    public void BtnCreditsClose()
    {
        creditsPanel.SetActive(false);
        currentPage = 0;
    }

    private void ShowCreditsPage()
    {
        for (int i = 0; i < creditsPage.Length; i++)
        {
            creditsPage[i].SetActive(i == currentPage);
        }
    }
}
