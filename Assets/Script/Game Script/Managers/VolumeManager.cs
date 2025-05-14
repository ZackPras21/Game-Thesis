using UnityEngine;
using UnityEngine.UI;

public class VolumeManager : MonoBehaviour
{
    public static VolumeManager instance;

    [Header("Volume Slider")]
    [SerializeField] private Slider volumeSlider;

    private const string VOLUME_KEY = "GLOBAL_VOLUME";
    private float globalVolume = 1f;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Load saved global volume
        globalVolume = PlayerPrefs.GetFloat(VOLUME_KEY, 1f);
        AudioListener.volume = globalVolume;

        if (volumeSlider != null)
        {
            volumeSlider.value = globalVolume;
            volumeSlider.onValueChanged.AddListener(SetGlobalVolume);
        }
    }

    private void SetGlobalVolume(float volume)
    {
        globalVolume = volume;
        AudioListener.volume = volume;
        PlayerPrefs.SetFloat(VOLUME_KEY, volume);
        PlayerPrefs.Save();
    }
}
