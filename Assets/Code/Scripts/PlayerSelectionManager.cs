using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PlayerSelectionManager : MonoBehaviour
{
    GameManager gm;
    public Button LoadButton;
    private void Start()
    {
        gm = GameManager.Instance;
        LoadButton.interactable = false;
        PlayerData playerData = gm.LoadPlayer();
        if (playerData != null)
        {
            LoadButton.interactable = true;
        }
    }

    public void SavePlayerMan()
    {
        gm.SavePlayer(PlayerManData.playerManData);
        gm.SaveWeapon(BatonWeapon.Baton);
        gm.SaveGear(0);
        LoadGame();
    }
    public void SavePlayerWoman()
    {
        gm.SavePlayer(PlayerWomanData.playerWomanData);
        gm.SaveWeapon(WhipWeapon.Whip);
        gm.SaveGear(0);
        LoadGame();
    }
    void LoadGame()
    {
        SceneManager.LoadScene("Gameplay");
    }
    public void LoadPlayer()
    {
        if (gm.LoadPlayer() != null)
        {
            LoadGame();
        }
    }
    public void CloseGame(){
        Application.Quit();
    }
}
