using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PickCharacter : MonoBehaviour
{
    public void LoadSelectLevelScene()
    {
        SceneManager.LoadScene("Select_Level");
    }
    GameManager gm;

    private void Start()
    {
        gm = GameManager.Instance;
    }
    public void SavePlayerMan()
    {
        gm.SavePlayer(PlayerManData.playerManData);
        gm.SaveWeapon(BatonWeapon.Baton);
        gm.SaveGear(0);
        LoadSelectLevelScene();
    }
    public void SavePlayerWoman()
    {
        gm.SavePlayer(PlayerWomanData.playerWomanData);
        gm.SaveWeapon(WhipWeapon.Whip);
        gm.SaveGear(0);
        LoadSelectLevelScene();
    }
}
