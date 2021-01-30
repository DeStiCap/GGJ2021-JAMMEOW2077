﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    #region Variable

    public Character2DController charactorController;
    public LayerMask whatIsLadder;
    public GameObject[] AllCrate;

    [Header("Number")]
    [Space]
    public float climbSpeed = 20f;
    public float runSpeed = 40f;
    public float dragSpeed = 1;
    [Range(0, 2)] public float rayUpDistance;
    [Range(0, 1)] public float rayFrontDistrance = 0.05f;

    protected float horizontalMove = 0f;
    protected float verticalMove = 0f;

    protected bool climb = false;
    protected bool holdCrate = false;
    protected bool jump = false;
    protected bool crouch = false;

    #endregion

    #region Base - Mono

    protected virtual void Awake()
    {
        AllCrate = GameObject.FindGameObjectsWithTag("Crate");
    }

    #endregion

    #region Base - Main

    protected virtual void MoveInput()
    {
        verticalMove = Input.GetAxisRaw(gameObject.tag + " Vertical") * climbSpeed;
        horizontalMove = Input.GetAxisRaw(gameObject.tag + " Horizontal") * runSpeed * dragSpeed;
    }

    protected virtual void Move()
    {
        charactorController.Move(horizontalMove * Time.fixedDeltaTime, crouch, jump);
    }

    protected virtual void ClimbLadder(KeyCode keyUp, KeyCode keyDown)
    {
        // Raycast for ladder climbing
        RaycastHit2D topHit = Physics2D.Raycast(transform.position, Vector2.up, rayUpDistance, whatIsLadder);

        if (topHit.collider != null)
        {
            GameObject ladder = topHit.collider.gameObject;
            if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.S))
            {
                gameObject.layer = LayerMask.NameToLayer("Climb");
                climb = true;
            }
        }
        else
        {
            gameObject.layer = LayerMask.NameToLayer("Player");
            climb = false;
        }
        charactorController.Climb(verticalMove * Time.fixedDeltaTime, climb);
        Debug.DrawRay(transform.position, Vector2.up * rayUpDistance, Color.red);
    }

    protected virtual void CheckHoldingCrate()
    {
        if (holdCrate)
        {
            dragSpeed = 0.4f;
            foreach (GameObject crate in AllCrate)
            {
                crate.GetComponent<Rigidbody2D>().constraints = RigidbodyConstraints2D.FreezeRotation;
            }
        }
        else
        {
            dragSpeed = 1;
            foreach (GameObject crate in AllCrate)
            {
                //Debug.Log(crate.name);
                crate.GetComponent<Crate>().DeActiveCrate();
                crate.GetComponent<Rigidbody2D>().constraints = RigidbodyConstraints2D.FreezeRotation | RigidbodyConstraints2D.FreezePositionX;
            }
        }
    }

    protected virtual void HoldCrate()
    {
        RaycastHit2D hit = Physics2D.Raycast(new Vector2(transform.position.x + 0.15f * transform.localScale.x, transform.position.y), Vector2.right * transform.localScale.x, rayFrontDistrance, LayerMask.GetMask("Crate"));

        if (hit /*&& !hit.collider.isTrigger*/)
        {
            Debug.Log("Crate before!");
            if (hit.transform.tag == "Crate")
            {
                if (Input.GetKey(KeyCode.Space))
                {
                    holdCrate = true;
                    if ((transform.localScale.x < 0 && horizontalMove < 0) || (transform.localScale.x > 0 && horizontalMove > 0))
                    {
                        //Debug.Log(hit.transform.name);
                        hit.rigidbody.AddForce(new Vector2(transform.localScale.x * 20 * Time.deltaTime, 0), ForceMode2D.Force);
                        hit.transform.gameObject.GetComponent<Crate>().ActiveCrate();
                    }
                }
                else
                {
                    holdCrate = false;
                }
            }
            else
            {
                holdCrate = false;
            }
        }
        else
        {

            holdCrate = false;
        }

        Debug.DrawRay(new Vector2(transform.position.x + (0.15f * transform.localScale.x), transform.position.y), Vector2.right * rayFrontDistrance, Color.green);

    }

    public virtual void SetDealth()
    {

    }

    #endregion
}