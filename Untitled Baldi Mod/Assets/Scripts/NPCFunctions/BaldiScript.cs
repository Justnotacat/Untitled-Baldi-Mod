using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class BaldiScript : MonoBehaviour
{
    private void Start()
    {
        baldiAudio = GetComponent<AudioSource>();
        agent = GetComponent<NavMeshAgent>();
        timeToMove = baseTime;
        Wander();
        if (PlayerPrefs.GetInt("Rumble") == 1)
        {
            rumble = true;
        }
    }

    private void Update()
    {
        if (!eating)
        {
            if (timeToMove > 0f)
            {
                timeToMove -= 1f * Time.deltaTime;
            }
            else
            {
                Move();
            }
        }

        if (coolDown > 0f)
        {
            coolDown -= 1f * Time.deltaTime;
        }
        if (baldiTempAnger > 0f)
        {
            baldiTempAnger -= 0.02f * Time.deltaTime;
        }
        else
        {
            baldiTempAnger = 0f;
        }
        if (antiHearingTime > 0f)
        {
            antiHearingTime -= Time.deltaTime;
        }
        else
        {
            antiHearing = false;
        }
        if (endless)
        {
            if (timeToAnger > 0f)
            {
                timeToAnger -= 1f * Time.deltaTime;
            }
            else
            {
                timeToAnger = angerFrequency;
                GetAngry(angerRate);
                angerRate += angerRateRate;
            }
        }
    }

    private void FixedUpdate()
    {
        if (moveFrames > 0f)
        {
            moveFrames -= 1f;
            agent.speed = speed;
        }
        else
        {
            agent.speed = 0f;
        }
        Vector3 direction = player.position - transform.position;
        RaycastHit raycastHit;
        if (Physics.Raycast(transform.position + Vector3.up * 2f, direction, out raycastHit, float.PositiveInfinity, 769, QueryTriggerInteraction.Ignore) & raycastHit.transform.tag == "Player")
        {
            db = true;
            TargetPlayer();
        }
        else
        {
            db = false;
        }
    }

    private void Wander()
    {
        wanderer.GetNewTarget();
        agent.SetDestination(wanderTarget.position);
        coolDown = 1f;
        currentPriority = 0f;
    }

    public void TargetPlayer()
    {
        agent.SetDestination(player.position);
        coolDown = 1f;
        currentPriority = 0f;
    }

    private void Move()
    {
        if (transform.position == previous & coolDown < 0f)
        {
            Wander();
        }
        moveFrames = 10f;
        timeToMove = baldiWait - baldiTempAnger;
        previous = transform.position;
        baldiAudio.PlayOneShot(slap);
        baldiAnimator.SetTrigger("slap");
        if (rumble)
        {
            float num = Vector3.Distance(transform.position, player.position);
            if (num < vibrationDistance)
            {
                float motorLevel = 1f - num / vibrationDistance;
            }
        }
    }

    public IEnumerator Eat()
    {
        eating = true;
        baldiAnimator.enabled = false;
        baldiAudio.PlayOneShot(appleForMe);
        baldiSprite.sprite = eatSprite2;

        yield return new WaitForSeconds(5);

        float elapsedTime = 0f;
        float duration = 10f;
        float interval = 0.05f;
        float yumInterval = 2f;
        float timeSinceLastYum = 0f;

        baldiAudio.loop = false;
        baldiAudio.clip = yum;
        baldiAudio.Play();

        while (elapsedTime < duration)
        {
            timeSinceLastYum += interval;
            if (timeSinceLastYum >= yumInterval)
            {
                baldiAudio.PlayOneShot(yum);
                timeSinceLastYum = 0f;
            }

            AudioClip selectedClip = UnityEngine.Random.Range(0, 2) == 0 ? crunch1 : crunch2;
            baldiAudio.PlayOneShot(selectedClip);

            baldiSprite.sprite = UnityEngine.Random.Range(0, 2) == 0 ? eatSprite2 : eatSprite3;

            yield return new WaitForSeconds(interval);

            elapsedTime += interval;
        }

        baldiAudio.Stop();
        baldiSprite.sprite = eatSprite2;
        baldiAnimator.enabled = true;
        eating = false;
    }

    public void GetAngry(float value)
    {
        baldiAnger += value;
        if (baldiAnger < 0.5f)
        {
            baldiAnger = 0.5f;
        }
        baldiWait = -3f * baldiAnger / (baldiAnger + 2f / baldiSpeedScale) + 3f;
    }

    public void GetTempAngry(float value)
    {
        baldiTempAnger += value;
    }

    public void Hear(Vector3 soundLocation, float priority)

    {

        if (!this.antiHearing &&
            !eating &&
            priority >= this.currentPriority)

        {

            this.agent.SetDestination(soundLocation);//Go to that sound

            this.currentPriority = priority;//Set the current priority to the priority

            this.Baldicator.Play("Baldicator_Look", -1, 0f);

        }

        else

        {

            this.Baldicator.Play("Baldicator_Think", -1, 0f);

        }

    }

    public void ActivateAntiHearing(float t)
    {
        Wander();
        antiHearing = true;
        antiHearingTime = t;
    }

    // Original fields
    public bool db;
    public float baseTime;
    public float speed;
    public float timeToMove;
    public float baldiAnger;
    public float baldiTempAnger;
    public float baldiWait;
    public float baldiSpeedScale;
    private float moveFrames;
    private float currentPriority;
    public bool antiHearing;
    public float antiHearingTime;
    public float vibrationDistance;
    public float angerRate;
    public float angerRateRate;
    public float angerFrequency;
    public float timeToAnger;
    public bool endless;
    public Transform player;
    public Transform wanderTarget;
    public AILocationSelectorScript wanderer;
    private AudioSource baldiAudio;
    public AudioClip slap;
    public Animator baldiAnimator;
    public float coolDown;
    private Vector3 previous;
    private bool rumble;
    private NavMeshAgent agent;

    public Animator Baldicator;

    // Apple/eating fields
    public AudioClip appleForMe;
    public AudioClip crunch1;
    public AudioClip crunch2;
    public AudioClip yum;
    public Sprite eatSprite;
    public Sprite eatSprite2;
    public Sprite eatSprite3;
    public SpriteRenderer baldiSprite;
    public bool eating;
}