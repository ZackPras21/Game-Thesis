using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NewGameS : MonoBehaviour
{
    public void NewGame()
    {
        SceneManager.LoadScene("Select_Character");
    }

    public void Credit()
    {
        SceneManager.LoadScene("Credit_Scene");
    }

}
