using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class UpgradeManager : MonoBehaviour
{

    PlayerController Player;
    public Button IncHP;
    public Button IncATK;
    public Button IncDEF;
    public Button DecHP;
    public Button DecATK;
    public Button DecDEF;
    public Button PayButton;
    public Button CloseButton;
    public Text PlayerGear;
    public Text PlayerMaxHP;
    public Text PlayerATK;
    public Text PlayerDEF;
    public Text UpgradeHPCount;
    public Text UpgradeATKCount;
    public Text UpgradeDEFCount;
    public Text GearRequired;
    private int MaxHP, ATK, DEF, GearTotal;
    private int upgradeHP, upgradeATK, upgradeDEF;
    private int priceHP, priceATK, priceDEF;
    private void Start()
    {
        Player = PlayerController.Instance;
        MaxHP = Player.MaxHealth;
        ATK = Player.playerData.playerAttack;
        DEF = Player.playerData.playerDefense;
        GearTotal = 0;
        priceATK = 2;
        priceDEF = 1;
        priceHP = 1;
    }
    private void Update()
    {
        if (Player == null)
        {
            Player = PlayerController.Instance;
            MaxHP = Player.MaxHealth;
            ATK = Player.playerData.playerAttack;
            DEF = Player.playerData.playerDefense;
        }
        else
        {
            MaxHP = Player.MaxHealth;
            ATK = Player.playerData.playerAttack;
            DEF = Player.playerData.playerDefense;

            PlayerMaxHP.text = MaxHP.ToString();
            PlayerATK.text = ATK.ToString();
            PlayerDEF.text = DEF.ToString();

            UpgradeHPCount.text = upgradeHP.ToString();
            UpgradeATKCount.text = upgradeATK.ToString();
            UpgradeDEFCount.text = upgradeDEF.ToString();

            if(Player.Gear >= GearTotal)
            GearRequired.text = "       Gear  : " + GearTotal;
            else if (GearTotal > Player.Gear)
            GearRequired.text = "       Insufficient";

            PlayerGear.text = "          Gear  : " + Player.Gear;
        }
    }
    public void IncrementAttack()
    {
        if (GearTotal < Player.Gear)
        {
            upgradeATK++;
            GearTotal += priceATK;
        }
    }

    public void DecrementAttack()
    {
        if (upgradeATK > 0)
        {
            upgradeATK--;
            GearTotal -= priceATK;
        }
    }

    public void IncrementDefense()
    {
        if (GearTotal < Player.Gear)
        {
            upgradeDEF++;
            GearTotal += priceDEF;
        }
    }

    public void DecrementDefense()
    {
        if (upgradeDEF > 0)
        {
            upgradeDEF--;
            GearTotal -= priceDEF;
        }
    }

    public void IncrementHealth()
    {
        if (GearTotal < Player.Gear)
        {
            upgradeHP += 5;
            GearTotal += priceHP;
        }
    }

    public void DecrementHealth()
    {
        if (upgradeHP > 0)
        {
            upgradeHP -= 5;
            GearTotal -= priceHP;
        }
    }
    public void Pay()
    {
        if (GearTotal <= Player.Gear)
        {
            Player.MaxHealth += upgradeHP;
            Player.playerData.playerHealth += upgradeHP;
            Player.playerData.playerAttack += upgradeATK;
            Player.playerData.playerDefense += upgradeDEF;
            Player.Gear -= GearTotal;
            GearTotal = 0;
            upgradeHP = 0;
            upgradeATK = 0;
            upgradeDEF = 0;
        }
    }
}
