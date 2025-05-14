using Unity.VisualScripting;
using UnityEngine;

public class LootController : MonoBehaviour
{
    public float attractSpeed = 1f; // Kecepatan penarikan koin
    public bool isAttracted = false; // Status koin sedang ditarik atau tidak
    public float destroyDistance = 0.1f; // Jarak dari pemain untuk menghapus koin

    private Vector3 initialPosition; // Posisi awal koin
    public float startTime;
    public float distance;
    public GameObject player;
    void Start()
    {
        player = GameObject.FindWithTag("Player");
        startTime = Time.time;
        distance = Vector3.Distance(transform.position, player.transform.position);

        initialPosition = transform.position;
    }

    void Update()
    {
        if (isAttracted)
        {
            float collectedDist = (Time.time - startTime) * attractSpeed;
            float distLeft = collectedDist / distance;
            transform.position = Vector3.Lerp(transform.position, player.transform.position, distLeft);

            if (Vector3.Distance(transform.position, player.transform.position) < destroyDistance)
            {
                if (gameObject.CompareTag("Data"))
                {
                    PlayerController.Instance.AddDataResource();
                }
                else
                {
                    // Debug.Log("Ga Masuk" + gameObject.tag);
                }
                if (gameObject.CompareTag("Gear"))
                {
                    PlayerController.Instance.AddGearResource();
                }
                if (gameObject.CompareTag("Health"))
                {
                    PlayerController.Instance.AddHealth();
                }
                Destroy(gameObject);
            }
        }
    }


    // Metode untuk mengembalikan koin ke posisi awal
    public void ReturnToInitialPosition()
    {
        transform.position = initialPosition;
        isAttracted = false;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && other.gameObject.layer == LayerMask.NameToLayer("Hitbox"))
        {
            isAttracted = true;
        }
    }
}