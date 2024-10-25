using UnityEngine;

public class LootManager : MonoBehaviour
{
    public GameObject lootDataPrefab; // Prefab dari objek loot yang akan di-spawn
    public GameObject lootGearPrefab; // Prefab dari objek loot yang akan di-spawn
    public GameObject lootHealthPrefab;
    public float spawnInterval = 3f; // Interval waktu antara setiap spawn
    private float nextSpawnTime; // Waktu berikutnya untuk melakukan spawn

    void Start()
    {
        nextSpawnTime = Time.time + spawnInterval; // Set waktu pertama untuk melakukan spawn
    }

    void Update()
    {
      
    }

    public void SpawnDataLoot(Transform spawnPoint)
    {
        // Spawn objek loot di spawnPoint yang telah dipilih
        Instantiate(lootDataPrefab, spawnPoint.position, spawnPoint.rotation, spawnPoint);
    }
    public void SpawnGearLoot(Transform spawnPoint)
    {
        // Spawn objek loot di spawnPoint yang telah dipilih
        Instantiate(lootGearPrefab, spawnPoint.position, spawnPoint.rotation, spawnPoint);
    }
    public void SpawnHealthLoot(Transform spawnPoint)
    {
        Instantiate(lootHealthPrefab, spawnPoint.position, spawnPoint.rotation, spawnPoint);
    }

}
