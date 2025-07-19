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
    private static CreepEnemyData _instance;
    public static CreepEnemyData Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = ScriptableObject.CreateInstance<CreepEnemyData>();
                _instance.enemyHealth = 100;
                _instance.enemyAttack = 4;
                _instance.enemySpeed = 9.0f;
                _instance.EnemyType = EnemyType.Creep;
            }
            return _instance;
        }
    }
}

public class Medium1EnemyData : EnemyData
{
    private static Medium1EnemyData _instance;
    public static Medium1EnemyData Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = ScriptableObject.CreateInstance<Medium1EnemyData>();
                _instance.enemyHealth = 150;
                _instance.enemyAttack = 6;
                _instance.enemySpeed = 7.0f;
                _instance.EnemyType = EnemyType.Medium1;
            }
            return _instance;
        }
    }
}

public class Medium2EnemyData : EnemyData
{
    private static Medium2EnemyData _instance;
    public static Medium2EnemyData Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = ScriptableObject.CreateInstance<Medium2EnemyData>();
                _instance.enemyHealth = 200;
                _instance.enemyAttack = 8;
                _instance.enemySpeed = 5.0f;
                _instance.EnemyType = EnemyType.Medium2;
            }
            return _instance;
        }
    }
}

public class BossEnemyData : EnemyData
{
    private static BossEnemyData _instance;
    public static BossEnemyData Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = ScriptableObject.CreateInstance<BossEnemyData>();
                _instance.enemyHealth = 1000;
                _instance.enemyAttack = 15;
                _instance.enemySpeed = 4.0f;
                _instance.EnemyType = EnemyType.Boss;
            }
            return _instance;
        }
    }
}