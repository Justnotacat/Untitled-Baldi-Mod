using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HandControllerScript : MonoBehaviour
{
    // Start is called before the first frame update

    private void Start()

    {


        
    }

    // Update is called once per frame

    private void Update()

    {

        if (Input.GetKey(KeyCode.W) | Input.GetKey(KeyCode.D) | Input.GetKey(KeyCode.A) | Input.GetKey(KeyCode.S))

        {

            this.hands.SetTrigger("Walking"); // sets walking animation

            this.hands.ResetTrigger("Idle"); // resets idle

            this.isWalking = true; // sets trigger if walking

        }

        else

        {
        
            this.hands.ResetTrigger("Walking");

            this.hands.SetTrigger("Idle");

            this.isWalking = false; // disables boolean

        }
        
    }

    public Animator hands;

    public bool isWalking;
}