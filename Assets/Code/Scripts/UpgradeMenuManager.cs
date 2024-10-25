using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UpgradeMenuManager : MonoBehaviour
{
    PlayerData player;
    int gearData;
    int gearRequired;
    int HP;
    int ATK;
    int DEF;

    public Button incHP;
    public Button incATK;
    public Button incDEF;
    public Button decHP;
    public Button decATK;
    public Button decDEF;
    public Button payButton;
    public Text GearRequired;
    public Text upgradeData;
    public Text resultData;
    public Text gear;
    int priceHP, priceATK, priceDEF;

    GameManager gm;

    void Start()
    {
        gm = GameManager.Instance;
        player = gm.LoadPlayer();
        gearData = gm.LoadGear();
        HP = 0;
        ATK = 0;
        DEF = 0;
        gearRequired = 0;
        priceATK = 5;
        priceDEF = 3;
        priceHP = 3;
    }

    void Update()
    {
        resultData.text = HP +
                        "\n" + ATK +
                        "\n" + DEF;
        upgradeData.text = player.playerHealth +
                            "\n" + player.playerAttack +
                            "\n" + player.playerDefense;
        gear.text = "GEAR : " + gm.LoadGear();
        if (gearRequired <= 1)
        {
            GearRequired.text = gearRequired + " GEAR";
        } else {
            GearRequired.text = gearRequired + " GEARS";
        }
        if (gm.LoadGear() <= 1)
        {
            gear.text = gm.LoadGear() + " GEAR";
        } else {
            gear.text = gm.LoadGear() + " GEARS";
        }
    }

    public void IncrementAttack()
    {
        if (gearRequired < gearData)
        {
            ATK++;
            gearRequired += priceATK;
        }
    }

    public void DecrementAttack()
    {
        if (ATK > 0)
        {
            ATK--;
            gearRequired -= priceATK;
        }
    }

    public void IncrementDefense()
    {
        if (gearRequired < gearData)
        {
            DEF++;
            gearRequired += priceDEF;
        }
    }

    public void DecrementDefense()
    {
        if (DEF > 0)
        {
            DEF--;
            gearRequired -= priceDEF;
        }
    }

    public void IncrementHealth()
    {
        if (gearRequired < gearData)
        {
            HP += 5;
            gearRequired += priceHP;
        }
    }

    public void DecrementHealth()
    {
        if (HP > 0)
        {
            HP -= 5;
            gearRequired -= priceHP;
        }
    }

    public void Pay()
    {
        if (gearRequired <= gearData)
        {
            player.playerHealth += HP;
            player.playerAttack += ATK;
            player.playerDefense += DEF;
            gearData -= gearRequired;
            gearRequired = 0;
            gear.text = "Gear: " + gearData;
            gm.SavePlayer(player);
            gm.SaveGear(gearData);
            Reset();
        }
    }

    public void Reset()
    {
        HP = 0;
        ATK = 0;
        DEF = 0;
        gearRequired = 0;
    }
}
