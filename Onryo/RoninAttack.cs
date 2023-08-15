using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RoninAttack : MonoBehaviour
{
    private int layerMask;
    private int layerMask2;

    private Animator myAnim;
    private Rigidbody2D myRigidBody;
    private AudioSource myAudioSource;
    [SerializeField] private AudioClip[] soundFX;

    [SerializeField] [Tooltip("How long the attack lasts to freeze movement")] private float attackTime = 0.5f;    // How long the attack lasts to freeze movement
    private float attackTimer = 0.0f;
    private bool isAttacking;

    private Enemy myEnemy;
    private EnemyPatroller myEnemyPatroller;

    private float originalMoveSpeed;
    [SerializeField] private float attackMoveSpeed = 2.0f;

    [Tooltip("+ or -")]
    [SerializeField] private float delayWhenDeflecting = 0.1f;
    // Start is called before the first frame update
    void Start()
    {
        layerMask = LayerMask.GetMask("Player");
        layerMask2 = LayerMask.GetMask("Weapons");
        myRigidBody = GetComponent<Rigidbody2D>();
        myAnim = GetComponentInChildren<Animator>();
        myAudioSource = GetComponent<AudioSource>();

        myEnemy = GetComponent<Enemy>();
        myEnemyPatroller = GetComponent<EnemyPatroller>();

        originalMoveSpeed = myEnemyPatroller.moveSpeed;
    }

    // Update is called once per frame
    void Update()
    {
        // Seeing raycast
        RaycastHit2D hit;
        hit = Physics2D.Raycast(transform.position, transform.right * gameObject.transform.localScale.x, 10, layerMask);
        if (hit)
            myEnemyPatroller.moveSpeed = originalMoveSpeed * attackMoveSpeed;
        else
            myEnemyPatroller.moveSpeed = originalMoveSpeed;
        Debug.DrawRay(transform.position, (transform.right * 10) * gameObject.transform.localScale.x, Color.red);


        // Sword range raycast
        RaycastHit2D hit2;
        hit2 = Physics2D.Raycast(transform.position + new Vector3(0, 0.3f, 0), transform.right * gameObject.transform.localScale.x, 2.5f, layerMask);
        if (hit2)
            RoninAttacking();

        Debug.DrawRay(transform.position + new Vector3(0, 0.3f, 0), (transform.right * 2.5f) * gameObject.transform.localScale.x, Color.blue);


        // Look for projectiles
        RaycastHit2D hit3;
        hit3 = Physics2D.Raycast(transform.position, transform.right * gameObject.transform.localScale.x, 2.7f, layerMask2);
        if (hit3)
        {
            if(hit3.collider.gameObject.GetComponent<Projectile>() ||
                hit3.collider.gameObject.GetComponent<PlayerProjectile>())
            {                
                float rand = Random.Range(0, delayWhenDeflecting);
                // Attack to know the projectile out of the air
                Invoke("RoninAttacking", rand);
            }
        }
        Debug.DrawRay(transform.position + new Vector3(0, 0.35f, 0), (transform.right * 2.7f) * gameObject.transform.localScale.x, Color.green);


        // Entity frozen during attack animation
        if (isAttacking)
        {
            attackTimer += Time.deltaTime;
            if (attackTimer > attackTime)
            {
                myEnemyPatroller.SetIsAttacking(false);
                isAttacking = false;
                attackTimer = 0;
            }
        }
    }

    /// <summary>
    /// Proceed towards player then attack
    /// </summary>

    private void RoninAttacking()
    {
        if (!myEnemy.GetIsOnGround() || isAttacking)
            return;
        // Throw star using animation
        myAnim.SetTrigger("attack");
        // Stop player from moving for 0.5s to initiate the attack
        isAttacking = true;
        myEnemyPatroller.SetIsAttacking(true);
        // Stop velocity
        myRigidBody.velocity = Vector2.zero;
        myAudioSource.clip = soundFX[0];
        myAudioSource.Play();
    }
}
