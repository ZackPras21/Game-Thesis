using UnityEngine;

public class GameProgression : MonoBehaviour
{
    [Header("----------------Material----------------")]
    public Material environmentNeonMaterial;
    public Material SkyBoxBlue;
    public Material SkyBoxRed;
    private static readonly int BaseColorPropertyID = Shader.PropertyToID("_BorderColor");
    [Header("----------------Initialize Enemy Data----------------")]
    public int EnemyTotalInitial;
    [Header("----------------Live Enemy Data----------------")]
    [Header("Enemy----------------")]
    public int EnemyTotalCount;
    public int EnemyTotalSpawnCount;
    public int EnemyKilled;
    public int SpawnerCount;
    public int EnemySpawnCount;
    [Header("Chest----------------")]
    public int ChestTotal;
    public int ChestClaimed;
    [Header("Box----------------")]
    public int BoxTotal;
    public int BoxDestroyed;
    [Header("Gate----------------")]
    public GameObject[] gate;
    [Header("Tower----------------")]
    public int TowerTotal;
    public int TowerRecovered;
    [Header("Game Progress Percentage----------------")]
    public int Percentage;
    private bool enemyCountMapped = false;
    public static GameProgression Instance;
    private bool isEnd = false;
    private int milestonePercentage = 0;
    public bool IsBossSpawned = false;
    public bool IsBossDead = false;
    public GameObject mainCamera;
    public GameObject sceneCamera;
    private void Awake()
    {
        if (Instance == null) Instance = this;
        RenderSettings.skybox = SkyBoxRed;
    }
    private void Start()
    {
        Color baseColor = new Color(191f / 255f, 0f, 0f, 1f);
        float intensity = 4f;
        Color adjustedColor = baseColor * intensity;
        adjustedColor.a = baseColor.a;

        environmentNeonMaterial.SetColor(BaseColorPropertyID, adjustedColor);
        SpawnerCount = GameObject.FindGameObjectsWithTag("Spawner").Length;
        ChestTotal = GameObject.FindGameObjectsWithTag("Chest").Length;
        TowerTotal = GameObject.FindGameObjectsWithTag("Tower").Length;
        BoxTotal = GameObject.FindGameObjectsWithTag("Box").Length;
    }
    private void Update()
    {
        if (SpawnerCount == 0) SpawnerCount = GameObject.FindGameObjectsWithTag("Spawner").Length;
        else if (!enemyCountMapped)
        {
            EnemySpawnCount = Mathf.RoundToInt(EnemyTotalInitial / SpawnerCount);
            EnemyTotalCount = EnemySpawnCount * SpawnerCount;

            foreach (GameObject enemySpawner in GameObject.FindGameObjectsWithTag("Spawner"))
            {
                enemySpawner.GetComponent<EnemySpawner>().maxEnemyCount = EnemySpawnCount;
            }
            enemyCountMapped = true;
        }
        if (EnemyTotalCount != 0 && ChestTotal != 0 && BoxTotal != 0 && TowerTotal != 0)
        {
            float enemyPercentage = (float)EnemyKilled / EnemyTotalCount;
            float chestPercentage = (float)ChestClaimed / ChestTotal;
            float boxPercentage = (float)BoxDestroyed / BoxTotal;
            float towerPercentage = (float)TowerRecovered / TowerTotal;

            Percentage = Mathf.RoundToInt((enemyPercentage + chestPercentage + boxPercentage + towerPercentage) / 4 * 100);
        }
        if (Percentage == 100 && !isEnd)
        {
            environmentNeonMaterial.SetColor(BaseColorPropertyID, new Color(0, 115, 191, 1));

            Color baseColor = new Color(0, 115f / 255f, 191f / 255f, 1f);
            float intensity = 4f;
            Color adjustedColor = baseColor * intensity;
            adjustedColor.a = baseColor.a;

            environmentNeonMaterial.SetColor(BaseColorPropertyID, adjustedColor);
            RenderSettings.skybox = SkyBoxBlue;

            isEnd = true;
        }
        if (Percentage == 19 && milestonePercentage != 19)
        {
            gate[0].GetComponent<GateInteraction>().OpenGate();
            gate[1].GetComponent<GateInteraction>().OpenGate();
            milestonePercentage = 19;
        }
        else if (Percentage == 26 && milestonePercentage != 26 && EnemyKilled == 4)
        {
            AudioManager.instance.PlaySFX(AudioManager.instance.gate);
            gate[1].GetComponent<GateInteraction>().OpenGate();
            gate[2].GetComponent<GateInteraction>().OpenGate();
            gate[3].GetComponent<GateInteraction>().OpenGate();
            milestonePercentage = 26;
        }
        else if (Percentage == 34 && milestonePercentage != 34 && EnemyKilled == 8)
        {
            AudioManager.instance.PlaySFX(AudioManager.instance.gate);
            gate[3].GetComponent<GateInteraction>().OpenGate();
            gate[4].GetComponent<GateInteraction>().OpenGate();
            gate[5].GetComponent<GateInteraction>().OpenGate();
            milestonePercentage = 34;
        }
        else if (Percentage == 44 && milestonePercentage != 44 && EnemyKilled == 12)
        {
            AudioManager.instance.PlaySFX(AudioManager.instance.gate);

            gate[5].GetComponent<GateInteraction>().OpenGate();
            gate[6].GetComponent<GateInteraction>().OpenGate();
            gate[7].GetComponent<GateInteraction>().OpenGate();
            milestonePercentage = 44;
            Invoke("SceneCamera", 3);
        }
        else if (Percentage == 52 && milestonePercentage != 52 && EnemyKilled == 16)
        {
            AudioManager.instance.PlaySFX(AudioManager.instance.gate);
            gate[7].GetComponent<GateInteraction>().OpenGate();
            gate[8].GetComponent<GateInteraction>().OpenGate();
            milestonePercentage = 52;
        }
        else if (Percentage == 58 && milestonePercentage != 58)
        {
            AudioManager.instance.PlaySFX(AudioManager.instance.gate);
            gate[9].GetComponent<GateInteraction>().OpenGate();
            milestonePercentage = 58;
        }
        else if (Percentage == 62 && milestonePercentage != 62 && EnemyKilled == 20)
        {
            AudioManager.instance.PlaySFX(AudioManager.instance.gate);
            gate[9].GetComponent<GateInteraction>().OpenGate();
            gate[10].GetComponent<GateInteraction>().OpenGate();
            gate[11].GetComponent<GateInteraction>().OpenGate();
            milestonePercentage = 62;
        }
        else if (Percentage == 69 && milestonePercentage != 69 && EnemyKilled == 24)
        {
            AudioManager.instance.PlaySFX(AudioManager.instance.gate);
            gate[11].GetComponent<GateInteraction>().OpenGate();
            gate[12].GetComponent<GateInteraction>().OpenGate();
            milestonePercentage = 69;
        }
        else if (Percentage == 76 && milestonePercentage != 76)
        {
            AudioManager.instance.PlaySFX(AudioManager.instance.gate);
            gate[13].GetComponent<GateInteraction>().OpenGate();
            milestonePercentage = 76;
        }
        else if (Percentage == 83 && milestonePercentage != 83 && EnemyKilled == 28)
        {
            AudioManager.instance.PlaySFX(AudioManager.instance.gate);
            gate[13].GetComponent<GateInteraction>().OpenGate();
            gate[14].GetComponent<GateInteraction>().OpenGate();
            gate[15].GetComponent<GateInteraction>().OpenGate();
            milestonePercentage = 83;
        }
        else if (Percentage == 92 && milestonePercentage != 92)
        {
            gate[15].GetComponent<GateInteraction>().OpenGate();
            gate[16].GetComponent<GateInteraction>().OpenGate();
            milestonePercentage = 92;
        }
        else if (Percentage == 100 && milestonePercentage != 100)
        {
            gate[17].GetComponent<GateInteraction>().OpenGate();
            milestonePercentage = 100;
        }
    }
    void ReturnToMainCamera()
    {
        PlayerController.Instance.GetComponent<PlayerController>().enabled = true;
        MainCamera();
    }
    public void MainCamera()
    {
        mainCamera.SetActive(true);
        sceneCamera.SetActive(false);
    }

    public void SceneCamera()
    {
        PlayerController.Instance.GetComponent<PlayerController>().enabled = false;
        PlayerController.Instance.GetComponent<Animator>().SetFloat("Speed", 0);
        sceneCamera.SetActive(true);
        mainCamera.SetActive(false);
        Invoke("ReturnToMainCamera", 5);
    }
    public void EnemySpawn()
    {
        EnemyTotalSpawnCount++;
    }
    public void EnemyKill()
    {
        EnemyKilled++;
    }
    public void ChestClaim()
    {
        ChestClaimed++;
    }
    public void BoxDestroy()
    {
        BoxDestroyed++;
    }
    public void TowerRecover()
    {
        TowerRecovered++;
    }
}
