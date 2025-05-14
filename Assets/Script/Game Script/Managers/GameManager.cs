using UnityEngine;

public class GameManager : MonoBehaviour
{
    #region Singleton
    public static GameManager Instance;
    public PlayerData PlayerData;
    private void Awake()
    {
        if (Instance == null) {
            Instance = this;
        } else {
            Destroy(gameObject);
        }
        DontDestroyOnLoad(this);
    }
    #endregion
    private void FixedUpdate()
    {
        PlayerData = LoadPlayer();
    }
    #region Load Player
    public void SavePlayer(PlayerData player)
    {
        string json = JsonUtility.ToJson(player);
        PlayerPrefs.SetString("PlayerData", json);
        PlayerPrefs.Save();
    }
    public PlayerData LoadPlayer()
    {
        string json = PlayerPrefs.GetString("PlayerData");

        if (json == "") return null;

        PlayerData player = JsonUtility.FromJson<PlayerData>(json);
        // Debug.Log(player);
        // Debug.Log(player.isMan);
        return player;
    }
    public void SaveWeapon(WeaponData player)
    {
        string json = JsonUtility.ToJson(player);
        PlayerPrefs.SetString("WeaponData", json);
        PlayerPrefs.Save();
    }
    public WeaponData LoadWeapon()
    {
        SaveWeapon(BatonWeapon.Baton);
        string json = PlayerPrefs.GetString("WeaponData");

        if (json == "") return null;

        WeaponData weapon = JsonUtility.FromJson<WeaponData>(json);
        return weapon;
    }
    #endregion

    #region Load Gear
    public int LoadGear()
    {
        return PlayerPrefs.GetInt("Gear");
    }
    public void SaveGear(int Gear)
    {
        PlayerPrefs.SetInt("Gear", Gear);
    }
    #endregion
}
