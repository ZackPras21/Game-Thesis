using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class EndingScreen : MonoBehaviour
{
    public GameObject WinScreen;
    public GameObject LoseScreen;
    public GameObject Volume;
    private bool isScreenEnabled = false;
    private void Start()
    {
        WinScreen.SetActive(false);
        LoseScreen.SetActive(false);
    }
    void Update()
    {
        if (!Cursor.visible)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        if (GameProgression.Instance.Percentage == 100 && GameProgression.Instance.IsBossDead && !isScreenEnabled && PlayerController.Instance.isAlive)
        {
            GameTimer.Instance.Stop();
            Volume.SetActive(false);
            LoseScreen.SetActive(false);
            WinScreen.SetActive(true);
            isScreenEnabled = true;
            PlayerController.Instance.Animator.enabled = false;
        }
        else if (!isScreenEnabled)
        {
            GameTimer.Instance.Stop();
            Volume.SetActive(false);
            WinScreen.SetActive(false);
            LoseScreen.SetActive(true);
            isScreenEnabled = true;
            PlayerController.Instance.Animator.enabled = false;
        }
        PlayerController.Instance.currentInputVector = Vector3.zero;
    }
}
