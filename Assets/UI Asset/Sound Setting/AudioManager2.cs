using UnityEngine;

public class AudioManager2 : MonoBehaviour
{
    public AudioSource audioSource;  // Referensi ke komponen AudioSource

    // Metode untuk menyalakan audio
    public void TurnOnAudio()
    {
        if (audioSource != null)
        {
            audioSource.Play();
        }
        else
        {
            // Debug.LogError("AudioSource tidak diatur");
        }
    }

    // Metode untuk mematikan audio
    public void TurnOffAudio()
    {
        if (audioSource != null)
        {
            audioSource.Stop();
        }
        else
        {
            // Debug.LogError("AudioSource tidak diatur");
        }
    }
}
