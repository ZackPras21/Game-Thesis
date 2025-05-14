using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    public GameObject mainMenu;
    public GameObject setting;
    public GameObject selectCharacter;
    public GameObject credit;
    public Button Continue;

    private void Update()
    {
        if (GameManager.Instance.PlayerData == null)
        {
            Continue.interactable = false;
        }
        else
        {
            Continue.interactable = true;
        }
    }

    public void OnSelectCharacter()
    {
        selectCharacter.SetActive(true);
        mainMenu.SetActive(false);
    }

    public void OnSetting()
    {
        setting.SetActive(true);
        mainMenu.SetActive(false);
    }

    public void OnCredit()
    {
        credit.SetActive(true);
        mainMenu.SetActive(false);
    }

    public void OnContinue()
    {
        SceneManager.LoadScene("Select_Level");
    }

    public void OnQuit()
    {
        Application.Quit();
    }
}
