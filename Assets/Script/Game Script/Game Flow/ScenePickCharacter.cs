using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ScenePickCharacter : MonoBehaviour
{
    public void NewGame()
    {
        SceneManager.LoadScene("Select_Character");
    }

}
