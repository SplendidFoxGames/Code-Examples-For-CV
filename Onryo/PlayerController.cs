using Cinemachine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    public enum PossessedForm { GHOST, KUNOICHI, RONIN, ONI, NAGINATA };
    [SerializeField] public PossessedForm possessedForm;
    [SerializeField] private GameObject[] possessedEnemies;

    [Space(10)]
    [Header("Movement config")]
    [SerializeField] float moveSpeed = 5f;
    [SerializeField] float jumpForce = 5f;
    [SerializeField] private float gravity = 0.25f;

    [Space(10)]
    [Header("Weapons config")]
    [SerializeField] GameObject starProjectile;
    [SerializeField] GameObject projectileFirePoint, projectileFirePointCrouch, projectileFirePointJump;
    [SerializeField] GameObject hammerWeapon;

    [Space(10)]
    [Header("Collider values fr crouch")]
    [SerializeField] float crouchCollider = 0.0f;
    [SerializeField] float jumpCollider = 0.0f;
    [SerializeField] float colliderOffset = 0.0f;

    [Space(10)]
    public bool isActive = true;
    private Vector2 moveDirection;
    private Vector2 rawInput;
    private bool isJumping;
    private bool isCrouching;
    private bool isPushingDown; // To gate the fire down option
    private bool isAttacking;
    [SerializeField] [Tooltip("How long the attack lasts to freeze movement")] private float attackTime = 0.5f;    // How long the attack lasts to freeze movement
    private float attackTimer = 0.0f;

    private Rigidbody2D myRigidBody;
    private BoxCollider2D myCollider;
    private float myColliderOriginalY;

    public Transform groundPoint;
    private bool isOnGround;
    private bool isOnOneWayPlatform;
    public LayerMask whatIsGround;
    public LayerMask whatIsOneWayPlatform;

    private bool isCollidingWithEnemy = false;
    public LayerMask whatIsEnemy;
    private bool canMove = true;
    private const string platformLayer = "Platform";

    [SerializeField] private float possessionTime = 0.5f;
    private float possessionTimer = 0.0f;
    private bool isPossessing = false;
    private Enemy enemyToPossess;
    private Animator myAnim;
    [SerializeField] private bool tutorial = false;

    private AudioSource myAudioSource;
    [SerializeField] private AudioClip[] soundFX;

    private bool gameIsPaused = false;
    private float oneWayPlatformResetTime = 0.5f;

    private void OnEnable() => FindObjectOfType<UIEnemyLocator>()?.AddThePlayer(gameObject);
    //private void OnDisable() => FindObjectOfType<UIEnemyLocator>().RemoveThePlayer();

    void Awake()
    {
        myRigidBody = GetComponent<Rigidbody2D>();
        myAnim = GetComponentInChildren<Animator>();
        myCollider = GetComponent<BoxCollider2D>();
        myAudioSource = GetComponent<AudioSource>();

        // Only used to shrink collider when crouching, and not needed for ghost
        if (possessedForm != PossessedForm.GHOST)
            myColliderOriginalY = myCollider.bounds.size.y;

    }
    private void Update()
    {
        // Checking if on ground
        isOnGround = Physics2D.OverlapCircle(groundPoint.position, 0.4f, whatIsGround);
        isOnOneWayPlatform = Physics2D.OverlapCircle(groundPoint.position, 0.4f, whatIsOneWayPlatform);

        myAnim.SetBool("isOnGround", isOnGround);
        myAnim.SetFloat("yVel", myRigidBody.velocity.y);

        if(isPossessing)
        {
            possessionTimer += Time.deltaTime;
            if (possessionTimer > possessionTime)
            {
                CompletePosession(enemyToPossess);
            }
        }
        // Entity frozen during attack animation
        if (isAttacking)
        {
            attackTimer += Time.deltaTime;
            if (attackTimer > attackTime)
            {
                isAttacking = false;
                attackTimer = 0;
            }
        }

        if (possessedForm == PossessedForm.GHOST)
            return;

        // Set collider box based on position
        if(isCrouching || !isOnGround)
        {
            if(isCrouching)
            {
                myCollider.size = new Vector2(myCollider.size.x, crouchCollider);
                myCollider.offset = new Vector2(0, -colliderOffset);
            }
            else  // Only Kunoichi is small when jumping as she somersaults
            {
                myCollider.size = new Vector2(myCollider.size.x, jumpCollider);
                myCollider.offset = new Vector2(0, 0f);
            }            
        }
        else
        {
            myCollider.size = new Vector2(myCollider.size.x, myColliderOriginalY);
            myCollider.offset = new Vector2(0, 0);
        }
    }

    private void OnDrawGizmos()
    {
        //Gizmos.color = Color.red;
        //Gizmos.DrawSphere(transform.position, 1.0f);
    }

    void FixedUpdate()
    {
        if (!isAttacking && canMove && Time.timeScale != 0)
        {
            //Move the player
            ProcessPlayerMovement();
            //Make the player jump
            if (isJumping)
            {
                myRigidBody.velocity += new Vector2(0f, jumpForce);
                isJumping = false;
            }        
            ProcessDirectionChange();
        }  
        
        myRigidBody.velocity = new Vector2(myRigidBody.velocity.x,myRigidBody.velocity.y - gravity);
    }

    //Used by the input system 
    void OnMove(InputValue value)
    {
        if (!isActive) { return; }
        rawInput = value.Get<Vector2>();

        // Ghost can floay on Y, nothing else can
        if(isOnGround && possessedForm == PossessedForm.KUNOICHI)
        {
            isPushingDown = false;
            if (rawInput.y < -0.7f)
            {
                isCrouching = true;
                myAnim.SetBool("isCrouching", isCrouching);
                rawInput.x = 0;
            }
            else
            {
                isCrouching = false;
                myAnim.SetBool("isCrouching", isCrouching);
            }
        }
        else if(!isOnGround && possessedForm == PossessedForm.KUNOICHI) // If pushing down and not on the ground, can fire down
        {
            if (rawInput.y < -0.7f)
                isPushingDown = true;  
            else
                isPushingDown = false;
        }           
    }

    /// <summary>
    /// Jump
    /// </summary>
    /// <param name="value"></param>
    void OnEast(InputValue value)
    {
        switch (possessedForm)
        {
            case PossessedForm.GHOST:
                Possess();
                break;
            case PossessedForm.KUNOICHI:
            case PossessedForm.RONIN:
            case PossessedForm.ONI:                
                PlayerJump();
                break;
            default:
                break;
        }
    }

    /// <summary>
    /// Kill host
    /// </summary>
    /// <param name="value"></param>
    void OnWest(InputValue value)
    {
        switch (possessedForm)
        {
            case PossessedForm.GHOST:
                // Possess
                Possess();
                break;
            default:
                // Kill
                myAudioSource.clip = soundFX[2];
                myAudioSource.Play();
                GetComponent<PlayerHealthController>().DestroyPlayer();
                break;
        }
    }

    /// <summary>
    /// Attack
    /// </summary>
    /// <param name="value"></param>
    void OnSouth(InputValue value)
    {
        switch (possessedForm)
        {
            case PossessedForm.GHOST:
                // Possess
                Possess();
                break;
            case PossessedForm.KUNOICHI:
                KunoichiAttack();
                break;
            case PossessedForm.RONIN:
                // Slice sword
                RoninAttack();
                break;
            case PossessedForm.ONI:
                // Swing hammer
                OniAttack();
                break;
        }
    }

    void OnPause(InputValue value)
    {
        print("Paused");

        if(!gameIsPaused)
        {
            FindObjectOfType<UIPausePanel>().PauseGame();
            gameIsPaused = true;
            Time.timeScale = 0;
        }
        else
        {
            FindObjectOfType<UIPausePanel>().UnPauseGame();
            gameIsPaused = false;
            Time.timeScale = 1;
        }
    }

    private void ProcessPlayerMovement()
    {
        switch (possessedForm)
        {
            case PossessedForm.GHOST:
                //myRigidBody.velocity = new Vector2(rawInput.x * moveSpeed, rawInput.y * moveSpeed);
                myRigidBody.AddForce(new Vector2(rawInput.x * moveSpeed, rawInput.y * moveSpeed));
                break;
            default:
                myRigidBody.velocity = new Vector2(rawInput.x * moveSpeed, myRigidBody.velocity.y);
                break;
        }
        myAnim.SetFloat("speed", Mathf.Abs(myRigidBody.velocity.x));
    }

    private void ProcessDirectionChange()
    {
        // Direction change
        if (myRigidBody.velocity.x < 0)
            gameObject.transform.localScale = new Vector3(-1f, 1f, 1f);
        else if (myRigidBody.velocity.x > 0)
            gameObject.transform.localScale = Vector3.one;
    }


    /// <summary>
    /// GHOST FUNCTIONS
    /// </summary>
    
    private void Possess()
    {
        isCollidingWithEnemy = Physics2D.OverlapCircle(transform.position, 1.0f, whatIsEnemy);
        if (isCollidingWithEnemy)
        {
            Enemy enemy = Physics2D.OverlapCircle(transform.position, 1.0f, whatIsEnemy).GetComponent<Enemy>();
            BeginPossession(enemy);
            print("Time to possess");
        }
    }
    private void BeginPossession(Enemy enemyToPossess)
    {
        this.enemyToPossess = enemyToPossess;
        // Set player to possessing animation
        myAnim.SetTrigger("isPossessing");
        GetComponentInChildren<Oscillate>().enabled = false;
        StopMovement();
        // Set enemy to possessing animation
        enemyToPossess.BeingPossessed();
        isPossessing = true;
        myAudioSource.clip = soundFX[1];
        AudioSource.PlayClipAtPoint(myAudioSource.clip, Camera.main.transform.position);
        // Destroy player input. If two inputs are active on 2 entities it can stop the controls working
        // There might be a better way around this, but find it later
        Destroy(GetComponent(typeof(PlayerInput)));
    }
    private void CompletePosession(Enemy enemyToPossess)
    {
        // Find the correct possessed enemy in the array and spawn it
        GameObject possessedEnemyThing = Instantiate(possessedEnemies[(int)enemyToPossess.enemyType], enemyToPossess.transform.position, Quaternion.identity);
          
        // Swap profile image
        FindObjectOfType<UIPlayerProfile>().SwapProfileImage((int)enemyToPossess.enemyType);
        // Set the health and health colour on UI
        FindObjectOfType<UIPlayerHealth>().SetPlayerHealth(
            enemyToPossess.GetComponent<EnemyHealthController>().GetHelth(),
            (int)enemyToPossess.enemyType);

        // Set the health to the created player
        possessedEnemyThing.GetComponent<PlayerHealthController>().SetCurrentHealth(enemyToPossess.GetComponent<EnemyHealthController>().GetHelth());
        // Kill the original enmy
        Destroy(enemyToPossess.gameObject);

        //Reduce candles on HUD
        if (!tutorial)
            Enemy.DeathSetCandles();


        // Find the Cinemachine camera and set the possessed player as follow
        CinemachineVirtualCamera virtualCam;
        virtualCam = FindObjectOfType<CinemachineVirtualCamera>();
        virtualCam.Follow = possessedEnemyThing.transform;

        // Destroy this
        Destroy(this.gameObject);
    }



    /// <summary>
    /// KUNOICHI FUNCTIONS
    /// </summary>
    /// 
    private void KunoichiAttack()
    {
        if (isAttacking)
            return;
        // Throw star using animation
        if(isOnGround)
            myAnim.SetTrigger("attack");
        // Stop player from moving for 0.5s to initiate the attack
        isAttacking = true;
        if (isOnGround)
        {
            // Stop velocity
            myRigidBody.velocity = Vector2.zero;
            myAnim.SetFloat("speed", Mathf.Abs(myRigidBody.velocity.x));
        }


        // Instantiate projectile
        GameObject tempPosition = null;

        if(isCrouching)
            tempPosition = projectileFirePointCrouch;
        else
            tempPosition = projectileFirePoint;

        if (isPushingDown)
        {
            Instantiate(starProjectile, projectileFirePointJump.transform.position, starProjectile.transform.rotation).
                 GetComponent<PlayerProjectile>().SetMoveDirection(new Vector2(0, -gameObject.transform.localScale.y));
        }
        else
        {
            Instantiate(starProjectile, tempPosition.transform.position, starProjectile.transform.rotation).
                 GetComponent<PlayerProjectile>().SetMoveDirection(new Vector2(gameObject.transform.localScale.x, 0));
        }
        myAudioSource.clip = soundFX[0];
        myAudioSource.Play();
    }

    /// <summary>
    /// RONIN FUNCTIONS
    /// </summary>
    /// 
    private void RoninAttack()
    {
        if (!isOnGround || isAttacking)
            return;
        // Throw star using animation
        myAnim.SetTrigger("attack");
        // Stop player from moving for 0.5s to initiate the attack
        isAttacking = true;
        // Stop velocity
        myRigidBody.velocity = Vector2.zero;

        myAudioSource.clip = soundFX[0];
        myAudioSource.Play();
    }

    /// <summary>
    /// ONI FUNCTIONS
    /// </summary>
    /// 
    private void OniAttack()
    {
        if (!isOnGround || isAttacking)
            return;
        // Throw star using animation
        myAnim.SetTrigger("attack");
        // Stop player from moving for 0.5s to initiate the attack
        isAttacking = true;
        // Stop velocity
        myRigidBody.velocity = Vector2.zero;

        myAudioSource.clip = soundFX[0];
        myAudioSource.Play();
    }


    private void StopMovement()
    {
        canMove = false;
        myRigidBody.velocity = Vector2.zero;
    }

    private void PlayerJump()
    {
        if (isCrouching && isOnOneWayPlatform) 
        {
            DropOneWayPlatform();
            return; 
        } 

        if (!isActive) { return; }
        if (!isOnGround) { return; }
        myAudioSource.clip = soundFX[1];
        myAudioSource.Play();
        isJumping = true;
    }

    private void DropOneWayPlatform()
    {
        gameObject.layer = LayerMask.NameToLayer("Player Platform Fall");
        Invoke("ResetPlayerLayer", oneWayPlatformResetTime);
    }

    private void ResetPlayerLayer() => gameObject.layer = LayerMask.NameToLayer("Player");
    
}
