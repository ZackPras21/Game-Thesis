using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ButtonHoverMenuHome : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public float hoverOffset = 5f; // Jarak geser saat dihover
    public Sprite hoverSprite; // Sprite yang akan ditampilkan saat dihover
    public AudioManager audioManager; // Referensi ke AudioManager

    private Vector3 originalPosition; // Posisi asli tombol
    private Sprite originalSprite; // Sprite asli tombol
    private Image buttonImage; // Komponen Image pada tombol

    void Start()
    {
        // Simpan posisi asli tombol
        originalPosition = transform.position;
        // Simpan sprite asli tombol
        originalSprite = GetComponent<Image>().sprite;
        // Dapatkan komponen Image
        buttonImage = GetComponent<Image>();
        audioManager = AudioManager.instance;
    }
    private void Update()
    {
        if (audioManager == null)
            audioManager = AudioManager.instance;
    }
    // Dipanggil saat pointer masuk ke dalam area tombol
    public void OnPointerEnter(PointerEventData eventData)
    {
        audioManager.PlaySFX(audioManager.buttonHover);
        MoveButtonOnHover();
        ChangeButtonSprite(hoverSprite); // Ubah sprite saat dihover
    }

    // Dipanggil saat pointer keluar dari area tombol
    public void OnPointerExit(PointerEventData eventData)
    {
        ResetButtonPosition();
        ChangeButtonSprite(originalSprite); // Kembalikan sprite ke sprite aslinya
    }

    // Dipanggil saat tombol diklik
    public void OnPointerClick(PointerEventData eventData)
    {
        audioManager.PlaySFX(audioManager.buttonClick);
    }

    // Geser tombol ke kanan saat dihover
    void MoveButtonOnHover()
    {
        transform.position += Vector3.right * hoverOffset;
    }

    // Kembalikan tombol ke posisi asli
    void ResetButtonPosition()
    {
        transform.position = originalPosition;
    }

    // Ubah sprite pada tombol
    void ChangeButtonSprite(Sprite newSprite)
    {
        if (buttonImage != null)
        {
            buttonImage.sprite = newSprite;
        }
    }
}