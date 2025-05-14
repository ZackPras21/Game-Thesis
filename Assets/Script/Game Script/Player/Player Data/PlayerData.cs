public class PlayerData
{
    public int playerHealth;
    public int playerAttack;
    public int playerDefense;
    public float playerSpeed = 6.5f;
    public float playerDashSpeed;
    public float playerDashTime;
    public float playerCriticalHit = 0.1f;
    public bool isMan;
    public void UpgradePlayerHealth()
    {
        playerHealth += 5;
    }
    public void UpgradePlayerAttack()
    {
        playerAttack += 1;
    }
    public void UpgradePlayerDefense()
    {
        playerDefense += 1;
    }
}

public class PlayerManData : PlayerData
{
    public static PlayerManData playerManData = new PlayerManData();
    public PlayerManData()
    {
        playerHealth = 120;
        playerAttack = 25;
        playerDefense = 25;
        playerDashSpeed = 15f;
        playerDashTime = 0.3f;
        isMan = true;
    }
}

public class PlayerWomanData : PlayerData
{
    public static PlayerWomanData playerWomanData = new PlayerWomanData();
    public PlayerWomanData()
    {
        playerHealth = 100;
        playerAttack = 22;
        playerDefense = 28;
        playerDashSpeed = 25f;
        playerDashTime = 0.2f;
        isMan = false;
    }
}
