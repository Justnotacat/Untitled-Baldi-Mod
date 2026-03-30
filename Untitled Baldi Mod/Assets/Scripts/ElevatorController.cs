using System.Collections;
using UnityEngine;

public class ElevatorController : MonoBehaviour
{
    [Header("Floor Settings")]
    [Tooltip("Set to TRUE if this elevator is on the 1st floor (goes UP). FALSE = 2nd floor (goes DOWN).")]
    public bool isOnFirstFloor = true;

    [Tooltip("How many units to move the player vertically when the elevator travels.")]
    public float floorTravelDistance = 10f;

    [Header("Player Snap Position")]
    [Tooltip("World position to snap the player to when they enter the elevator.")]
    public Vector3 playerSnapPosition = new Vector3(0f, 4f, 0f);

    [Tooltip("How fast the player lerps to the snap position.")]
    public float playerSnapSpeed = 8f;

    [Tooltip("The transform to actually move when the elevator travels (e.g. the root player object). If left empty, uses whatever transform is passed by the button.")]
    public Transform teleportTarget;

    [Header("Doors — Floor 1")]
    public Transform floor1LeftDoor;
    public Transform floor1RightDoor;

    [Header("Doors — Floor 2")]
    public Transform floor2LeftDoor;
    public Transform floor2RightDoor;

    [Header("Door Z Positions")]
    [Tooltip("Left door OPEN Z position (starting position).")]
    public float leftDoorOpenZ = -10f;

    [Tooltip("Left door CLOSED Z position.")]
    public float leftDoorClosedZ = -5f;

    [Tooltip("Right door OPEN Z position (starting position).")]
    public float rightDoorOpenZ = 0f;

    [Tooltip("Right door CLOSED Z position.")]
    public float rightDoorClosedZ = -5f;

    [Tooltip("How long (seconds) the doors take to open or close.")]
    public float doorTweenDuration = 1.5f;

    [Header("Timing")]
    [Tooltip("How long to wait (seconds) after doors close before the elevator moves.")]
    public float waitBeforeTravel = 5f;

    [Header("Audio")]
    [Tooltip("Played once all doors have finished CLOSING.")]
    public AudioClip doorClosedClip;

    [Tooltip("Played when the elevator arrives at the destination floor.")]
    public AudioClip arrivalClip;

    [Tooltip("Played the moment doors START to open after arrival.")]
    public AudioClip doorOpeningClip;

    private AudioSource audioSource;

    [Header("Misc")]
    public GameControllerScript gc; // elevator bool is gc.elevatorRunning

    // ─────────────────────────────────────────────
    // Unity Setup
    // ─────────────────────────────────────────────

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
    }

    // ─────────────────────────────────────────────
    // Public entry point — call this from your ButtonScript
    // ─────────────────────────────────────────────

    /// <summary>
    /// Call this from your ButtonScript when the button is pressed.
    /// Example:  elevatorController.OnButtonPressed(playerTransform);
    /// </summary>
    public void OnButtonPressed(Transform player)
    {
        Debug.Log($"[Elevator] Button pressed. gc.elevatorRunning={gc.elevatorRunning}");
        if (gc.elevatorRunning) return;
        StartCoroutine(ElevatorSequence(player));
    }

    // ─────────────────────────────────────────────
    // Main Sequence
    // ─────────────────────────────────────────────

    private IEnumerator ElevatorSequence(Transform player)
    {
        gc.elevatorRunning = true;
        Debug.Log("[Elevator] Sequence started.");

        PlayerScript ps = player.GetComponent<PlayerScript>();
        if (ps != null)
        {
            ps.canMove = false;
            ps.height = playerSnapPosition.y;
        }

        // 1 ── Snap player smoothly to elevator centre
        Debug.Log($"[Elevator] Snapping player. Current localPos: {player.localPosition}, Target: {playerSnapPosition}");
        yield return StartCoroutine(SnapPlayerToPosition(player, playerSnapPosition));
        Debug.Log($"[Elevator] Snap done. localPos is now: {player.localPosition}");

        // 2 ── Close all four doors simultaneously
        Debug.Log("[Elevator] Closing doors...");
        yield return StartCoroutine(TweenAllDoors(closing: true));
        Debug.Log("[Elevator] Doors closed.");

        // 3 ── Play "doors closed" audio
        Debug.Log($"[Elevator] Playing doorClosedClip ({(doorClosedClip ? doorClosedClip.name : "NULL")}).");
        PlayClip(doorClosedClip);

        // 4 ── Wait before travelling
        Debug.Log($"[Elevator] Waiting {waitBeforeTravel}s before travel...");
        yield return new WaitForSeconds(waitBeforeTravel);

        // 5 ── Teleport player up or down
        float direction = isOnFirstFloor ? 1f : -1f;
        Transform moveTarget = teleportTarget != null ? teleportTarget : player;
        Vector3 destination = moveTarget.position + new Vector3(0f, floorTravelDistance * direction, 0f);
        Debug.Log($"[Elevator] Teleporting '{moveTarget.name}'. isOnFirstFloor={isOnFirstFloor}, direction={direction}, from {moveTarget.position} to {destination}");
        moveTarget.position = destination;
        if (ps != null) ps.height = destination.y;
        Debug.Log($"[Elevator] Teleport done. New world pos: {moveTarget.position}");

        // 6 ── Play arrival audio
        Debug.Log($"[Elevator] Playing arrivalClip ({(arrivalClip ? arrivalClip.name : "NULL")}).");
        PlayClip(arrivalClip);

        yield return new WaitForSeconds(0.1f);

        // 7 ── Play door-opening audio BEFORE doors start moving
        Debug.Log($"[Elevator] Playing doorOpeningClip ({(doorOpeningClip ? doorOpeningClip.name : "NULL")}), then opening doors.");
        PlayClip(doorOpeningClip);

        // 8 ── Open all four doors simultaneously
        yield return StartCoroutine(TweenAllDoors(closing: false));
        Debug.Log("[Elevator] Doors fully open.");

        // ── Done!
        if (ps != null) ps.canMove = true;
        gc.elevatorRunning = false;
        Debug.Log("[Elevator] Sequence complete.");
    }

    // ─────────────────────────────────────────────
    // Player Snap
    // ─────────────────────────────────────────────

    private IEnumerator SnapPlayerToPosition(Transform player, Vector3 target)
    {
        Debug.Log($"[Elevator] SnapPlayerToPosition started. Target localPos: {target}");
        while (Vector3.Distance(player.localPosition, target) > 0.05f)
        {
            player.localPosition = Vector3.Lerp(player.localPosition, target, Time.deltaTime * playerSnapSpeed);
            yield return null;
        }
        player.localPosition = target;
        Debug.Log("[Elevator] SnapPlayerToPosition finished.");
    }

    // ─────────────────────────────────────────────
    // Door Tweening
    // ─────────────────────────────────────────────

    private IEnumerator TweenAllDoors(bool closing)
    {
        // Capture start positions
        float leftStart = closing ? leftDoorOpenZ : leftDoorClosedZ;
        float leftEnd = closing ? leftDoorClosedZ : leftDoorOpenZ;
        float rightStart = closing ? rightDoorOpenZ : rightDoorClosedZ;
        float rightEnd = closing ? rightDoorClosedZ : rightDoorOpenZ;

        float elapsed = 0f;

        while (elapsed < doorTweenDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / doorTweenDuration); // linear (no easing)

            SetDoorZ(floor1LeftDoor, Mathf.Lerp(leftStart, leftEnd, t));
            SetDoorZ(floor1RightDoor, Mathf.Lerp(rightStart, rightEnd, t));
            SetDoorZ(floor2LeftDoor, Mathf.Lerp(leftStart, leftEnd, t));
            SetDoorZ(floor2RightDoor, Mathf.Lerp(rightStart, rightEnd, t));

            yield return null;
        }

        // Snap to exact final positions
        SetDoorZ(floor1LeftDoor, leftEnd);
        SetDoorZ(floor1RightDoor, rightEnd);
        SetDoorZ(floor2LeftDoor, leftEnd);
        SetDoorZ(floor2RightDoor, rightEnd);
    }

    private void SetDoorZ(Transform door, float z)
    {
        if (door == null) return;
        Vector3 pos = door.localPosition;
        pos.z = z;
        door.localPosition = pos;
    }

    // ─────────────────────────────────────────────
    // Audio Helper
    // ─────────────────────────────────────────────

    private void PlayClip(AudioClip clip)
    {
        if (clip == null || audioSource == null) return;
        audioSource.PlayOneShot(clip);
    }
}