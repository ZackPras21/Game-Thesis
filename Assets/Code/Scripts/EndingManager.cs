using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EndingManager : MonoBehaviour
{
    public GameObject mainCamera;
    public GameObject sceneCamera;
    public GameObject endingScreen;
    private bool End = false;
    void Update()
    {
        if ((!PlayerController.Instance.isAlive || GameProgression.Instance.IsBossDead) && !End)
        {
            StartCoroutine(EndingScreenOn());
            End = true;
        }
    }
    IEnumerator EndingScreenOn()
    {
        yield return new WaitForSeconds(3);
        sceneCamera.SetActive(true);
        yield return new WaitForSeconds(4);
        endingScreen.SetActive(true);
    }
}
