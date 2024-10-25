using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Setting : MonoBehaviour
{
    public GameObject sound;
    public GameObject control;

    public void OnSound()
    {
        sound.SetActive(true);
        control.SetActive(false);
    }

    public void OnControl()
    {
        control.SetActive(true);
        sound.SetActive(false);
    }
}

