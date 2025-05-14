using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerManager : MonoBehaviour
{
    GameManager gm;
    WeaponData weaponData;
    PlayerData playerData;
    public GameObject playerManPrefabs;
    public GameObject playerWomanPrefabs;
    GameObject playerPrefabs;
    public bool IsMan;
    private void Awake()
    {
        gm = GameManager.Instance;
        playerData = gm.LoadPlayer();
        weaponData = gm.LoadWeapon();
        IsMan = playerData.isMan;
        PlayerSpawn();
    }
    private void PlayerSpawn()
    {
        if (IsMan) playerPrefabs = playerManPrefabs;
        else playerPrefabs = playerWomanPrefabs;

        GameObject playerSpawn = Instantiate(playerPrefabs, transform.position, Quaternion.identity);
        playerSpawn.GetComponent<PlayerController>().playerData = playerData;
        playerSpawn.GetComponent<PlayerController>().weaponData = weaponData;
    }
}
