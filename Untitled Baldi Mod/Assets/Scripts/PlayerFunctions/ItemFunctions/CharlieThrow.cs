using UnityEngine;
public class CharlieThrow : MonoBehaviour
{
    private void Awake()
    {
        GameObject player = GameObject.Find("Player");
        if (player != null)
            Physics.IgnoreCollision(GetComponent<Collider>(), player.GetComponent<Collider>());
    }

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.velocity = transform.forward * speed;
        lifeSpan = scream.length;
        audioSource.clip = scream;
        audioSource.Play();
        gc = FindObjectOfType<GameControllerScript>();
    }

    private void Update()
    {
        if (lifeSpan <= 0f)
        {
            gc.Explode(gameObject);
        }
        else
        {
            charlie.transform.Rotate(Vector3.forward * 720f * Time.deltaTime);
            lifeSpan -= Time.deltaTime;
            rb.velocity = transform.forward * speed;
        }
    }
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player")) return;
        audioSource.Stop();
        gc.Explode(gameObject);
    }
    public float speed;
    private float lifeSpan;
    private Rigidbody rb;
    public AudioClip scream;
    public AudioSource audioSource;
    [SerializeField] private GameObject charlie;
    public GameControllerScript gc;
}