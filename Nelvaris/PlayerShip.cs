using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.InputSystem;
using static UnityEngine.Rendering.DebugUI;

//public class PlayerShip : MonoBehaviour {
public class PlayerShip : AllShipBase
{
    public Volume ppVolume;

    private bool invertY = false;
    //private bool isPaused = false;
    private bool exitingLevel = false;      // When player complets objectives and exits level

    //[Tooltip("This is when controller is idle and ship slows down, or when throttle is locked to 0")]
    [SerializeField] private float boostFactor = 40f;        // The speed the ship increases when boosting
    [SerializeField] private float maxBoostSpeed = 120f;    // This should be 50% quicker than the standard max speed

    private bool isBoosting = false;
    private bool boostDepleated = false;

    private float boostShakeAmount = 7.0f;        // How much the ship shakes when boosting
    private float boostShakeCounter = 1.0f;
    private float boostShakeDecreaseFactor = 1.1f;
    private bool boostShaking = false;

    // These values = the input value multiplied by shipRotationSpeed, or shipStrafeSpeed
    private float yawSpeed = 0.0f;       // Left/Right
    private float pitchSpeed = 0.0f;     // Up/Down
    private float rollSpeed = 0.0f;     // Tilt up/down
    private Vector2 strafeDir = Vector2.zero;

    private bool disembarking = false;
    private float totalEnginePower = 400;    // Default is 400 Can be increased to 1200 by depleating other 2 subsystems


    private float maxBoostPower = 100;
    private float currentBoostPower;
    [SerializeField] private float reduceBoostAmmount = 1.5f;

    //Define a constant factor for reducing the initial boost speed
    const float InitialBoostFactor = 0.15f;
    //Define a constant threshold for the initial boost speed
    float InitialBoostThreshold = 0.0f;
    //Define a constant factor for reducing the boost speed
    const float BoostFactor = 0.54f; // Increase this to increase the rate that the ship gets to its top boost speed

    private PlayerTargeting playerTargeting;
    private PlayerWeaponSystem weaponSystem;
    private MissileFire missileFire;
    private PlayerControlPower controlPower;
    private Shield shields;
    private PlayerWarpDrive warpDrive;
    private PauseManager pauseManager;

    private AudioSource[] audioSource;

    private Vector3 originalCameraPosition;                                      // Default for camera should be 0,0,0,
    private Vector3 defaultCameraPosition = new Vector3(0, 0, 4);                // In front of the ship facing forwards (default for play)
    private Vector3 behindCameraPosition = new Vector3(0, 9, -29);               // Behind in 3rd person over should view
    private Vector3 inFrontFacingCameraPosition;                                // In front of the ship but facing it

    private UIMissiles uiMissiles;
    private UITargetIndicators uiTargetIndicators;

    private float throttleIncreaseTime = 0.01f;
    private float throttleIncreaseTimer = 0.01f;
    private bool increaseThrottle = false;
    private float throttleChangeAmount = 0.0f;

    public static PlayerControls inputActions;

    new void Start()
    {
        //Cursor.lockState = CursorLockMode.Locked;        
        base.Start();
        if (inputActions == null)        // Will most likely never be null
            inputActions = InputManager.inputActions;   

        currentBoostPower = maxBoostPower;
        playerTargeting = GetComponentInChildren<PlayerTargeting>();
        weaponSystem = GetComponentInChildren<PlayerWeaponSystem>();
        missileFire = GetComponentInChildren<MissileFire>();
        controlPower = GetComponent<PlayerControlPower>();
        shields = GetComponentInChildren<Shield>();
        warpDrive = GetComponent<PlayerWarpDrive>();
        pauseManager = FindObjectOfType<PauseManager>();
        audioSource = GetComponents<AudioSource>();
        audioSource[0].volume = 0f;

        uiMissiles = FindObjectOfType<UIMissiles>();
        uiMissiles?.SetUpUIMissiles(missileFire.GetMissilePools().Length, missileFire);
        uiTargetIndicators = FindObjectOfType<UITargetIndicators>();

        invertY = PersistentPlayerData.GetPlayerYInvertedControls();
        InitialBoostThreshold = 1.0f * maxSpeed;
        originalCameraPosition = Camera.main.transform.localPosition;
        Camera.main.transform.localPosition = defaultCameraPosition;

        // Movement
        inputActions.Player.IncreaseSpeed.started += DoIncreaseSpeed;
        inputActions.Player.DecreaseSpeed.started += DoDecreaseSpeed;

        inputActions.Player.ChangeSpeed.started += DoChangeSpeed;
        inputActions.Player.ChangeSpeed.canceled += DoStopChangeSpeed;

        inputActions.Player.MaxThrottle.started += DoMaxThrottle;
        inputActions.Player.ZeroThrottle.started += DoZeroThrottle;
        inputActions.Player.EngageWarpEndMission.started += DoEngageWarp;
        inputActions.Player.MatchTargetSpeed.started += DoMatchTargetSpeed;
        inputActions.Player.Boost.started += DoBoost;
        inputActions.Player.Boost.canceled += DoStopBoost;
        inputActions.Player.PitchYaw.performed += DoPitchYaw;
        inputActions.Player.PitchYaw.canceled += DoStopPitchYaw;
        inputActions.ShroudShip.IncreaseSpeed.started += DoIncreaseSpeed;
        inputActions.ShroudShip.DecreaseSpeed.started += DoDecreaseSpeed;
        inputActions.ShroudShip.MaxThrottle.started += DoMaxThrottle;
        inputActions.ShroudShip.ZeroThrottle.started += DoZeroThrottle;
        inputActions.ShroudShip.EngageWarpEndMission.started += DoEngageWarp;
        inputActions.ShroudShip.MatchTargetSpeed.started += DoMatchTargetSpeed;
        inputActions.ShroudShip.Boost.started += DoBoost;
        inputActions.ShroudShip.Boost.canceled += DoStopBoost;
        inputActions.ShroudShip.PitchYaw.started += DoPitchYaw;
        inputActions.ShroudShip.PitchYaw.canceled += DoStopPitchYaw;

        // Axial Movement
        inputActions.Player.StrafeUp.started += DoStrafeY;
        inputActions.Player.StrafeDown.started += DoStrafeY;
        inputActions.Player.StrafeLeft.started += DoStrafeX;
        inputActions.Player.StrafeRight.started += DoStrafeX;

        inputActions.Player.StrafeUp.canceled += DoStopStrafing;
        inputActions.Player.StrafeDown.canceled += DoStopStrafing;
        inputActions.Player.StrafeLeft.canceled += DoStopStrafing;
        inputActions.Player.StrafeRight.canceled += DoStopStrafing;

        inputActions.Player.BankLeft.started += DoBank;
        inputActions.Player.BankRight.started += DoBank;
        inputActions.Player.BankLeft.canceled += DoStopBanking;
        inputActions.Player.BankRight.canceled += DoStopBanking;


        inputActions.ShroudShip.StrafeUp.started += DoStrafeY;
        inputActions.ShroudShip.StrafeDown.started += DoStrafeY;
        inputActions.ShroudShip.StrafeLeft.started += DoStrafeX;
        inputActions.ShroudShip.StrafeRight.started += DoStrafeX;

        inputActions.ShroudShip.StrafeUp.canceled += DoStopStrafing;
        inputActions.ShroudShip.StrafeDown.canceled += DoStopStrafing;
        inputActions.ShroudShip.StrafeLeft.canceled += DoStopStrafing;
        inputActions.ShroudShip.StrafeRight.canceled += DoStopStrafing;

        inputActions.ShroudShip.BankLeft.started += DoBank;
        inputActions.ShroudShip.BankRight.started += DoBank;
        inputActions.ShroudShip.BankLeft.canceled += DoStopBanking;
        inputActions.ShroudShip.BankRight.canceled += DoStopBanking;


        // Combat
        inputActions.Player.FirePrimaryWeapon.performed += DoFirePrimaryWeapon;
        inputActions.Player.FirePrimaryWeapon.canceled += StopFirePrimaryWeapon;
        inputActions.Player.FireSecondaryWeapon.performed += DoFireSecondaryWeapon;
        inputActions.Player.FireSecondaryWeapon.canceled += StopFireSecondaryWeapon;
        inputActions.Player.CyclePrimaryWeaponBank.performed += DoCyclePrimaryWeaponBank;
        inputActions.Player.CycleSecondaryWeaponBank.performed += DoCycleSecondaryWeaponBank;
        inputActions.Player.CycleSecondaryWeaponFiringRate.performed += DoCycleSecondaryWeaponFiringRate;

        // Targeting
        inputActions.Player.TargetNextClosestAnyShip.started += DoTargetNextClosestAnyShip;
        inputActions.Player.TargetNextClosestEnemyShip.started += DoTargetNextClosestEnemyShip;
        inputActions.Player.TargetNextClosestFriendlyShip.started += DoTargetNextClosestFriendlyShip;
        inputActions.Player.TargetNextClosestEscortShip.started += DoTargetNextClosestFriendlyEscortShip;
        inputActions.Player.TargetShipinReticle.started += DoTargetShipInReticle;
        inputActions.Player.TargetClosestEnemyShip.started += DoTargetClosestEnemyShip;
        inputActions.Player.TargetAttackingShip.started += DoTargetAttackingShip;
        inputActions.Player.TargetNextSubSystem.started += DoTargetNextSubSystem;

        inputActions.ShroudShip.TargetNextClosestAnyShip.started += DoTargetNextClosestAnyShip;
        inputActions.ShroudShip.TargetNextClosestEnemyShip.started += DoTargetNextClosestEnemyShip;
        inputActions.ShroudShip.TargetNextClosestFriendlyShip.started += DoTargetNextClosestFriendlyShip;
        inputActions.ShroudShip.TargetNextClosestEscortShip.started += DoTargetNextClosestFriendlyEscortShip;
        inputActions.ShroudShip.TargetShipinReticle.started += DoTargetShipInReticle;
        inputActions.ShroudShip.TargetClosestEnemyShip.started += DoTargetClosestEnemyShip;
        inputActions.ShroudShip.TargetAttackingShip.started += DoTargetAttackingShip;
        inputActions.ShroudShip.TargetNextSubSystem.started += DoTargetNextSubSystem;

        // Shields
        inputActions.Player.EqualiseShields.started += DoEqualiseShields;
        inputActions.Player.AugmentBowShield.started += DoAugmentBowShield;
        inputActions.Player.AugmentAftShield.started += DoAugmentAftShield;
        inputActions.Player.AugmentPortShield.started += DoAugmentPortShield;
        inputActions.Player.AugmentStarboardShield.started += DoAugmentStarboardShield;

        // Power Management
        inputActions.Player.IncreaseWeaponPower.started += DoIncreaseWeaponPower;
        inputActions.Player.DecreaseWeaponPower.started += DoDecreaseWeaponPower;
        inputActions.Player.IncreaseShieldPower.started += DoIncreaseShieldPower;
        inputActions.Player.DecreaseShieldPower.started += DoDecreaseShieldPower;
        inputActions.Player.IncreaseEnginePower.started += DoIncreaseEnginePower;
        inputActions.Player.DecreaseEnginePower.started += DoDecreaseEnginePower;

        // Misc
        inputActions.Player.Pause.started += DoPause;
        inputActions.UI.Pause.started += DoPause;
        inputActions.ShroudShip.Pause.started += DoPause;

        inputActions.Player.Enable();
    }

    private void Update()
    {
        if (!exitingLevel /*&& !isPaused*/)
        {
            AccelerateDecelerate();
            if (!disembarking) // From a carrier
            {
                //    LockThrottle();
                Boost();
                BoostShake();
            }
        }        
    }


    // Update is called once per frame
    void FixedUpdate()
    {
        if (!exitingLevel /*&& !isPaused*/)
        {
            if (!disembarking) // From a carrier
            {
                KeyboardAndMouseControls();
            }
            MoveShip();
        }
    }

    // PC and Xbox Acceleration and Deceleration
    new private void AccelerateDecelerate()
    {
        throttleIncreaseTimer += Time.deltaTime;
        //if (increaseThrottle)
        //{            
            if (throttleIncreaseTimer > throttleIncreaseTime)
            {
                shipThrottleSpeed += throttleChangeAmount;
                shipThrottleSpeed  = Mathf.Clamp(shipThrottleSpeed, minSpeed, maxSpeed);
                throttleIncreaseTimer = 0.0f;
            }
        //}

        if (matchTargetSpeed)
            MatchTargetSpeed();
        else
            SetSpeed(shipThrottleSpeed);
        // Adjust engine sound effect to match speed
        audioSource[0].volume = shipCurrentSpeed / 100;
    }

    protected override void MoveShip()
    {
        //myRigidBody.AddForce(Camera.main.transform.forward * shipCurrentSpeed * speedMultiplier);
        myRigidBody.AddForce(gameObject.transform.forward * shipCurrentSpeed * speedMultiplier);
    }

    /// <summary>
    /// To set a speec for the ship to attain, 
    /// rather than use the mouse wheel/keys to accelerate/decelerate
    /// </summary>
    /// <param name="speedSet"></param>
    private void SetSpeed(float speedSet)
    {
        if (!matchTargetSpeed)
        {
            if (shipCurrentSpeed < speedSet)
            {
                shipCurrentSpeed += accelerateSpeed * Time.deltaTime;
                shipCurrentSpeed = Mathf.Clamp(shipCurrentSpeed, minSpeed, speedSet);
            }
            else if (shipCurrentSpeed > speedSet && !isBoosting)
            {
                shipCurrentSpeed -= decelerateSpeed * Time.deltaTime;
                shipCurrentSpeed = Mathf.Clamp(shipCurrentSpeed, speedSet, 400); // Max boost speed, make this a variable later
            }
        }
        else
        {
            if (shipCurrentSpeed < speedSet)
            {
                shipCurrentSpeed += accelerateSpeed * Time.deltaTime;
                shipCurrentSpeed = Mathf.Clamp(shipCurrentSpeed, minSpeed, maxSpeed); // Use max speed of the ship, so if enemy ship is faster, cant keep up
            }
            else if (shipCurrentSpeed > speedSet && !isBoosting)
            {
                shipCurrentSpeed -= decelerateSpeed * Time.deltaTime;
                shipCurrentSpeed = Mathf.Clamp(shipCurrentSpeed, speedSet, maxSpeed);
            }
        }
    }

    /// <summary>
    /// If target, then match speed, else set throttle to 1/3
    /// </summary>
    private new void MatchTargetSpeed()
    {
        shipThrottleSpeed = playerTargeting.GetCurrentTargetSpeed();
        SetSpeed(shipThrottleSpeed);
    }

    //******************************************************************************************************************************************************//
    //*******************************************************************  Movement  ***********************************************************************//
    //******************************************************************************************************************************************************//

    private void DoIncreaseSpeed(InputAction.CallbackContext ob)
    {
        shipThrottleSpeed += ob.ReadValue<float>();
        matchTargetSpeed = false;
    }
    private void DoDecreaseSpeed(InputAction.CallbackContext ob)
    {
        shipThrottleSpeed -= ob.ReadValue<float>();
        matchTargetSpeed = false;
    }

    private void DoChangeSpeed(InputAction.CallbackContext ob)
    {
        if (ob.ReadValue<Vector2>().y > 0)
            throttleChangeAmount = 2.0f;
        else if (ob.ReadValue<Vector2>().y < 0)
            throttleChangeAmount = -2.0f;
        increaseThrottle = true;
    }

    private void DoStopChangeSpeed(InputAction.CallbackContext ob)
    {
        increaseThrottle = false;
    }


    private void DoMaxThrottle(InputAction.CallbackContext ob) { shipThrottleSpeed = maxSpeed; matchTargetSpeed = false; }
    private void DoZeroThrottle(InputAction.CallbackContext ob) { shipThrottleSpeed = 0; matchTargetSpeed = false; }

    private void DoBank(InputAction.CallbackContext ob)
    {
        float temp = ob.ReadValue<float>();
        rollSpeed = shipRotationSpeed * ob.ReadValue<float>();
    }
    private void DoStopBanking(InputAction.CallbackContext ob)
    {
        rollSpeed = 0;
    }

    private void DoStrafeY(InputAction.CallbackContext ob)
    {
        float test = ob.ReadValue<float>();
        Vector2 direction = new Vector2(0, test);
        strafeDir += direction;
    }
    private void DoStrafeX(InputAction.CallbackContext ob)
    {
        float test = ob.ReadValue<float>();
        Vector2 direction = new Vector2(test, 0);
        strafeDir += direction;
    }

    private void DoStopStrafing(InputAction.CallbackContext ob) { strafeDir = Vector2.zero; }

    private void DoPitchYaw(InputAction.CallbackContext ob)
    {
        //Debug.Log("Pitchyaw called");
        pitchSpeed = 0;
        yawSpeed = 0;

        yawSpeed += shipRotationSpeed * ob.ReadValue<Vector2>().x;
        if (!invertY)
            pitchSpeed -= shipRotationSpeed * ob.ReadValue<Vector2>().y;
        else
            pitchSpeed += shipRotationSpeed * ob.ReadValue<Vector2>().y;
    }

    private void DoStopPitchYaw(InputAction.CallbackContext ob)
    {
        // Without this the view will always follow where the mouse is
        // This is known as "Steering Mode" in X4.
        pitchSpeed = 0;
        yawSpeed = 0;
    }


    private void DoBoost(InputAction.CallbackContext ob)
    {
        matchTargetSpeed = false;
        boostShakeCounter = 1.0f;
        boostShakeAmount = 10.0f;
        boostShaking = false;
        isBoosting = true;
        shipThrottleSpeed = maxSpeed;
   
    }

    private void DoStopBoost(InputAction.CallbackContext ob)
    {
        boostDepleated = false;
        isBoosting = false;
    }

    //private void OnZeroThrottle(InputValue value) { shipThrottleSpeed = 0; matchTargetSpeed = false; }
    private void DoMatchTargetSpeed(InputAction.CallbackContext ob)
    {
        if (playerTargeting.GetCurrentTarget() != null)
            matchTargetSpeed = true;
    }

    private void DoEngageWarp(InputAction.CallbackContext ob) { warpDrive.EngageWarp(); }

    private void KeyboardAndMouseControls()
    {
        // Ship Roll, Pitch, and Yaw
        myRigidBody.AddRelativeTorque(pitchSpeed, yawSpeed, rollSpeed);
        // Strafe - all directions
        myRigidBody.AddRelativeForce(strafeDir * shipStrafeSpeed, ForceMode.Acceleration);
    }

    //******************************************************************************************************************************************************//
    //********************************************************************  Combat  ************************************************************************//
    //******************************************************************************************************************************************************//
    private void DoFirePrimaryWeapon(InputAction.CallbackContext ob) => weaponSystem.IsFiring = ob.performed;    
    private void StopFirePrimaryWeapon(InputAction.CallbackContext ob) => weaponSystem.IsFiring = !ob.canceled;
    private void DoFireSecondaryWeapon(InputAction.CallbackContext ob) => missileFire.IsFiring = ob.performed;
    private void StopFireSecondaryWeapon(InputAction.CallbackContext ob) => missileFire.IsFiring = !ob.canceled;

    private void DoCyclePrimaryWeaponBank(InputAction.CallbackContext ob)
    {
        /// Not yet Implemented
        Debug.Log("Cycle primary weapon bank not yet implemented");
    }
    private void DoCycleSecondaryWeaponBank(InputAction.CallbackContext ob)
    {
        // Prevents this from working while RightShift is held down
        // because right shift is a modifier for cyrcling the firing rate below
       // if (!Keyboard.current.rightShiftKey.isPressed)
        //{
            int selectedBank = 90;
            // Get currently selected missile bank
            selectedBank = uiMissiles.GetCurrentlySelectedMissileBank();
            if (selectedBank == 0)
                selectedBank = 1;
            else if (selectedBank == 1)
                selectedBank = 2;
            else if (selectedBank == 2)
                selectedBank = 0;
            missileFire.SetSelectedMissileBank(selectedBank);
        //}
    }
    private void DoCycleSecondaryWeaponFiringRate(InputAction.CallbackContext ob)
    {
        uiMissiles.CycleSecondaryWeaponFiringRate();
        missileFire.CycleSecondaryWeaponFiringRate();
    }

    //******************************************************************************************************************************************************//
    //*****************************************************************  Power Management  ********************************************************************//
    //******************************************************************************************************************************************************//
    private void DoIncreaseWeaponPower(InputAction.CallbackContext ob) => controlPower.IncreaseWP();
    private void DoDecreaseWeaponPower(InputAction.CallbackContext ob) => controlPower.DecreaseWP();
    private void DoIncreaseShieldPower(InputAction.CallbackContext ob) => controlPower.IncreaseSP();
    private void DoDecreaseShieldPower(InputAction.CallbackContext ob) => controlPower.DecreaseSP();
    private void DoIncreaseEnginePower(InputAction.CallbackContext ob) => controlPower.IncreaseEP();
    private void DoDecreaseEnginePower(InputAction.CallbackContext ob) => controlPower.DecreaseEP();

    //******************************************************************************************************************************************************//
    //******************************************************************  Targeting  ***********************************************************************//
    //******************************************************************************************************************************************************//
    private void DoTargetNextClosestAnyShip(InputAction.CallbackContext ob) => playerTargeting.TargetNextClosestAnyShip(ob);
    private void DoTargetNextClosestEnemyShip(InputAction.CallbackContext ob) => playerTargeting.TargetNextClosestEnemyShip(ob);
    private void DoTargetNextClosestFriendlyShip(InputAction.CallbackContext ob) 
    {
        var value = inputActions.Player.enabled;
        playerTargeting.TargetNextClosestFriendlyShip(ob); 
    }
    private void DoTargetNextClosestFriendlyEscortShip(InputAction.CallbackContext ob) => playerTargeting.TargetNextClosestFriendlyEscortShip(ob);
    private void DoTargetShipInReticle(InputAction.CallbackContext ob) => playerTargeting.TargetShipInReticle(ob);
    private void DoTargetClosestEnemyShip(InputAction.CallbackContext ob) => playerTargeting.TargetClosestEnemyShip(ob);
    private void DoTargetAttackingShip(InputAction.CallbackContext ob) => playerTargeting.TargetAttackingShip(ob);
    private void DoTargetNextSubSystem(InputAction.CallbackContext ob) => playerTargeting.TargetNextSubSystem(ob);

    //******************************************************************************************************************************************************//
    //*******************************************************************  Shields  ************************************************************************//
    //******************************************************************************************************************************************************//
    private void DoEqualiseShields(InputAction.CallbackContext ob) => shields.EqualiseShields();
    private void DoAugmentBowShield(InputAction.CallbackContext ob) => shields.AugmentBowShield();
    private void DoAugmentAftShield(InputAction.CallbackContext ob) => shields.AugmentAftShield();
    private void DoAugmentPortShield(InputAction.CallbackContext ob) => shields.AugmentPortShield();
    private void DoAugmentStarboardShield(InputAction.CallbackContext ob) => shields.AugmentStarboardShield();

    //******************************************************************************************************************************************************//
    //*********************************************************************  Misc  *************************************************************************//
    //******************************************************************************************************************************************************//
    
    private void DoPause(InputAction.CallbackContext ob)
    {
        pauseManager.SetPauseMenu();
    }
    private void Boost()
    {
        if (!boostDepleated && isBoosting)
        {
            //Calculate the boost speed based on the current speed and the max speed
            float boostSpeed = boostFactor * Time.deltaTime;
            if (shipCurrentSpeed >= maxSpeed)
            {
                boostSpeed *= BoostFactor / (shipCurrentSpeed / maxSpeed);
            }
            else if (shipCurrentSpeed < InitialBoostThreshold)  // This prevents the ship boosting too fast before it gets to its usual max speed
            {
                boostSpeed *= InitialBoostFactor;
            }
            //Limit the boost speed to the max boost speed
            boostSpeed = Mathf.Min(boostSpeed, maxBoostSpeed - shipCurrentSpeed);
            //Increase the ship's speed by the boost speed
            shipCurrentSpeed += boostSpeed;

            audioSource[1].volume = 0.5f; // Boost sound effect. It is always playing, but its at 0f volume. Set to 1f when boosting, then it drops down to 0f when finished

            if (currentBoostPower < 1)
            {
                boostDepleated = true;
                isBoosting = false;
            }
               
            boostShaking = true;

            currentBoostPower -= (boostFactor * Time.deltaTime) / reduceBoostAmmount;          // Reduce boost power
        }
        else
        {
            if (totalEnginePower > 0)
            {
                currentBoostPower += totalEnginePower * 0.0002f;   // Regeneration
                ppVolume.weight -= Time.deltaTime;
                ppVolume.weight = Mathf.Clamp(ppVolume.weight, 0.0f, 1.0f);
            }
            currentBoostPower = Mathf.Clamp(currentBoostPower, 0f, maxBoostPower);
            audioSource[1].volume -= Time.deltaTime / 4;   // Set the boost sound back to 0
        }
    }
    private void BoostShake()
    {
        if (boostShakeCounter > 0 && boostShaking == true)
        {
            Camera.main.transform.localPosition = Random.insideUnitSphere * boostShakeAmount;
            boostShakeCounter -= Time.deltaTime * boostShakeDecreaseFactor;
            boostShakeAmount -= Time.deltaTime * (boostShakeDecreaseFactor * 10);
            ppVolume.weight += Time.deltaTime;
            ppVolume.weight = Mathf.Clamp(ppVolume.weight, 0.0f, 1.0f);
        }
        else
        {
            boostShakeCounter = 0.0f;
            if (!isBoosting)
            {
                ppVolume.weight -= Time.deltaTime;
                ppVolume.weight = Mathf.Clamp(ppVolume.weight, 0.0f, 1.0f);
                boostShakeCounter = 1.0f;
                boostShakeAmount = 10.0f;
                boostShaking = false;
            }
        }
    }
    public float GetSpeed()
    {
        return shipCurrentSpeed;
    }

    public bool GetMatchTargetSpeed()
    {
        return matchTargetSpeed;
    }

    public float GetThrottleSpeed()
    {
        return shipThrottleSpeed;
    }

    public void SetMatchSpeed(bool value)
    {
        matchTargetSpeed = value;
    }

    new public void SetThrottleSpeed(float s)
    {
        shipCurrentSpeed = s;
        SetSpeed(s);
    }

    public float GetBoostPower()
    {
        return currentBoostPower;
    }

    public void IncreaseTotalEnginePower(float power)
    {
        totalEnginePower += power;
        totalEnginePower = Mathf.Clamp(totalEnginePower, 0, 1200);
        SetSpeedBasedOnEnginePower();
    }

    // A request to take x power from subsystem.
    // Subsystem will return less if it has less than that requested
    public float DecreaseTotalEnginePower(float power)
    {
        if (totalEnginePower > power)
        {
            // return power to say it can be decrease
            totalEnginePower -= power;
            totalEnginePower = Mathf.Clamp(totalEnginePower, 0, 1200);
            SetSpeedBasedOnEnginePower();
            return power;
        }
        else
        {
            // return whats left of shield strength
            float temp = totalEnginePower;
            totalEnginePower -= power;
            totalEnginePower = Mathf.Clamp(totalEnginePower, 0, 1200);
            SetSpeedBasedOnEnginePower();
            return temp;
        }
    }

    private void SetSpeedBasedOnEnginePower()
    {
        if (totalEnginePower == 1200)
            maxSpeed = totalEnginePower / 12f;
        if (totalEnginePower == 1000)
            maxSpeed = totalEnginePower / 11f;
        else if (totalEnginePower == 800)
            maxSpeed = totalEnginePower / 9f;
        else if (totalEnginePower == 600)
            maxSpeed = totalEnginePower / 7f;
        else if (totalEnginePower == 400)
            maxSpeed = totalEnginePower / 5f;
        else if (totalEnginePower == 200)
            maxSpeed = totalEnginePower / 4.5f;
        else if (totalEnginePower == 0)
            maxSpeed = 35f;
    }

    public float ReturnTotalEnginePower()
    {
        return totalEnginePower;
    }

    public void ExitingLevelStopControls()
    {
        exitingLevel = true;
    }
    public void Disembark()
    {
        disembarking = true;
        Collider[] theColliders;
        MeshCollider[] shieldColliders;
        // Turn off HUD and controls
        FindObjectOfType<HUD>().GetComponent<Canvas>().enabled = false;
        // Turn off space dust
        GetComponentInChildren<SpaceDust>().enabled = false;
        // Turn off weapons
        GetComponentInChildren<PlayerWeaponSystem>().enabled = false;
        // Turn off missiles
        GetComponentInChildren<PlayerWeaponSystem>().GetComponentInChildren<MissileFire>().enabled = false;

        theColliders = GetComponents<Collider>();
        
        foreach (Collider c in theColliders)
        {
            c.enabled = false;
        }
        theColliders = GetComponentsInChildren<Collider>();
        foreach (Collider c in theColliders)
        {
            c.enabled = false;
        }

        shieldColliders = GetComponentsInChildren<MeshCollider>();
        foreach (Collider c in shieldColliders)
        {
            c.enabled = false;
        }

        //throttleEnumSpeed = Throttle.Max;
        shipThrottleSpeed = maxSpeed;
        shipCurrentSpeed = shipThrottleSpeed;
        Invoke("Disembarked", 5.3f);
    }

    private void Disembarked()
    {
        disembarking = false;
        Collider[] theColliders;
        MeshCollider[] shieldColliders;
        // Turn on HUD and controls
        FindObjectOfType<HUD>().GetComponent<Canvas>().enabled = true;
        // Turn on space dust
        GetComponentInChildren<SpaceDust>().enabled = true;
        // Turn on weapons
        GetComponentInChildren<PlayerWeaponSystem>().enabled = true;
        // Turn on missiles
        GetComponentInChildren<PlayerWeaponSystem>().GetComponentInChildren<MissileFire>().enabled = true;

        theColliders = GetComponents<Collider>();
        foreach (Collider c in theColliders)
        {
            c.enabled = true;
        }
        theColliders = GetComponentsInChildren<Collider>();
        foreach (Collider c in theColliders)
        {
            c.enabled = true;
        }

        shieldColliders = GetComponentsInChildren<MeshCollider>();
        foreach (Collider c in shieldColliders)
        {
            c.enabled = true;
        }
    }

    public override SubComponent[] GetSubComponents()
    {
        return subComponents;
    }

    /// <summary>
    /// Camera controls
    /// </summary>
    private void SetBehindCamera()
    {

    }

    //The Settings Menu sets these controls after the player closes it.
    public void SetInvertYControl(bool value)
    {
        invertY = value;
    }

    private void OnEnable()
    {
        TargetingManager.AddShipToList(this);
    }

    private void OnDisable()
    {
        TargetingManager.RemoveShipFromList(this);

        //Movement
        inputActions.Player.IncreaseSpeed.started -= DoIncreaseSpeed;
        inputActions.Player.DecreaseSpeed.started -= DoDecreaseSpeed;

        inputActions.Player.ChangeSpeed.started -= DoChangeSpeed;
        inputActions.Player.ChangeSpeed.canceled -= DoStopChangeSpeed;

        inputActions.Player.MaxThrottle.started -= DoMaxThrottle;
        inputActions.Player.ZeroThrottle.started -= DoZeroThrottle;
        inputActions.Player.EngageWarpEndMission.started -= DoEngageWarp;
        inputActions.Player.MatchTargetSpeed.started -= DoMatchTargetSpeed;
        inputActions.Player.Boost.started -= DoBoost;
        inputActions.Player.Boost.canceled -= DoStopBoost;
        inputActions.Player.PitchYaw.performed -= DoPitchYaw;
        inputActions.Player.PitchYaw.canceled -= DoStopPitchYaw;
        inputActions.ShroudShip.IncreaseSpeed.started -= DoIncreaseSpeed;
        inputActions.ShroudShip.DecreaseSpeed.started -= DoDecreaseSpeed;
        inputActions.ShroudShip.MaxThrottle.started -= DoMaxThrottle;
        inputActions.ShroudShip.ZeroThrottle.started -= DoZeroThrottle;
        inputActions.ShroudShip.EngageWarpEndMission.started -= DoEngageWarp;
        inputActions.ShroudShip.MatchTargetSpeed.started -= DoMatchTargetSpeed;
        inputActions.ShroudShip.Boost.started -= DoBoost;
        inputActions.ShroudShip.Boost.canceled -= DoStopBoost;
        inputActions.ShroudShip.PitchYaw.started -= DoPitchYaw;
        inputActions.ShroudShip.PitchYaw.canceled -= DoStopPitchYaw;

        // Axial Movement
        inputActions.Player.StrafeUp.started -= DoStrafeY;
        inputActions.Player.StrafeDown.started -= DoStrafeY;
        inputActions.Player.StrafeLeft.started -= DoStrafeX;
        inputActions.Player.StrafeRight.started -= DoStrafeX;

        inputActions.Player.StrafeUp.canceled -= DoStopStrafing;
        inputActions.Player.StrafeDown.canceled -= DoStopStrafing;
        inputActions.Player.StrafeLeft.canceled -= DoStopStrafing;
        inputActions.Player.StrafeRight.canceled -= DoStopStrafing;

        inputActions.Player.BankLeft.started -= DoBank;
        inputActions.Player.BankRight.started -= DoBank;
        inputActions.Player.BankLeft.canceled -= DoStopBanking;
        inputActions.Player.BankRight.canceled -= DoStopBanking;


        inputActions.ShroudShip.StrafeUp.started -= DoStrafeY;
        inputActions.ShroudShip.StrafeDown.started -= DoStrafeY;
        inputActions.ShroudShip.StrafeLeft.started -= DoStrafeX;
        inputActions.ShroudShip.StrafeRight.started -= DoStrafeX;

        inputActions.ShroudShip.StrafeUp.canceled -= DoStopStrafing;
        inputActions.ShroudShip.StrafeDown.canceled -= DoStopStrafing;
        inputActions.ShroudShip.StrafeLeft.canceled -= DoStopStrafing;
        inputActions.ShroudShip.StrafeRight.canceled -= DoStopStrafing;

        inputActions.ShroudShip.BankLeft.started -= DoBank;
        inputActions.ShroudShip.BankRight.started -= DoBank;
        inputActions.ShroudShip.BankLeft.canceled -= DoStopBanking;
        inputActions.ShroudShip.BankRight.canceled -= DoStopBanking;


        // Combat
        inputActions.Player.FirePrimaryWeapon.performed -= DoFirePrimaryWeapon;
        inputActions.Player.FirePrimaryWeapon.canceled -= StopFirePrimaryWeapon;
        inputActions.Player.FireSecondaryWeapon.performed -= DoFireSecondaryWeapon;
        inputActions.Player.FireSecondaryWeapon.canceled -= StopFireSecondaryWeapon;
        inputActions.Player.CyclePrimaryWeaponBank.performed -= DoCyclePrimaryWeaponBank;
        inputActions.Player.CycleSecondaryWeaponBank.performed -= DoCycleSecondaryWeaponBank;
        inputActions.Player.CycleSecondaryWeaponFiringRate.performed -= DoCycleSecondaryWeaponFiringRate;

        // Targeting
        inputActions.Player.TargetNextClosestAnyShip.started -= DoTargetNextClosestAnyShip;
        inputActions.Player.TargetNextClosestEnemyShip.started -= DoTargetNextClosestEnemyShip;
        inputActions.Player.TargetNextClosestFriendlyShip.started -= DoTargetNextClosestFriendlyShip;
        inputActions.Player.TargetNextClosestEscortShip.started -= DoTargetNextClosestFriendlyEscortShip;
        inputActions.Player.TargetShipinReticle.started -= DoTargetShipInReticle;
        inputActions.Player.TargetClosestEnemyShip.started -= DoTargetClosestEnemyShip;
        inputActions.Player.TargetAttackingShip.started -= DoTargetAttackingShip;
        inputActions.Player.TargetNextSubSystem.started -= DoTargetNextSubSystem;

        inputActions.ShroudShip.TargetNextClosestAnyShip.started -= DoTargetNextClosestAnyShip;
        inputActions.ShroudShip.TargetNextClosestEnemyShip.started -= DoTargetNextClosestEnemyShip;
        inputActions.ShroudShip.TargetNextClosestFriendlyShip.started -= DoTargetNextClosestFriendlyShip;
        inputActions.ShroudShip.TargetNextClosestEscortShip.started -= DoTargetNextClosestFriendlyEscortShip;
        inputActions.ShroudShip.TargetShipinReticle.started -= DoTargetShipInReticle;
        inputActions.ShroudShip.TargetClosestEnemyShip.started -= DoTargetClosestEnemyShip;
        inputActions.ShroudShip.TargetAttackingShip.started -= DoTargetAttackingShip;
        inputActions.ShroudShip.TargetNextSubSystem.started -= DoTargetNextSubSystem;

        // Shields
        inputActions.Player.EqualiseShields.started -= DoEqualiseShields;
        inputActions.Player.AugmentBowShield.started -= DoAugmentBowShield;
        inputActions.Player.AugmentAftShield.started -= DoAugmentAftShield;
        inputActions.Player.AugmentPortShield.started -= DoAugmentPortShield;
        inputActions.Player.AugmentStarboardShield.started -= DoAugmentStarboardShield;

        // Power Management
        inputActions.Player.IncreaseWeaponPower.started -= DoIncreaseWeaponPower;
        inputActions.Player.DecreaseWeaponPower.started -= DoDecreaseWeaponPower;
        inputActions.Player.IncreaseShieldPower.started -= DoIncreaseShieldPower;
        inputActions.Player.DecreaseShieldPower.started -= DoDecreaseShieldPower;
        inputActions.Player.IncreaseEnginePower.started -= DoIncreaseEnginePower;
        inputActions.Player.DecreaseEnginePower.started -= DoDecreaseEnginePower;

        // Misc
        inputActions.Player.Pause.started -= DoPause;
        inputActions.UI.Pause.started -= DoPause;
        inputActions.ShroudShip.Pause.started -= DoPause;
    }
}
