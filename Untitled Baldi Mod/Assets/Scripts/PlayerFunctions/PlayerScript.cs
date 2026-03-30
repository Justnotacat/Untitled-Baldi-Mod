using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

public class PlayerScript : MonoBehaviour
{
    private void Start()
    {
        if (PlayerPrefs.GetInt("AnalogMove") == 1)
        {
            sensitivityActive = true;
        }
        height = transform.position.y;
        stamina = maxStamina;
        playerRotation = transform.rotation;
        mouseSensitivity = PlayerPrefs.GetFloat("MouseSensitivity", 1f);
        principalBugFixer = 1;
        flipaturn = 1f;
    }
    private void Update()
    {
        if (canMove)
            transform.position = new Vector3(transform.position.x, height, transform.position.z);
        MouseMove();
        PlayerMove();
        StaminaCheck();
        GuiltCheck();
        if (cc.velocity.magnitude > 0f)
        {
            gc.LockMouse();
        }
        if (jumpRope & (transform.position - frozenPosition).magnitude >= 1f)
        {
            DeactivateJumpRope();
        }
        if (sweepingFailsave > 0f)
        {
            sweepingFailsave -= Time.deltaTime;
        }
        else
        {
            sweeping = false;
            hugging = false;
        }

        // if (Singleton<InputManager>.Instance.GetActionKey(InputAction.Test)) CharlieChatter();
        // if (Singleton<InputManager>.Instance.GetActionKey(InputAction.Test2)) Consume();
        if (Singleton<InputManager>.Instance.GetActionKey(InputAction.UseItem)) throwCharlie();

        if (charliePickup.hasCharlie)
        {
            charlieEventTimer -= Time.deltaTime;
            if (charlieEventTimer <= 0f)
                CharlieRandomEvent();
        }
        else
        {
            ResetCharlieEventTimer();
        }
        if (gc.item[gc.itemSelected] == 13)
        {
            charlieHolder.SetActive(true);
            audioSource.gameObject.SetActive(true);
        }
        else
        {
            charlieHolder.SetActive(false);
            audioSource.gameObject.SetActive(false);
        }
    }
    private void MouseMove()
    {
        playerRotation.eulerAngles = new Vector3(playerRotation.eulerAngles.x, playerRotation.eulerAngles.y, fliparoo);
        playerRotation.eulerAngles = playerRotation.eulerAngles + Vector3.up * Input.GetAxis("Mouse X") * mouseSensitivity * Time.timeScale * flipaturn;
        transform.rotation = playerRotation;
    }
    private void PlayerMove()
    {
        Vector3 movement = Vector3.zero;
        Vector3 lateralMovement = Vector3.zero;
        if (Singleton<InputManager>.Instance.GetActionKey(InputAction.MoveForward)) movement = transform.forward;
        if (Singleton<InputManager>.Instance.GetActionKey(InputAction.MoveBackward)) movement = -transform.forward;
        if (Singleton<InputManager>.Instance.GetActionKey(InputAction.MoveLeft)) lateralMovement = -transform.right;
        if (Singleton<InputManager>.Instance.GetActionKey(InputAction.MoveRight)) lateralMovement = transform.right;
        if (stamina > 0f)
        {
            if (Singleton<InputManager>.Instance.GetActionKey(InputAction.Run))
            {
                playerSpeed = runSpeed;
                sensitivity = 1f;
                if (cc.velocity.magnitude > 0.1f & !hugging & !sweeping)
                {
                    ResetGuilt("running", 0.1f);
                }
            }
            else
            {
                playerSpeed = walkSpeed;
                if (sensitivityActive)
                {
                    sensitivity = Mathf.Clamp((movement + lateralMovement).magnitude, 0f, 1f);
                }
                else
                {
                    sensitivity = 1f;
                }
            }
        }
        else
        {
            playerSpeed = walkSpeed;
            if (sensitivityActive)
            {
                sensitivity = Mathf.Clamp((lateralMovement + movement).magnitude, 0f, 1f);
            }
            else
            {
                sensitivity = 1f;
            }
        }
        playerSpeed *= Time.deltaTime;
        moveDirection = (movement + lateralMovement).normalized * playerSpeed * sensitivity;
        if (!(!jumpRope & !sweeping & !hugging))
        {
            if (sweeping && !bootsActive)
            {
                moveDirection = gottaSweep.velocity * Time.deltaTime + moveDirection * 0.3f;
            }
            else if (hugging && !bootsActive)
            {
                moveDirection = (firstPrize.velocity * 1.2f * Time.deltaTime + (new Vector3(firstPrizeTransform.position.x, height, firstPrizeTransform.position.z) + new Vector3((float)Mathf.RoundToInt(firstPrizeTransform.forward.x), 0f, (float)Mathf.RoundToInt(firstPrizeTransform.forward.z)) * 3f - transform.position)) * (float)principalBugFixer;
            }
            else if (jumpRope)
            {
                moveDirection = Vector3.zero;
            }
        }
        if (canMove)
            cc.Move(moveDirection);
    }
    private void StaminaCheck()
    {
        if (cc.velocity.magnitude > 0.1f)
        {
            if (Singleton<InputManager>.Instance.GetActionKey(InputAction.Run) & stamina > 0f)
            {
                stamina -= staminaRate * Time.deltaTime;
            }
            if (stamina < 0f & stamina > -5f)
            {
                stamina = -5f;
            }
        }
        else if (stamina < maxStamina)
        {
            stamina += staminaRate * Time.deltaTime;
        }
        staminaBar.value = stamina / maxStamina * 100f;
    }
    private void OnTriggerEnter(Collider other)
    {
        if (other.transform.name == "Baldi" & !gc.debugMode & !baldi.eating)
        {
            for (int i = 0; i < 3; i++)
            {
                if (gc.item[i] == 12) // Apple item ID
                {
                    gc.LoseItem(i);
                    baldi.StartCoroutine(baldi.Eat());
                    return;
                }
            }
            gameOver = true;
            RenderSettings.skybox = blackSky;
            StartCoroutine(KeepTheHudOff());
        }
        else if (other.transform.name == "Playtime" & !jumpRope & playtime.playCool <= 0f)
        {
            ActivateJumpRope();
        }
    }
    public IEnumerator KeepTheHudOff()
    {
        while (gameOver)
        {
            hud.enabled = false;
            mobile1.enabled = false;
            mobile2.enabled = false;
            jumpRopeScreen.SetActive(false);
            yield return new WaitForEndOfFrame();
        }
        yield break;
    }
    private void OnTriggerStay(Collider other)
    {
        if (other.transform.name == "Gotta Sweep")
        {
            sweeping = true;
            sweepingFailsave = 1f;
        }
        else if (other.transform.name == "1st Prize" & firstPrize.velocity.magnitude > 5f)
        {
            hugging = true;
            sweepingFailsave = 1f;
        }
    }
    private void OnTriggerExit(Collider other)
    {
        if (other.transform.name == "Office Trigger")
        {
            ResetGuilt("escape", door.lockTime);
        }
        else if (other.transform.name == "Gotta Sweep")
        {
            sweeping = false;
        }
        else if (other.transform.name == "1st Prize")
        {
            hugging = false;
        }
    }
    public void ResetGuilt(string type, float amount)
    {
        if (amount >= guilt)
        {
            guilt = amount;
            guiltType = type;
        }
    }
    private void GuiltCheck()
    {
        if (guilt > 0f)
        {
            guilt -= Time.deltaTime;
        }
    }
    public void ActivateJumpRope()
    {
        jumpRopeScreen.SetActive(true);
        jumpRope = true;
        frozenPosition = transform.position;
    }
    public void DeactivateJumpRope()
    {
        jumpRopeScreen.SetActive(false);
        jumpRope = false;
    }
    public void ActivateBoots()
    {
        bootsActive = true;
        StartCoroutine(BootTimer());
    }
    private IEnumerator BootTimer()
    {
        float time = 15f;
        while (time > 0f)
        {
            time -= Time.deltaTime;
            yield return null;
        }
        bootsActive = false;
        yield break;
    }

    public void GainCharlie()
    {
        StartCoroutine(GainCharlieDelay());
    }

    private IEnumerator GainCharlieDelay()
    {
        yield return null; // wait one frame for gc.item to update
        audioSource.PlayOneShot(charlieIntro);
        charlieHolder.GetComponent<Animator>().Play("CharliePickup");
        StartCoroutine(CharlieShake(charlieIntro.length));
    }

    private int lastCharlieIndex = -1;


    public CharliePickup charliePickup;

    private void CharlieChatter()
    {
        if (audioSource.isPlaying || !charliePickup.hasCharlie) return;

        if (gc.spoopMode)
        {
            baldi.Hear(transform.position, .5f);
        }

        int index;
        do
        {
            index = Random.Range(0, charlieLines.Count);
        } while (index == lastCharlieIndex);

        lastCharlieIndex = index;
        audioSource.clip = charlieLines[index];
        audioSource.Play();
        StartCoroutine(CharlieShake(charlieLines[index].length));
    }

    public void Consume()
    {
        List<int> occupiedSlots = new List<int>();
        for (int i = 0; i < gc.item.Length; i++)
        {
            if (gc.item[i] != 0)
                occupiedSlots.Add(i);
        }

        if (occupiedSlots.Count == 0) return;

        int randomSlot = occupiedSlots[Random.Range(0, occupiedSlots.Count)];
        if (audioSource.isPlaying || !charliePickup.hasCharlie || gc.item[randomSlot] == 13)
        {
            return;
        }

        gc.LoseItem(randomSlot);

        gc.itemSelected = gc.FindItemSlot(13);
        gc.UpdateItemSelection();

        audioSource.clip = crunch;
        audioSource.Play();
        StartCoroutine(CharlieShake(crunch.length));
    }

    private Vector2 charlieOriginalPos;

    public IEnumerator CharlieShake(float duration)
    {
        RectTransform rt = charlieHolder.transform.GetChild(0).GetComponent<RectTransform>();
        charlieOriginalPos = rt.anchoredPosition;
        float elapsed = 0f;
        float[] samples = new float[256];
        float bobAmount = 10000f;

        while (elapsed < duration)
        {
            if (gc.gamePaused)
            {
                audioSource.Pause();
                yield return new WaitUntil(() => !gc.gamePaused);
                audioSource.UnPause();
            }

            audioSource.GetSpectrumData(samples, 0, FFTWindow.BlackmanHarris);

            float volume = 0f;
            foreach (float s in samples) volume += s;
            volume /= samples.Length;

            rt.anchoredPosition = new Vector2(
                charlieOriginalPos.x,
                charlieOriginalPos.y + (volume * bobAmount)
            );

            elapsed += Time.deltaTime;
            yield return null;
        }

        // lerp back
        float lerpTime = 0.2f;
        float t = 0f;
        Vector2 shakeEndPos = rt.anchoredPosition;

        while (t < lerpTime)
        {
            t += Time.deltaTime;
            rt.anchoredPosition = Vector2.Lerp(shakeEndPos, charlieOriginalPos, t / lerpTime);
            yield return null;
        }

        rt.anchoredPosition = charlieOriginalPos;
    }

    public void throwCharlie()
    {
        if (!charliePickup.hasCharlie) return;
        if (audioSource.isPlaying) audioSource.Stop();
        if (gc.item[gc.itemSelected] == 13)
        {
            charliePickup.hasCharlie = false;
        }
    }

    private float charlieEventTimer;

    private void ResetCharlieEventTimer()
    {
        charlieEventTimer = Random.Range(5f, 10f);
    }

    private void CharlieRandomEvent()
    {
        if (!charliePickup.hasCharlie) return;

        if (audioSource.isPlaying) return;

        if (Random.value < charlieChatterChance)
        {
            if (gc.item[gc.itemSelected] != 13) return;
            CharlieChatter();
        }
        else
            Consume();

        ResetCharlieEventTimer();
    }

    public GameControllerScript gc;
    public BaldiScript baldi;
    public DoorScript door;
    public PlaytimeScript playtime;
    public bool gameOver;
    public bool canMove = true;
    public bool jumpRope;
    public bool sweeping;
    public bool hugging;
    public bool bootsActive;
    public int principalBugFixer;
    public float sweepingFailsave;
    public float fliparoo;
    public float flipaturn;
    private Quaternion playerRotation;
    public Vector3 frozenPosition;
    private bool sensitivityActive;
    private float sensitivity;
    public float mouseSensitivity;
    public float walkSpeed;
    public float runSpeed;
    public float slowSpeed;
    public float maxStamina;
    public float staminaRate;
    public float guilt;
    public float initGuilt;
    private Vector3 moveDirection;
    private float playerSpeed;
    public float stamina;
    public CharacterController cc;
    public NavMeshAgent gottaSweep;
    public NavMeshAgent firstPrize;
    public Transform firstPrizeTransform;
    public Slider staminaBar;
    public float db;
    public string guiltType;
    public GameObject jumpRopeScreen;
    public float height;
    public Material blackSky;
    public Canvas hud;
    public Canvas mobile1;
    public Canvas mobile2;
    public AudioClip charlieIntro;
    public List<AudioClip> charlieLines;
    public float charlieChatterChance;
    public AudioSource audioSource;
    public GameObject charlieHolder;
    public AudioClip crunch;
}