using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class BackSelectLevel : MonoBehaviour
{
    private AudioManager audioManager; // Referensi ke AudioManager public Button UpgradeButton;
    
    private void Start()
    {
        // Mendapatkan referensi ke AudioManager
        audioManager = AudioManager.instance;
    }

    public void OnBackSelectLevel()
    {
        SceneManager.LoadScene("Select_Level");
    }

    public void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            OnBackSelectLevel();
        }
    }

    private void OnDestroy()
    {
        // Memastikan audio tetap aktif saat objek dihancurkan
        if (audioManager != null)
        {
            audioManager.TurnOnAudio();
        }
    }
}
