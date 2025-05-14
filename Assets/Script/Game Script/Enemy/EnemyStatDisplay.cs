using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class EnemyStatDisplay : MonoBehaviour
{
    public GameObject enemyStatsPanel; // Reference to the stats panel in the UI
    public Text healthText;
    public Text attackText;
    public Text speedText;

    private EnemyData enemyData;

    void Start()
    {
        enemyData = GetComponent<EnemyData>();
        if (enemyStatsPanel != null)
            enemyStatsPanel.SetActive(false); // Hide panel initially
    }

    public void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            ShowEnemyStats();
        }
    }

    public void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            HideEnemyStats();
        }
    }

    public void ShowEnemyStats()
    {
        if (enemyData != null && enemyStatsPanel != null)
        {
            healthText.text = "Health: " + enemyData.enemyHealth;
            attackText.text = "Attack: " + enemyData.enemyAttack;
            speedText.text = "Speed: " + enemyData.enemySpeed;
            enemyStatsPanel.SetActive(true); // Show panel
        }
    }

    public void HideEnemyStats()
    {
        if (enemyStatsPanel != null)
            enemyStatsPanel.SetActive(false); // Hide panel
    }
}
