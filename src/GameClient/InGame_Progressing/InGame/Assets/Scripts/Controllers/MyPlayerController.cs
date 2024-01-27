using Google.Protobuf.Protocol;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;
using static Define;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;



//플레이어가 goal 하면 stagenum을 검사한다. 만약 최종 스테이지가 아니면 don't destory on load. 최종스테이지면 destroy
//goal 못하면 바로 destory. don't destory on load 한건 다음 스테이지에서 스폰위치에 소환


/*
 * 게임 사용자가 조작하는 플레이어의 스크립트, PlayerController로부터 상속받는다.
 * 플레이어의 조작에 따라 상태가 바뀌며, 이를 서버에 패킷으로 전송한다.
 * 
 * 
 * */
public class MyPlayerController : PlayerController
{

    GameManager gamemanager;
    CameraController cameracontroller;
    bool inMenu = false;


    GameObject[] enemys;
    protected override void Init()
    {
        base.Init();
  
        gamemanager = GameObject.Find("GameManager").GetComponent<GameManager>();
        cameracontroller = GameObject.Find("Virtual Camera").GetComponent<CameraController>();

    }
    protected override void UpdateController()
    {
        switch (State)
        {
            case BirdState.Idle:
                GetInput();
                break;
            case BirdState.Moving:
                GetInput();
                break;
            case BirdState.Jumping:
                GetInput();
                break;
        }
        base.UpdateController();
    }
    void GetInput()
    {

        if ( !inGoal)
        {
            //바닥에 떨어지면 스폰 포인트로 강제 이동
            if (transform.position.y < -1)
            {
                Debug.Log("Fail Spawn: " + spawnPoint);
                transform.position = spawnPoint;
                transform.rotation = Quaternion.Euler(0, 180f, 0f);
            }
            if (State == BirdState.Jumping && isJumping == false)
            {
                State = BirdState.Idle;
            }


            float h = 0.0f;
            float v = 0.0f;


            h = Input.GetAxis("Horizontal");
            v = Input.GetAxis("Vertical");


            pressedJump = Input.GetKeyDown(KeyCode.Space);
            moveVec = new Vector3(h, 0f, v).normalized;


            if (pressedJump && !isJumping)
            {
                State = BirdState.Jumping;
            }

            if (isJumping && State == BirdState.Jumping)
            {
                bool SlideBtn = Input.GetMouseButtonDown(1);

                if (SlideBtn)
                    isSliding = true;
                else
                    isSliding = false;

            }

            if (Input.GetMouseButton(0))
            {

            }

            //esc를 누르면 menupanel이 활성화되고 나의 키보드 시스템은 정지된다.
            //계속하기를 누르면 menupaneel이 비활성화되고, 나의 키보드 시스템은 다시 시작된다.
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                inMenu = true;
                gamemanager.ActiveMenu();
            }
        }

        else if ( inGoal)
        {

            //Map 내의 Enemy 태그를 가진 플레이어를 follow시킨다. 카메라를

            enemys = GameObject.FindGameObjectsWithTag("Enemy");

            if (enemys.Length != 0)
            {
                cameracontroller.SetFollowTarget(enemys[0]);


            }
        }

        //  Debug.Log("State : " + State + " isJumping: " + isJumping + " moveVec: " + moveVec + " pressedJump: " + pressedJump + "isSliding" + isSliding) ; 

    }

    //Idle로 계속 남을지, 다른 상태로 넘어갈지를 판단.
    protected override void UpdateIdle()
    {


        if (moveVec.x != 0 || moveVec.z != 0)
        {
            State = BirdState.Moving;
            return;
        }


    }

    //플레이어가 먼저 이동하고 좌표를 보냄, 플레이어의 지상에서의 움직임을 제어한다.
    protected override void UpdateMoving()
    {



        prevVec = transform.position;


        Vector3 movementDirection = Quaternion.AngleAxis(cam.transform.eulerAngles.y, Vector3.up) * moveVec;

        movementDirection.Normalize();

        transform.rotation = Quaternion.Euler(0f, cam.transform.eulerAngles.y, 0f);
        transform.position += movementDirection * speed * Time.deltaTime;

        UpdateAnimation();

        if (prevVec == transform.position)
        {

            State = BirdState.Idle;
            UpdateAnimation();

        }
        else if (prevVec != transform.position)
        {

            Move playerMove = new Move()
            {
                Id = playerId,
                Position = new Vector { X = transform.position.x, Y = transform.position.y, Z = transform.position.z },
                Rotation = new Vector { X = transform.eulerAngles.x, Y = transform.eulerAngles.y, Z = transform.eulerAngles.z },
                State = PlayerState.Move,

            };

            Managers.Network.Send(playerMove, INGAME.PlayerMove);

        }



    }


    protected override void UpdateJumping()
    {

        prevVec = transform.position;

        Vector3 movementDirection = Quaternion.AngleAxis(cam.transform.eulerAngles.y, Vector3.up) * moveVec;

        movementDirection.Normalize();

        transform.rotation = Quaternion.Euler(0f, cam.transform.eulerAngles.y, 0f);

        //바닥에 착지해있는 상태라면 점프 수행
        if (!isJumping && State == BirdState.Jumping)
        {
            Jump();
            isJumping = true;
        }

        //아직까지 공중에 떠있다면 계속해서 패킷 전송
        if (isJumping && State == BirdState.Jumping)
        {
            UpdateAnimation();

            if (isSliding)
            {
                animator.SetBool("isSlide", true);
                if (!audioSource.isPlaying)
                {
                    audioSource.clip = slidClip;
                    audioSource.Play();
                }
                Slide();

            }

            transform.position += movementDirection * speed * Time.deltaTime;
        }
    }


    //점프 상태라는 패킷을 보낸다.
    void Jump()
    {
        if (!isJumping && State == BirdState.Jumping)
        {
            rigid.AddForce(Vector3.up * jumpPower, ForceMode.Impulse);
            animator.SetTrigger("doJump");
            if (!audioSource.isPlaying)
            {
                audioSource.clip = jumpClip;
                audioSource.Play();

            }

            Move playerMove = new Move()
            {
                Id = playerId,
                Position = new Vector { X = transform.position.x, Y = transform.position.y, Z = transform.position.z },
                Rotation = new Vector { X = transform.eulerAngles.x, Y = transform.eulerAngles.y, Z = transform.eulerAngles.z },
                State = PlayerState.Jump,
            };

            Managers.Network.Send(playerMove, INGAME.PlayerMove);

        }
        else
            return;
    }

    void Slide()
    {
        if (!isJumping && State == BirdState.Sliding)
        {
            animator.SetBool("isSlide", true);
            Move playerMove = new Move()
            {
                Id = playerId,
                Position = new Vector { X = transform.position.x, Y = transform.position.y, Z = transform.position.z },
                Rotation = new Vector { X = transform.eulerAngles.x, Y = transform.eulerAngles.y, Z = transform.eulerAngles.z },
                State = PlayerState.Slide,
            };

            Managers.Network.Send(playerMove, INGAME.PlayerMove);

        }
    }

    void UpdateAnimation()
    {
        switch (State)
        {
            case BirdState.Idle:
                animator.SetBool("MoveForward", false);
                animator.SetBool("inAir", false);
                animator.SetBool("isSlide", false);
                break;
            case BirdState.Moving:
                animator.SetBool("MoveForward", true);
                animator.SetBool("inAir", false);
                animator.SetBool("isSlide", false);
                break;
            case BirdState.Jumping:
                animator.SetBool("MoveForward", false);
                animator.SetBool("inAir", true);
                break;


        }
    }

    //Goal 하면 invisible, 카메라만 움직일 수 있게 만든다.
    //게임 시간 초과 or 모든 인원이 결승선을 통과해 게임이 종료되면 통과한 플레이어를 다음 Scene으로 옮긴다. 
    //다음 Scene으로 옮겨진 Player들은 Random 한 출반선안의 Random Position에 스폰된다. 

    protected override void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Victory Ground") && !inGoal)
        {
            State = BirdState.Idle;
            isJumping = false;
            isSliding = false;
         

            UpdateAnimation();

            PlayerGoalData pkt = new PlayerGoalData
            {
                Id = playerId,
                Success = true,
            };
            Managers.Network.Send(pkt, INGAME.PlayerGoal);

     

            this.transform.GetChild(0).gameObject.SetActive(false);
            this.GetComponent<CapsuleCollider>().enabled = false;

            Debug.Log("MyPlayer Goal! " + playerId);


            inGoal = true;

        }

        if (collision.gameObject.CompareTag("Ground"))
        {
            if (isJumping)
            {
                //Debug.Log("collisionGround");
                State = BirdState.Idle;
                isJumping = false;
                isSliding = false;

                UpdateAnimation();


            }
        }
    }

    protected override void OnCollisionStay(Collision collision)
    {

    }







}

