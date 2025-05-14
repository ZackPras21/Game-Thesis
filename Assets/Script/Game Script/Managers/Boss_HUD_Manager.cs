using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Boss_HUD_Manager : MonoBehaviour
{
    public GameObject BossHUD;
    Boss3Controller Boss3;
    private int bossStatus = 0;
    public Slider bossHealth;
    private GameProgression gameProgression;
    void Start()
    {
        bossHealth.gameObject.SetActive(false);
        BossHUD.SetActive(false);
        gameProgression = GameProgression.Instance;
    }

    // Update is called once per frame
    void Update()
    {
        if (gameProgression.IsBossSpawned)
        {   
            BossHUD.SetActive(true);
            if (Boss3 == null)
            {
                // Debug.Log("Boss3 is null");
                Boss3 = Boss3Controller.Instance;
                SetMaxBossHealth();
            }

            if(Boss3 != null)
            SetBossHealth();

            if (bossStatus == 0 && Boss3 != null && Boss3.triggerBossSpawn)
            {
                bossStatus = 1;
                bossHealth.gameObject.SetActive(true);
            }

            if (bossStatus == 1 && Boss3 != null && Boss3.getBossHP() <= 0)
            {
                bossStatus = 2;
                bossHealth.gameObject.SetActive(false);
            }
        }
    }
    public void SetMaxBossHealth()
    {
        bossHealth.maxValue = BossEnemyData.Instance.enemyHealth;
    }

    public void SetBossHealth()
    {
        bossHealth.value = Boss3.getBossHP();
        // Debug.Log("Boss Health is Update: " + Boss3.getBossHP());
    }
}
