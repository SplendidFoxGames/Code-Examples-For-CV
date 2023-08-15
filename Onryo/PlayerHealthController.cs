using Cinemachine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerHealthController : MonoBehaviour
{
    public int totalHealth = 3;
    private int currentHealth;
    public float invulnerableTime = 0.3f;
    private float invulnerableTimer = 0.0f;
    private bool isInvulnerable;
    private Animator myAnimator;

    [SerializeField] private AudioClip[] myClips;
    private AudioSource myAudioSource;

    private UIPlayerHealth myHealth;
    private PlayerController myPlayerController;
    [SerializeField] private GameObject orbToRespawn;

    private float orbHealthRegenTime = 3.0f;
    private float orbHealthRegenTimer = 0.0f;

    // Start is called before the first frame update
    void Start()
    {
        currentHealth = totalHealth;
        myAnimator = GetComponentInChildren<Animator>();
        myAudioSource = GetComponent<AudioSource>();

        myHealth = FindObjectOfType<UIPlayerHealth>();
        myPlayerController = GetComponent<PlayerController>();
    }

    // Update is called once per frame
    void Update()
    {
        invulnerableTimer -= Time.deltaTime;
        if (isInvulnerable && invulnerableTimer < 0)
        {
            isInvulnerable = false;
            gameObject.layer = LayerMask.NameToLayer("Player");
        }
        // Regenerate orb health while possessing
        orbHealthRegenTimer += Time.deltaTime;
        if (orbHealthRegenTimer >= orbHealthRegenTime)
        {
            myHealth.IncreaseOrbHealth(1);
            orbHealthRegenTimer = 0.0f;
        }
    }

    public void DamagePlayer(int damageAmount)
    {
        if (isInvulnerable)
            return;

        currentHealth -= damageAmount;
        myHealth.SetPlayerHealth(currentHealth,
            (int)myPlayerController.possessedForm-1); // -1 because the Possessed Forms start with GHOST form, but enemy enum forms dont.
        if (currentHealth <= 0)
        {
            myAnimator.SetTrigger("death");
            gameObject.layer = LayerMask.NameToLayer("Dead");
            Invoke("DestroyPlayer", 0.5f);
            DestroyPlayer();
            myAudioSource.PlayOneShot(myClips[1]);
        }
        else
        {
            // flicker
            myAnimator.SetTrigger("damaged");
            isInvulnerable = true;
            invulnerableTimer = invulnerableTime;
            gameObject.layer = LayerMask.NameToLayer("Invulnerable");

            myAudioSource.PlayOneShot(myClips[0]);
        }
    }

    public void SetCurrentHealth(int value) => currentHealth = value;

    // When player dies, the orb comes out
    public void DestroyPlayer()
    {
        myAnimator.SetTrigger("death");
        gameObject.layer = LayerMask.NameToLayer("Dead");

        // Destroy player input. If two inputs are active on 2 entities it can stop the controls working
        // There might be a better way around this, but find it later
        Destroy(GetComponent(typeof(PlayerInput)));

        Invoke("Kill", 0.5f);
    }

    private void Kill()
    {
        // Swap profile image
        FindObjectOfType<UIPlayerProfile>().SwapProfileImage(4);
        // Turn off possessed player health
        // Set the health and health colour on UI
        FindObjectOfType<UIPlayerHealth>().TurnOffPlayerHealth();

        GameObject newOrbThing = Instantiate(orbToRespawn, transform.position, orbToRespawn.gameObject.transform.rotation);

        // Find the Cinemachine camera and set the orb as follow
        CinemachineVirtualCamera virtualCam;
        virtualCam = FindObjectOfType<CinemachineVirtualCamera>();
        virtualCam.Follow = newOrbThing.transform;

        Destroy(gameObject);
    }
}
