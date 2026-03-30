using UnityEngine;

public class CameraScript : MonoBehaviour
{
    private void Start()
    {
        offset = transform.position - player.transform.position;
        cam = GetComponent<Camera>();
        currentFov = baseFov;
        cam.fieldOfView = baseFov;
    }

    private void Update()
    {
        if (ps.jumpRope)
        {
            velocity -= gravity * Time.deltaTime;
            jumpHeight += velocity * Time.deltaTime;
            if (jumpHeight <= 0f)
            {
                jumpHeight = 0f;
                if (Singleton<InputManager>.Instance.GetActionKey(InputAction.Jump))
                {
                    velocity = initVelocity;
                }
            }
            jumpHeightV3 = new Vector3(0f, jumpHeight, 0f);
        }
        else if (Singleton<InputManager>.Instance.GetActionKey(InputAction.LookBehind))
        {
            lookBehind = 180;
        }
        else
        {
            lookBehind = 0;
        }

        UpdateCameraBob();
        UpdateFov();
    }

    private bool wasMoving;

    private void UpdateCameraBob()
    {
        bool isMoving = ps.cc.velocity.magnitude > 0.1f;
        bool isRunning = Singleton<InputManager>.Instance.GetActionKey(InputAction.Run) && ps.stamina > 0f;

        if (isMoving && !ps.gameOver && !ps.jumpRope)
        {
            if (!wasMoving)
            {
                bobTimer = 0f;
            }

            float currentBobSpeed = isRunning ? bobSpeed * runBobMultiplier : bobSpeed;
            float currentBobAmount = bobAmount;

            bobTimer += Time.deltaTime * currentBobSpeed;
            bobOffset = Mathf.Sin(bobTimer) * currentBobAmount;
        }
        else
        {
            bobOffset = Mathf.Lerp(bobOffset, 0f, Time.deltaTime * bobSettleSpeed);
            if (Mathf.Abs(bobOffset) < 0.001f)
            {
                bobOffset = 0f;
                bobTimer = 0f;
            }
        }

        wasMoving = isMoving;
    }

    private void UpdateFov()
    {
        if (ps.gameOver || ps.jumpRope)
        {
            currentFov = Mathf.Lerp(currentFov, baseFov, Time.deltaTime * fovLerpSpeed);
            cam.fieldOfView = currentFov;
            return;
        }

        bool isRunning = Singleton<InputManager>.Instance.GetActionKey(InputAction.Run);
        bool isMoving = ps.cc.velocity.magnitude > 0.1f;

        float targetFov = (isRunning && isMoving && ps.stamina > 0f) ? runFov : baseFov;
        currentFov = Mathf.Lerp(currentFov, targetFov, Time.deltaTime * fovLerpSpeed);
        cam.fieldOfView = currentFov;
    }

    private void LateUpdate()
    {
        Vector3 bobV3 = new Vector3(0f, bobOffset, 0f);

        transform.position = player.transform.position + offset;

        if (!ps.gameOver && !ps.jumpRope)
        {
            transform.position = player.transform.position + offset + bobV3;
            transform.rotation = player.transform.rotation * Quaternion.Euler(0f, (float)lookBehind, 0f);
        }
        else if (ps.gameOver)
        {
            transform.position = baldi.transform.position + baldi.transform.forward * BaldiOffset.z + new Vector3(0f, BaldiOffset.y, 0f);
            transform.LookAt(new Vector3(baldi.position.x, baldi.position.y + BaldiOffset.y, baldi.position.z));
        }
        else if (ps.jumpRope)
        {
            transform.position = player.transform.position + offset + jumpHeightV3;
            transform.rotation = player.transform.rotation;
        }
    }

    // --- Existing fields ---
    public GameObject player;
    public PlayerScript ps;
    public Transform baldi;
    public Vector3 BaldiOffset;
    public float initVelocity;
    public float velocity;
    public float gravity;
    private int lookBehind;
    public Vector3 offset;
    public float jumpHeight;
    public Vector3 jumpHeightV3;

    // --- Bob fields ---
    public float bobSpeed = 8f;
    public float bobAmount = 0.04f;
    public float runBobMultiplier = 1.6f;
    public float bobSettleSpeed = 8f;
    private float bobTimer;
    private float bobOffset;

    // --- FOV fields ---
    private Camera cam;
    private float currentFov;
    public float baseFov = 60f;
    public float runFov = 70f;
    public float fovLerpSpeed = 6f;
}