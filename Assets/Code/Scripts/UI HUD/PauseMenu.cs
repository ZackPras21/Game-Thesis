using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PauseMenu : MonoBehaviour
{
    public GameObject pauseMenuUI;
    public Button Settings;
    public Button MainMenu;
    public GameObject SettingPanel;
    private bool isPause = false;
    // Start is called before the first frame update
    private void Pause()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        pauseMenuUI.SetActive(true);
        Time.timeScale = 0f; // Pause the game
    }

    // Update is called once per frame
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (!SettingPanel.activeSelf)
            {
                isPause = !isPause;
                if (isPause)
                    Pause();
                else
                    Resume();
            }
            else
                SettingPanel.SetActive(false);
        }
    }
    public void Resume()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        pauseMenuUI.SetActive(false);
        Time.timeScale = 1f;
    }
    public void Restart()
    {
        SceneManager.LoadScene("NewLevelOne");
        pauseMenuUI.SetActive(false);
        Time.timeScale = 1f;
    }
    public void GoMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("Menu 3D");
    }
    public void Setting()
    {
        SettingPanel.SetActive(true);
    }
}
