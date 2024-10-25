using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class AudioManagerToggle : MonoBehaviour
{
    public AudioManager audioManager;
    public Button toggleButtonOn;
    public Button toggleButtonOff;
    public TMP_Text displayTextOn; // Teks yang menunjukkan status audio ON
    public TMP_Text displayTextOff; // Teks yang menunjukkan status audio OFF

    private bool audioOn = true; // Status audio saat ini

    private void Start()
    {
        // Coba mengambil status audio dari PlayerPrefs
        if (PlayerPrefs.HasKey("AudioOn"))
        {
            audioOn = PlayerPrefs.GetInt("AudioOn") == 1;
        }

        // Atur teks awal berdasarkan status audio yang disimpan
        UpdateButtonText();

        // Menambahkan listener ke tombol untuk memanggil ToggleAudio saat diklik
        toggleButtonOn.onClick.AddListener(ToggleAudio);
        toggleButtonOff.onClick.AddListener(ToggleAudio);
        audioManager = AudioManager.instance;
    }

    void ToggleAudio()
    {
        // Ubah status audio
        audioOn = !audioOn;

        // Simpan status audio ke PlayerPrefs
        PlayerPrefs.SetInt("AudioOn", audioOn ? 1 : 0);

        // Panggil fungsi yang sesuai di AudioManager jika audioManager tidak null
        if (audioManager != null)
        {
            if (audioOn)
            {
                audioManager.TurnOnAudio();
            }
            else
            {
                audioManager.TurnOffAudio();
            }
        }
        else
        {
            // Debug.LogWarning("AudioManager reference is null. Cannot toggle audio.");
        }

        // Perbarui teks tombol
        UpdateButtonText();
    }

    void UpdateButtonText()
    {
        // Sesuaikan teks tombol berdasarkan status audio yang baru
        if (audioOn)
        {
            displayTextOn.text = "ON";
            displayTextOff.text = "OFF";
        }
        else
        {
            displayTextOn.text = "OFF";
            displayTextOff.text = "ON";
        }
    }
}
