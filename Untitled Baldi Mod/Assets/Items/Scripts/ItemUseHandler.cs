using UnityEngine;
using System.Collections;

public class ItemUseHandler : MonoBehaviour
{
    [Header("References")]
    public GameControllerScript gc;
    public PlayerScript player;
    public Transform playerTransform;
    public Transform cameraTransform;
    public BaldiScript baldiScrpt;
    public GameObject baldi;
    public PlaytimeScript playtimeScript;
    public FirstPrizeScript firstPrizeScript;
    public AILocationSelectorScript AILocationSelector;
    public GameObject bsodaSpray;
    public GameObject charlieThrowable;
    public GameObject alarmClock;
    public RectTransform boots;
    public CharacterController playerCharacter;
    public Collider playerCollider;

    [Header("Audio")]
    public AudioSource audioDevice;
    public AudioClip aud_Soda;
    public AudioClip aud_Cronch;
    public AudioClip aud_Spray;
    public AudioClip aud_Teleport;


    private bool flipped;

    /// <summary>
    /// Called by GameControllerScript when the player uses the currently selected item.
    /// Returns true if the item was successfully consumed.
    /// </summary>
    public bool Execute(int itemID, System.Action onConsumed)
    {
        if (itemID == 0) return false;

        if (itemID == 1) return UseZestyBar(onConsumed);
        if (itemID == 2) return UseYellowLock(onConsumed);
        if (itemID == 3) return UseKeys(onConsumed);
        if (itemID == 4) return UseBSODA(onConsumed);
        if (itemID == 5) return UseQuarter(onConsumed);
        if (itemID == 6) return UseTape(onConsumed);
        if (itemID == 7) return UseAlarmClock(onConsumed);
        if (itemID == 8) return UseWDNoSquee(onConsumed);
        if (itemID == 9) return UseScissors(onConsumed);
        if (itemID == 10) return UseBoots(onConsumed);
        if (itemID == 11) return UseTeleporter(onConsumed);
        if (itemID == 12) return UseApple(onConsumed);
        if (itemID == 13) return UseCharlie(onConsumed);

        Debug.LogWarning($"ItemUseHandler: No handler for item ID {itemID}");
        return false;
    }

    // ── Item implementations ──────────────────────────────────────────────────

    private bool UseZestyBar(System.Action onConsumed)
    {
        player.stamina = player.maxStamina * 2f;
        audioDevice.PlayOneShot(aud_Cronch);
        onConsumed();
        return true;
    }

    private bool UseYellowLock(System.Action onConsumed)
    {
        if (!RaycastTag("SwingingDoor", 10f, out var hit)) return false;
        hit.collider.gameObject.GetComponent<SwingingDoorScript>().LockDoor(15f);
        onConsumed();
        return true;
    }

    private bool UseKeys(System.Action onConsumed)
    {
        if (!RaycastTag("Door", 10f, out var hit)) return false;
        var door = hit.collider.gameObject.GetComponent<DoorScript>();
        if (!door.DoorLocked) return false;
        door.UnlockDoor();
        door.OpenDoor();
        onConsumed();
        return true;
    }

    private bool UseBSODA(System.Action onConsumed)
    {
        Instantiate(bsodaSpray, playerTransform.position, cameraTransform.rotation);
        player.ResetGuilt("drink", 1f);
        audioDevice.PlayOneShot(aud_Soda);
        onConsumed();
        return true;
    }

    private bool UseQuarter(System.Action onConsumed)
    {
        if (!RaycastNamed(10f, out var hit)) return false;

        if (hit.collider.name == "BSODAMachine") { onConsumed(); gc.CollectItem(4); return true; }
        if (hit.collider.name == "ZestyMachine") { onConsumed(); gc.CollectItem(1); return true; }
        if (hit.collider.name == "PayPhone")
        {
            hit.collider.gameObject.GetComponent<TapePlayerScript>().Play();
            onConsumed();
            return true;
        }
        return false;
    }

    private bool UseTape(System.Action onConsumed)
    {
        if (!RaycastNamed(10f, out var hit) || hit.collider.name != "TapePlayer") return false;
        hit.collider.gameObject.GetComponent<TapePlayerScript>().Play();
        onConsumed();
        return true;
    }

    private bool UseAlarmClock(System.Action onConsumed)
    {
        var obj = Instantiate(alarmClock, playerTransform.position, cameraTransform.rotation);
        obj.GetComponent<AlarmClockScript>().baldi = baldiScrpt;
        onConsumed();
        return true;
    }

    private bool UseWDNoSquee(System.Action onConsumed)
    {
        if (!RaycastTag("Door", 10f, out var hit)) return false;
        hit.collider.gameObject.GetComponent<DoorScript>().SilenceDoor();
        audioDevice.PlayOneShot(aud_Spray);
        onConsumed();
        return true;
    }

    private bool UseScissors(System.Action onConsumed)
    {
        if (player.jumpRope)
        {
            player.DeactivateJumpRope();
            playtimeScript.Disappoint();
            onConsumed();
            return true;
        }
        Ray ray = Camera.main.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0f));
        if (Physics.Raycast(ray, out var hit) && hit.collider.name == "1st Prize")
        {
            firstPrizeScript.GoCrazy();
            onConsumed();
            return true;
        }
        return false;
    }

    private bool UseBoots(System.Action onConsumed)
    {
        player.ActivateBoots();
        StartCoroutine(BootAnimation());
        onConsumed();
        return true;
    }

    private bool UseTeleporter(System.Action onConsumed)
    {
        StartCoroutine(Teleporter());
        onConsumed();
        return true;
    }

    private bool UseApple(System.Action onConsumed)
    {
        var baldiScript = baldi.GetComponent<BaldiScript>();
        if (baldiScript.eating) return false;
        gc.LoseItem(gc.itemSelected);
        baldiScript.StartCoroutine(baldiScript.Eat());
        return true;
    }

    private bool UseCharlie(System.Action onConsumed)
    {
        Instantiate(charlieThrowable, playerTransform.position, cameraTransform.rotation);
        onConsumed();
        return true;
    }

    // ── Coroutines ────────────────────────────────────────────────────────────

    private IEnumerator BootAnimation()
    {
        float time = 15f;
        float height = 375f;
        Vector3 position;
        boots.gameObject.SetActive(true);

        while (height > -375f)
        {
            height -= 375f * Time.deltaTime;
            time -= Time.deltaTime;
            position = boots.localPosition;
            position.y = height;
            boots.localPosition = position;
            yield return null;
        }

        position = boots.localPosition;
        position.y = -375f;
        boots.localPosition = position;
        boots.gameObject.SetActive(false);

        while (time > 0f)
        {
            time -= Time.deltaTime;
            yield return null;
        }

        boots.gameObject.SetActive(true);

        while (height < 375f)
        {
            height += 375f * Time.deltaTime;
            position = boots.localPosition;
            position.y = height;
            boots.localPosition = position;
            yield return null;
        }

        position = boots.localPosition;
        position.y = 375f;
        boots.localPosition = position;
        boots.gameObject.SetActive(false);
    }

    private IEnumerator Teleporter()
    {
        playerCharacter.enabled = false;
        playerCollider.enabled = false;

        int teleports = Random.Range(12, 16);
        int teleportCount = 0;
        float baseTime = 0.2f;
        float currentTime = baseTime;
        float increaseFactor = 1.1f;

        while (teleportCount < teleports)
        {
            currentTime -= Time.deltaTime;
            if (currentTime < 0f)
            {
                DoTeleport();
                teleportCount++;
                baseTime *= increaseFactor;
                currentTime = baseTime;
            }

            player.height = flipped ? 6f : 4f;
            yield return null;
        }

        playerCharacter.enabled = true;
        playerCollider.enabled = true;
    }

    private void DoTeleport()
    {
        AILocationSelector.GetNewTarget();
        player.transform.position = AILocationSelector.transform.position + Vector3.up * player.height;
        audioDevice.PlayOneShot(aud_Teleport);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private bool RaycastTag(string tag, float maxDist, out RaycastHit hit)
    {
        Ray ray = Camera.main.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0f));
        return Physics.Raycast(ray, out hit)
            && hit.collider.CompareTag(tag)
            && Vector3.Distance(playerTransform.position, hit.transform.position) <= maxDist;
    }

    private bool RaycastNamed(float maxDist, out RaycastHit hit)
    {
        Ray ray = Camera.main.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0f));
        return Physics.Raycast(ray, out hit)
            && Vector3.Distance(playerTransform.position, hit.transform.position) <= maxDist;
    }
}