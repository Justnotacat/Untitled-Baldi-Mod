using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.SceneManagement;
using FluidMidi;

public class GameControllerScript : MonoBehaviour
{
    private void Start()
    {
        cullingMask = playerCamera.cullingMask;
        audioDevice = GetComponent<AudioSource>();
        mode = PlayerPrefs.GetString("CurrentMode");
        if (mode == "endless")
        {
            baldiScrpt.endless = true;
        }
        schoolMusic.Play();
        LockMouse();
        UpdateNotebookCount();
        itemSelected = 0;
        gameOverDelay = 0.5f;

        // Populate item UI from registry on start
        for (int i = 0; i < item.Length; i++)
        {
            itemSlot[i].texture = GetIconForItem(0); // empty slot texture
        }
        UpdateItemName();
    }

    private void Update()
    {
        if (!learningActive)
        {
            if (Singleton<InputManager>.Instance.GetActionKeyDown(InputAction.PauseOrCancel) && !player.gameOver)
            {
                if (!gamePaused) PauseGame();
                else UnpauseGame();
            }

            if (Input.GetKeyDown(KeyCode.Y) & gamePaused) ExitGame();
            else if (Input.GetKeyDown(KeyCode.N) & gamePaused) UnpauseGame();

            if (!gamePaused & Time.timeScale != 1f)
                Time.timeScale = 1f;

            if (Singleton<InputManager>.Instance.GetActionKeyDown(InputAction.UseItem) && Time.timeScale != 0f)
                UseItem();

            if (Input.GetAxis("Mouse ScrollWheel") > 0f && Time.timeScale != 0f)
                DecreaseItemSelection();
            else if (Input.GetAxis("Mouse ScrollWheel") < 0f && Time.timeScale != 0f)
                IncreaseItemSelection();

            if (Time.timeScale != 0f)
            {
                if (Singleton<InputManager>.Instance.GetActionKey(InputAction.Slot0)) { itemSelected = 0; UpdateItemSelection(); }
                else if (Singleton<InputManager>.Instance.GetActionKey(InputAction.Slot1)) { itemSelected = 1; UpdateItemSelection(); }
                else if (Singleton<InputManager>.Instance.GetActionKey(InputAction.Slot2)) { itemSelected = 2; UpdateItemSelection(); }
                else if (Singleton<InputManager>.Instance.GetActionKey(InputAction.Slot3)) { itemSelected = 3; UpdateItemSelection(); }
                else if (Singleton<InputManager>.Instance.GetActionKey(InputAction.Slot4)) { itemSelected = 4; UpdateItemSelection(); }
            }
        }
        else
        {
            if (Time.timeScale != 0f)
                Time.timeScale = 0f;
        }

        if (player.stamina < 0f & !warning.activeSelf)
            warning.SetActive(true);
        else if (player.stamina > 0f & warning.activeSelf)
            warning.SetActive(false);

        if (player.gameOver)
        {
            if (mode == "endless" && notebooks > PlayerPrefs.GetInt("HighBooks") && !highScoreText.activeSelf)
                highScoreText.SetActive(true);

            Time.timeScale = 0f;
            gameOverDelay -= Time.unscaledDeltaTime * 0.5f;
            playerCamera.farClipPlane = gameOverDelay * 400f;
            audioDevice.PlayOneShot(aud_buzz);

            if (gameOverDelay <= 0f)
            {
                if (mode == "endless")
                {
                    if (notebooks > PlayerPrefs.GetInt("HighBooks"))
                        PlayerPrefs.SetInt("HighBooks", notebooks);
                    PlayerPrefs.SetInt("CurrentBooks", notebooks);
                }
                Time.timeScale = 1f;
                SceneManager.LoadScene(GameOverScene);
            }
        }

    }

    // ── Notebooks ─────────────────────────────────────────────────────────────

    private void UpdateNotebookCount()
    {
        notebookCount.text = mode == "story"
            ? notebooks.ToString() + "/7 Notebooks"
            : notebooks.ToString() + " Notebooks";

        if (notebooks == 7 & mode == "story")
            ActivateFinaleMode();
    }

    public void CollectNotebook()
    {
        notebooks++;
        UpdateNotebookCount();
    }

    // ── Mouse / Pause ─────────────────────────────────────────────────────────

    public void LockMouse()
    {
        if (!learningActive)
        {
            cursorController.LockCursor();
            mouseLocked = true;
            reticle.SetActive(true);
        }
    }

    public void UnlockMouse()
    {
        cursorController.UnlockCursor();
        mouseLocked = false;
        reticle.SetActive(false);
    }

    public void PauseGame()
    {
        if (!learningActive)
        {
            UnlockMouse();
            Time.timeScale = 0f;
            gamePaused = true;
            pauseMenu.SetActive(true);
            AudioListener.pause = true;  // pause all audio
            SongPlayer[] midis = FindObjectsOfType<SongPlayer>();
            foreach (SongPlayer songmidi in midis)
            {
                songmidi.Pause();
            }
        }
    }

    public void UnpauseGame()
    {
        Time.timeScale = 1f;
        gamePaused = false;
        pauseMenu.SetActive(false);
        LockMouse();
        AudioListener.pause = false;  // resume all audio
        SongPlayer[] midis = FindObjectsOfType<SongPlayer>();
        foreach (SongPlayer songmidi in midis)
        {
            songmidi.Resume();
        }
    }

    public void ExitGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(ExitGameScene);
    }

    // ── Spoop / Finale ────────────────────────────────────────────────────────

    public void ActivateSpoopMode()
    {
        spoopMode = true;
        foreach (var e in entrances) e.Lower();
       baldiTutor.SetActive(false);
        baldi.SetActive(true);
        principal.SetActive(true);
        crafters.SetActive(true);
        playtime.SetActive(true);
        gottaSweep.SetActive(true);
        bully.SetActive(true);
        firstPrize.SetActive(true);
        audioDevice.PlayOneShot(aud_Hang);
        learnMusic.Stop();
        schoolMusic.Stop();
    }

    private void ActivateFinaleMode()
    {
        finaleMode = true;
        foreach (var e in entrances) e.Raise();
    }

    public void GetAngry(float value)
    {
        if (!spoopMode) ActivateSpoopMode();
        baldiScrpt.GetAngry(value);
    }

    // ── Learning game ─────────────────────────────────────────────────────────

    public void ActivateLearningGame()
    {
        learningActive = true;
        UnlockMouse();
        tutorBaldi.Stop();
        if (!spoopMode)
        {
            schoolMusic.Stop();
            learnMusic.Play();
        }
    }

    public void DeactivateLearningGame(GameObject subject)
    {
        playerCamera.cullingMask = cullingMask;
        learningActive = false;
        Destroy(subject);
        LockMouse();
        if (player.stamina < 100f)
            player.stamina = 100f;
        if (!spoopMode)
        {
            schoolMusic.Play();
            learnMusic.Stop();
        }
        if (notebooks == 1 & !spoopMode)
        {
            quarter.SetActive(true);
            tutorBaldi.PlayOneShot(aud_Prize);
        }
        else if (notebooks == 7 & mode == "story")
        {
            audioDevice.PlayOneShot(aud_AllNotebooks, 0.8f);
            startLoopAudio.Play();
        }
    }

    // ── Item selection ────────────────────────────────────────────────────────

    public float lerpSpeed = 10f; // Adjust for faster/slower sliding
    private Coroutine moveCoroutine;
    [SerializeField] private AudioSource audioSource;

    private void IncreaseItemSelection()
    {
        
        itemSelected = (itemSelected + 1) % 5;
        MoveItemSelect();
        UpdateItemName();
    }

    private void DecreaseItemSelection()
    {
        itemSelected = (itemSelected + 4) % 5;
        MoveItemSelect();
        UpdateItemName();
    }

    public void UpdateItemSelection()
    {
        MoveItemSelect();
        UpdateItemName();
    }

    public int FindItemSlot(int item_ID)
    {
        for (int i = 0; i < item.Length; i++)
        {
            if (item[i] == item_ID) return i;
        }
        return -1;
    }

    private void MoveItemSelect()
    {
        if (moveCoroutine != null)
            StopCoroutine(moveCoroutine);
        audioSource.PlayOneShot(aud_Click);
        moveCoroutine = StartCoroutine(LerpItemSelect(itemSelectOffset[itemSelected]));
    }

    private IEnumerator LerpItemSelect(float targetX)
    {
        Vector2 target = new Vector2(targetX, itemSelectPosition);

        while (Vector2.Distance(itemSelect.anchoredPosition, target) > 0.1f)
        {
            itemSelect.anchoredPosition = Vector2.Lerp(
                itemSelect.anchoredPosition,
                target,
                Time.deltaTime * lerpSpeed
            );
            yield return null;
        }

        itemSelect.anchoredPosition = target; // Snap to final position
    }

    // ── Item collection ───────────────────────────────────────────────────────

    public int CollectItem(int item_ID)
    {
        int slot = -1;
        for (int i = 0; i < item.Length; i++)
        {
            if (item[i] == 0) { slot = i; break; }
        }
        if (slot == -1) slot = itemSelected;

        item[slot] = item_ID;
        itemSlot[slot].texture = GetIconForItem(item_ID);
        UpdateItemName();
        return slot; // <-- add this
    }

    // ── Item use ──────────────────────────────────────────────────────────────

    public void UseItem()
    {
        if (item[itemSelected] == 0) return;
        itemUseHandler.Execute(item[itemSelected], ResetItem);
    }

    public void ResetItem()
    {
        item[itemSelected] = 0;
        itemSlot[itemSelected].texture = GetIconForItem(0);
        UpdateItemName();
    }

    public void LoseItem(int slotIndex)
    {
        item[slotIndex] = 0;
        itemSlot[slotIndex].texture = GetIconForItem(0);
        UpdateItemName();
    }

    private void UpdateItemName()
    {
        var def = itemRegistry.GetByID(item[itemSelected]);
        itemText.text = def != null ? def.itemName : "Nothing";
    }

    /// <summary>Returns the icon texture for the given item ID (0 = empty slot).</summary>
    private Texture GetIconForItem(int id)
    {
        if (id == 0) return emptySlotTexture;
        var def = itemRegistry.GetByID(id);
        return def != null ? def.icon : emptySlotTexture;
    }

    // ── Exits ─────────────────────────────────────────────────────────────────

    public void ExitReached()
    {
        exitsReached++;
        if (exitsReached == 1)
        {
            Color startColor = RenderSettings.ambientLight;
            float startStrength = pinCushion.GetFloat("_Strength");
            StartCoroutine(LerpEffects(startColor, startStrength));
        }
    }

    private IEnumerator LerpEffects(Color startColor, float startStrength)
    {
        float duration = 2f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0, 1, elapsed / duration);

            RenderSettings.ambientLight = Color.Lerp(startColor, Color.red, t);
            pinCushion.SetFloat("_Strength", Mathf.Lerp(startStrength, -0.1f, t));

            yield return null;
        }

        RenderSettings.ambientLight = Color.red;
        pinCushion.SetFloat("_Strength", -0.06f);
    }

    // ── Misc ──────────────────────────────────────────────────────────────────

    public void Fliparoo()
    {
        flipped = true;
        player.height = 6f;
        player.fliparoo = 180f;
        player.flipaturn = -1f;
        Camera.main.GetComponent<CameraScript>().offset = new Vector3(0f, -1f, 0f);
    }

    public void Explode(GameObject gameObject)
    {
        Instantiate(explosion, gameObject.transform.position, gameObject.transform.rotation);
        Destroy(gameObject);
    }

    // ── Fields ────────────────────────────────────────────────────────────────

    [Header("Item System")]
    public ItemRegistry itemRegistry;       // drag your ItemRegistry asset here
    public ItemUseHandler itemUseHandler;   // drag the ItemUseHandler component here
    public Texture emptySlotTexture;        // the blank item slot texture (was itemTextures[0])

    [Header("Player")]
    public CursorControllerScript cursorController;
    public PlayerScript player;
    public Transform playerTransform;
    public CharacterController playerCharacter;
    public Collider playerCollider;
    public AILocationSelectorScript AILocationSelector;
    public Transform cameraTransform;
    public Camera playerCamera;
    private int cullingMask;

    [Header("Baldi")]
    public GameObject baldiTutor;
    public GameObject baldi;
    public BaldiScript baldiScrpt;
    public AudioSource tutorBaldi;
    public AudioClip aud_Prize;
    public AudioClip aud_PrizeMobile;
    public AudioClip aud_AllNotebooks;

    [Header("Characters")]
    public GameObject principal;
    public GameObject crafters;
    public GameObject playtime;
    public PlaytimeScript playtimeScript;
    public GameObject gottaSweep;
    public GameObject bully;
    public GameObject firstPrize;
    public GameObject TestEnemy;
    public FirstPrizeScript firstPrizeScript;

    [Header("Entrances")]
    public EntranceScript[] entrances;

    [Header("UI")]
    public TMP_Text itemText;
    public RawImage[] itemSlot = new RawImage[3];
    public TMP_Text notebookCount;
    public GameObject pauseMenu;
    public GameObject highScoreText;
    public GameObject warning;
    public GameObject reticle;
    public RectTransform itemSelect;
    public int[] itemSelectOffset;
    public int itemSelectPosition;
    public RectTransform boots;
    public GameObject quarter;

    [Header("Audio")]
    public AudioClip aud_Teleport;
    public AudioClip aud_buzz;
    public AudioClip aud_Hang;
    public AudioClip aud_MachineQuiet;
    public AudioClip aud_MachineStart;
    public AudioClip aud_MachineRev;
    public AudioClip aud_MachineLoop;
    public AudioClip aud_Switch;
    public AudioClip aud_Click;
    public SongPlayer schoolMusic;
    public AudioSource learnMusic;

    [Header("Scenes")]
    public string ExitGameScene;
    public string GameOverScene;

    [Header("State")]
    public string mode;
    public int notebooks;
    public int failedNotebooks;
    public bool spoopMode;
    public bool finaleMode;
    public bool debugMode;
    public bool mouseLocked;
    public int exitsReached;
    public int itemSelected;
    public int[] item = new int[3];
    public bool gamePaused;
    private bool learningActive;
    private float gameOverDelay;
    private bool flipped;

    [Header("Miscellaneous")]
    private AudioSource audioDevice;
    public StartLoopAudio startLoopAudio;
    public GameObject explosion;
    public Material pinCushion;
    public bool elevatorRunning;

}