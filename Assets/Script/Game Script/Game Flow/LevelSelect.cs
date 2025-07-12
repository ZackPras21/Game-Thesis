using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class LevelSelect : MonoBehaviour
{
    public Button UpgradeButton;
    public GameObject UpgradeCharacter;
    public GameObject LevelSelectPanel;
    public Image img;
    private void Start()
    {
        // Pastikan komponen UI telah dihubungkan dengan benar di Inspector
        if (UpgradeButton == null || UpgradeCharacter == null || LevelSelectPanel == null)
        {
            // Debug.LogError("Please assign all UI components in the inspector.");
            return;
        }
        if (GameManager.Instance.LoadGear() == 0)
        {
            UpgradeButton.gameObject.SetActive(false);
        }
        else
        {
            UpgradeButton.gameObject.SetActive(true);
        }
        // Periksa dan update interaktifitas tombol upgrade saat memulai
        UpdateButtonsInteractability();
    }

    private void UpdateButtonsInteractability()
    {
        // Periksa apakah GameManager.Instance ada dan PlayerData tidak null
        if (GameManager.Instance == null || GameManager.Instance.PlayerData == null)
        {
            UpgradeButton.interactable = false;
        }
        else
        {
            UpgradeButton.interactable = true;
        }
    }

    public void LoadGame()
    {
        // Load scene dengan nama "NewLevelOne"
        SceneNavigationManager.Instance.NextScene = "Intro Komik";
        StartCoroutine(LoadGameTransition());
    }

    public void LoadReinforcementLearningGame()
    {
        // Load scene dengan nama "RL"
        SceneNavigationManager.Instance.NextScene = "Test Reinforcement Learning";
        StartCoroutine(LoadGameTransition());
    }

    public void OnUpgrade()
    {
        // Aktifkan UpgradeCharacter dan nonaktifkan LevelSelectPanel
        UpgradeCharacter.SetActive(true);
        LevelSelectPanel.SetActive(false);
    }

    private IEnumerator LoadGameTransition()
    {
        img.enabled = true;
        while (img.color.a < 0.99)
        {
            img.color = new Color(img.color.r, img.color.g, img.color.b, Mathf.Lerp(img.color.a, 1, Time.deltaTime * 5f));
            yield return null;
        }
        yield return new WaitForSeconds(0.3f);
        SceneManager.LoadScene("Loading");
        yield return null;
    }
    
}
