using UnityEngine;
using UnityEngine.Events;

public class ButtonScript : MonoBehaviour
{
    [Header("Sprites")]
    [SerializeField] private Sprite offSprite;
    [SerializeField] private Sprite onSprite;

    [Header("Settings")]
    [SerializeField] private bool isToggle = false;
    [SerializeField] private float interactDistance = 3f;
    [SerializeField] private Transform player;

    [Header("Events")]
    public UnityEvent onClick;
    public UnityEvent onToggleOn;
    public UnityEvent onToggleOff;

    private SpriteRenderer sr;
    private AudioSource audioSource;
    private Collider trigger;
    private bool isOn = false;
    private bool isHeld = false;

    void Start()
    {
        sr = GetComponent<SpriteRenderer>();
        audioSource = GetComponentInChildren<AudioSource>();
        trigger = GetComponent<Collider>();
    }

    void Update()
    {
        // Handle release
        if (isHeld && Singleton<InputManager>.Instance.GetActionKeyUp(InputAction.Interact))
        {
            isHeld = false;
            if (!isToggle)
                SetSprite(false);
        }

        if (Singleton<InputManager>.Instance.GetActionKeyDown(InputAction.Interact) && Time.timeScale != 0f)
        {
            Ray ray = Camera.main.ScreenPointToRay(new Vector3((float)(Screen.width / 2), (float)(Screen.height / 2), 0f));
            RaycastHit raycastHit;

            if (Physics.Raycast(ray, out raycastHit))
            {
                if (raycastHit.collider == trigger
                    && Vector3.Distance(player.position, transform.position) < interactDistance)
                {
                    Press();
                }
            }
            else
            {
            }
        }
    }

    private void Press()
    {
        isHeld = true;

        if (isToggle)
        {
            if (isOn && onToggleOn != null && onToggleOn.GetPersistentEventCount() > 0)
            { onToggleOn.Invoke(); }
            else if (!isOn && onToggleOff != null && onToggleOff.GetPersistentEventCount() > 0)
            { onToggleOff.Invoke(); }
        }
        else
        {
            SetSprite(true);
        }

        audioSource?.Play();
        if (onClick != null && onClick.GetPersistentEventCount() > 0)
        {
            onClick.Invoke();
        }
            
    }

    public void SetState(bool state)
    {
        isOn = state;
        SetSprite(isOn);
    }

    private void SetSprite(bool on)
    {
        if (sr != null)
            sr.sprite = on ? onSprite : offSprite;
    }
}