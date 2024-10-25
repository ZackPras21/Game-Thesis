using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class P_Hud_Manager : MonoBehaviour
{
    public Slider slider;
    public Slider sliderDash;
    public Text DebugText;
    public Text DataText;
    public Text GearText;
    public Text MissionTask;
    public Slider progressSlider;
    public TextMeshProUGUI ProgressText;
    public TextMeshProUGUI ReadyToDash;
    private GameProgression gameProgression;
    public GameObject UpgradeStation;
    PlayerController Player;
    public GameObject HUDStatus;
    public Text SkillCount;
    public Slider SkillCountSlider;
    public Slider ParrySlider;
    private void Start()
    {
        Player = PlayerController.Instance;
        SetMaxHealth();
        gameProgression = GameProgression.Instance;
    }
    private void Update()
    {
        if (Player.isAlive)
        {
            if (Input.GetKeyDown(KeyCode.T))
            {
                HUDStatus.SetActive(false);
                UpgradeStation.SetActive(true);
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }
            if (Player.playerState == PlayerState.Parry)
                ParrySlider.value = Mathf.Lerp(ParrySlider.value, 1, Time.deltaTime * 3);

            else if (ParrySlider.value != 0)
                ParrySlider.value = Mathf.Lerp(ParrySlider.value, 0, Time.deltaTime * 5);

            if (Player.SkillEnergy <= 2)
                SkillCountSlider.value = Mathf.Lerp(SkillCountSlider.value, Player.SkillEnergy, Time.deltaTime * 3);

            SkillCount.text = (Player.SkillEnergy / 2).ToString();
            SetMaxHealth();
            SetHealth();
            DebugText.text = "Debug: " + Player.playerState;
            GearText.text = Player.Gear.ToString();
            DataText.text = Player.Data.ToString();
            sliderDash.value = Player.dashCount + 1;
            if (sliderDash.value == 50)
            {
                ReadyToDash.enabled = true;
            }
            else
            {
                ReadyToDash.enabled = false;
            }
            ProgressText.text = gameProgression.Percentage.ToString() + "%";
            progressSlider.value = Mathf.Lerp(progressSlider.value, gameProgression.Percentage, Time.deltaTime);
            MissionTask.text = gameProgression.TowerRecovered + "  / " + gameProgression.TowerTotal + "\n" + gameProgression.ChestClaimed + "  / " + gameProgression.ChestTotal + "\n" + gameProgression.BoxDestroyed + "  / " + gameProgression.BoxTotal + "\n" + gameProgression.EnemyKilled + "  / " + gameProgression.EnemyTotalCount;
        }
    }
    public void CloseUpgrade()
    {
        HUDStatus.SetActive(true);
        UpgradeStation.SetActive(false);
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }
    public void BackToPlayerSelection()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("Menu 3D");
    }
    public void SaveData()
    {
        GameManager.Instance.SaveGear(Player.Gear);
        GameManager.Instance.SavePlayer(Player.playerData);
        Time.timeScale = 1f;
        SceneManager.LoadScene("Menu 3D");
    }

    public void SetMaxHealth()
    {
        slider.maxValue = Player.MaxHealth;
    }

    public void SetHealth()
    {
        slider.value = Player.playerData.playerHealth;
    }


}
