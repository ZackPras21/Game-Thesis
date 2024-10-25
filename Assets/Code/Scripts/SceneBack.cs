using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class SceneBack : MonoBehaviour
{
    public void BackMainMenu()
    {
        SceneManager.LoadScene("Menu 3D");
    }
     public void BackSelectCharacter()
    {
        SceneManager.LoadScene("Select_Character");
    }


    public void Update()
    {
      
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            
            BackMainMenu();
        }
    }
}
