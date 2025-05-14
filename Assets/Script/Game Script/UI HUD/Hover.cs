using UnityEngine;
using UnityEngine.EventSystems;

public class HoverEnlargeTitle : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public float hoverScaleFactor = 1.2f; // Faktor perbesaran saat dihover
    public AudioManager audioManager; // Referensi ke AudioManager

    private Vector3 originalScale; // Skala asli judul
    private bool isHovering = false; // Status judul sedang dihover

    void Start()
    {
        // Simpan skala asli judul
        originalScale = transform.localScale;
        // Jika AudioManager belum di-set melalui Inspector, coba cari instance yang ada di Scene
        if (audioManager == null && AudioManager.instance != null)
        {
            audioManager = AudioManager.instance; 
        }
    }

    private void Update()
    {
        if (audioManager == null)
        audioManager = AudioManager.instance;
    }

    // Dipanggil saat pointer masuk ke dalam area judul
    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovering = true;
        EnlargeTitleOnHover();
        if (audioManager != null)
        {
            audioManager.PlayButtonHoverSound(); // Mainkan efek suara saat dihover
        }
    }

    // Dipanggil saat pointer keluar dari area judul
    public void OnPointerExit(PointerEventData eventData)
    {
        isHovering = false;
        ResetTitleScale();
    }

    // Memperbesar judul saat dihover
    void EnlargeTitleOnHover()
    {
        if (isHovering)
        {
            // Mengalikan skala asli dengan faktor perbesaran
            transform.localScale = originalScale * hoverScaleFactor;
        }
    }

    // Kembalikan skala judul ke skala asli
    void ResetTitleScale()
    {
        transform.localScale = originalScale;
    }
}
