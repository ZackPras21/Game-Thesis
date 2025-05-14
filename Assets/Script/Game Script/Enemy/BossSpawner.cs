using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BossSpawner : MonoBehaviour
{
    public GameObject BossPrefab;
    public GameObject corner1;
    public GameObject corner2;
    public Transform[] waypoints;
    private bool spawnTriggered = false;
    private CameraFollow cameraFollow;
    private GameProgression gameProgression;
    public GameObject Gate;
    private void Start()
    {
        gameProgression = GameProgression.Instance;
        cameraFollow = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<CameraFollow>();
    }
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && other.gameObject.layer == LayerMask.NameToLayer("Hitbox"))
        {
            cameraFollow.CombatMode();
            if (!spawnTriggered)
            {
                AudioManager.instance.PlaySFX(AudioManager.instance.gateClose);
                GameProgression.Instance.IsBossSpawned = true;
                Gate.GetComponent<GateInteraction>().CloseGate();
                SpawnEnemy();
                spawnTriggered = true;
            }
        }
    }
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") && other.gameObject.layer == LayerMask.NameToLayer("Hitbox"))
        {
            cameraFollow.NormalMode();
        }
    }

    private void SpawnEnemy()
    {
        GameObject enemyToSpawn = BossPrefab;

        Vector3 spawnPosition = GetRandomPosition();
        Quaternion spawnDirection = Quaternion.Euler(0, -90, 0);

        GameObject Enemy = Instantiate(enemyToSpawn, spawnPosition, spawnDirection);
        // for (int i = 0; i < waypoints.Length; i++)
        // {
        //     Enemy.GetComponent<EnemyController>().waypoints[i] = waypoints[i];
        // }
        
        // gameProgression.EnemySpawn();
    }
    Vector3 GetRandomPosition()
    {
        if (corner1 == null || corner2 == null)
        {
            // Debug.Log("One or both corners are not assigned.");
            return Vector3.zero; // Or any default position you want to return in case of error.
        }
        Vector3 minCorner = Vector3.Min(corner1.transform.position, corner2.transform.position);
        Vector3 maxCorner = Vector3.Max(corner1.transform.position, corner2.transform.position);

        float randomX = Random.Range(minCorner.x, maxCorner.x);
        float randomZ = Random.Range(minCorner.z, maxCorner.z);

        return new Vector3(randomX, 1f, randomZ);
    }
}
