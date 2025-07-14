using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum EnemyType
{
    Creep,
    Medium1,
    Medium2,
    Boss
}

public class EnemyData : ScriptableObject
{
    public int enemyHealth = 100;
    public int enemyAttack = 10;
    public float enemySpeed = 5f;
    public EnemyType EnemyType;
    
    // Add default values in constructor
    public EnemyData()
    {
        enemyHealth = 100;
        enemyAttack = 10;
        enemySpeed = 5f;
    }
}


public class CreepEnemyData : EnemyData
{
    public static CreepEnemyData creepEnemyData = new CreepEnemyData();
    public CreepEnemyData()
    {
        enemyHealth = 100;
        enemyAttack = 4;
        enemySpeed = 9.0f;
        EnemyType = EnemyType.Creep;
    }
    // Properti statis Instance untuk mendapatkan instance CreepEnemyData
    private static CreepEnemyData _instance;
    public static CreepEnemyData Instance
    {
        get
        {
            if (_instance == null)
                _instance = new CreepEnemyData();
            return _instance;
        }
    }
}

public class Medium1EnemyData : EnemyData
{
    public static Medium1EnemyData medium1EnemyData = new Medium1EnemyData();
    public Medium1EnemyData()
    {
        enemyHealth = 150;
        enemyAttack = 6;
        enemySpeed = 7.0f;
        EnemyType = EnemyType.Medium1;
    }
}

public class Medium2EnemyData : EnemyData
{
    public static Medium2EnemyData medium2EnemyData = new Medium2EnemyData();
    public Medium2EnemyData()
    {
        enemyHealth = 200;
        enemyAttack = 8;
        enemySpeed = 5.0f;
        EnemyType = EnemyType.Medium2;
    }
}
public class BossEnemyData : EnemyData
{
    public static BossEnemyData boss2EnemyData = new BossEnemyData();
    public BossEnemyData()
    {
        enemyHealth = 1000;
        enemyAttack = 15;
        enemySpeed = 4.0f;
        EnemyType = EnemyType.Boss;
    }

    private static BossEnemyData _instance;
    public static BossEnemyData Instance
    {
        get
        {
            if (_instance == null)
                _instance = new BossEnemyData();
            return _instance;
        }
    }
}