using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using PixelPlay.OffScreenIndicator;
using Unity.VectorGraphics;
using UnityEngine.InputSystem;

public class PlayerTargeting : MonoBehaviour {

    [Tooltip("For the ray cast to ignore when targeting what is in front of the player")] public LayerMask layerMask;

    [SerializeField] private Color subSystemTargetColour = Color.yellow;

    private UITargetShipIcon uiTargetIcon;     // The icon on the HUD showing the targeted ship and shields
    private Camera targetCamera;
    private UITargetCamera uiTargetCamera;
    private UITargetIndicators uiTargetIndicators;
    private UIMissiles uiMissiles;

    private GameObject theCurrentTarget;    
    private SubComponent theSubTarget;
    private GameObject myAttacker;      // The ship attacking me
    private Shield myShield;
    private PlayerShipHullStrength myHull;

    private int targetDistance; // Calculated in this script, so it can't be gained from targetShip above
    private float targetSize = 0;
    private float targetSpeedLastFrame = 0.0f;

   // private bool isPaused = false;
    private bool disembarking = false; // So cant fire when leaving a carrier

    // Used for predictive targeting
    private PlayerGunFire guns;
    private MissileFire missiles;
    private Vector3 lastPosition;
    private bool exitingLevel = false;      // When player complets objectives and exits level

    private int subComponentCounter = 0;    // Used for cycling sub component targets
    private GameObject thisShip;

    // Screenbounds for NEW off screen targeter. Not the old shite I used to use
    private Vector3 screenCentre;
    private Vector3 screenBounds;

    [Range(0.5f, 0.9f)]
    [Tooltip("Distance offset of the indicators from the centre of the screen")]
    [SerializeField] private float screenBoundOffset = 0.9f;

    // All this is for the ray box to be cast in front of the player when they
    // press Y. This is to try to target ships inside the reticle (or close by)
    RaycastHit hit;
    bool hitDetect;
    Collider myCollider;
    float maxRayDistance;
    float rayBoxSize = 50f;

    void Start ()
    {
        maxRayDistance = 9999f;
        myCollider = GetComponent<Collider>();
        screenCentre = new Vector3(Screen.width, Screen.height, 0) / 2;
        screenBounds = screenCentre * screenBoundOffset;

        uiTargetIndicators = FindObjectOfType<UITargetIndicators>();                   // The bracket and info around the target ship
        uiTargetCamera = FindObjectOfType<UITargetCamera>();                    // The camera panel and info at the bottom left of screen
        uiMissiles = FindObjectOfType<UIMissiles>();                            // The missile selectors on the HUD
        uiTargetIcon = FindObjectOfType<UITargetShipIcon>();

        targetCamera = FindObjectOfType<UITargetRenderCamera>().GetComponent<Camera>();
        guns = GetComponentInChildren<PlayerGunFire>();
        missiles = GetComponentInChildren<MissileFire>();

        thisShip = GetComponent<PlayerShip>().gameObject;

        myShield = GetComponentInChildren<Shield>();
        myHull = GetComponentInChildren<PlayerShipHullStrength>();
   
        ClearTargetUIGraphics();
    }
	
	// Update is called once per frame
	void Update ()
    {
        if (!exitingLevel && !disembarking /*&& !isPaused*/)
        {
            // If target then do the graphics
            // If target is dead clear the target indicator and target camera
            if (theCurrentTarget)
                UITargetCamera(theCurrentTarget.GetComponent<AllShipBase>());
            else
                ClearTargetUIGraphics(); // Is this needed? can't it just be called once
        }
    }

    private void FixedUpdate()
    {
        if (theCurrentTarget && guns != null)
        {
            if (theSubTarget)
                DrawPredictiveTargeter(theSubTarget.gameObject);
            else
                DrawPredictiveTargeter(theCurrentTarget.gameObject);                
        }

        if(!exitingLevel)
        {
            if(theCurrentTarget)
                PositionTargetCamera();
        }
    }

    /// <summary>
    /// The target camera at the bottom left of the screen
    /// </summary>
    private void PositionTargetCamera()
    {
        Vector3 direction = theCurrentTarget.transform.position - gameObject.transform.position;
        float relativeOffset = 1 - (targetSize / direction.magnitude);
        targetCamera.transform.position = direction * relativeOffset + gameObject.transform.position;
       // targetCamera.transform.rotation = gameObject.transform.rotation;
        targetCamera.orthographicSize = targetSize;
    }

    /// ************************************************************************************************************************************** ///
    /// ********************************************************* TARGETING CONTROLS ********************************************************* ///
    /// ************************************************************************************************************************************** ///


    private void SetUpTargeting()
    {
        // Turns off sub component targeting until it 
        // is used.
        theSubTarget = null;
        uiTargetCamera.SetUISubCompName("");
        uiTargetCamera.SetUISubCompHealth("");

        uiTargetIndicators.SetScalingCube(theCurrentTarget.GetComponentInChildren<ScaleRelativeToCamera>().gameObject);               // Scaling cube for targeter
        InitialiseTargetIndicatorColours();
        SetupTargetSizeForCamera();
        uiTargetIcon.UpdateUIShip(theCurrentTarget.GetComponent<AllShipBase>());        // Sends the target to the UI to display an icon of it        

        // Initially set the target icons so they dont appear for one frame when you first target the ship
        DrawTargetBox(theCurrentTarget.transform.position);

        // Move the camera into position so that it doesn't flicker for that single frame when switching from small to large ships
        PositionTargetCamera();
    }

    /// <summary>
    /// If the returned target is null (no ships in level or whatever)
    /// </summary>
    private void ClearTargetUIGraphics()
    {
        theSubTarget = null;
        uiTargetCamera.gameObject.SetActive(false);
        uiTargetIndicators.SetActiveOnScreenIndicators(false);
        thisShip.GetComponent<PlayerShip>().SetMatchSpeed(false);        
        uiTargetIcon.UpdateUIShip(null); // Clear target icon at bottom of screen
    }

    //******************************************************************************************************************************************************//
    //*************************************************************  Targeting Functions  ******************************************************************//
    //******************************************************************************************************************************************************//

    /// <summary>
    /// Targeting ANY ship. This should pick up Humans, Nelvari, Legotians,  Zangy and maybe asteroids if I put them in
    /// </summary>
    public void TargetNextClosestAnyShip(InputAction.CallbackContext obj)
    {
        SetCurrentTarget(TargetingManager.NextClosestTargetAny(this.gameObject));
        if (theCurrentTarget) { SetUpTargeting(); }
        else { ClearTargetUIGraphics(); }
    }

    /// <summary>
    /// Targeting closest hostile ship
    /// </summary>
    public void TargetNextClosestEnemyShip(InputAction.CallbackContext obj)
    {
        SetCurrentTarget(TargetingManager.NextClosestHumanShip(this.gameObject));
        if (theCurrentTarget) { SetUpTargeting(); }
        else { ClearTargetUIGraphics(); }
    }


    /// <summary>
    /// Target next closest friendly ship
    /// </summary>
    public void TargetNextClosestFriendlyShip(InputAction.CallbackContext ob)
    {
        SetCurrentTarget(TargetingManager.NextClosestNelvariShip(this.gameObject));
        // If the player gets targeted then try again
        if (theCurrentTarget == thisShip)
            SetCurrentTarget(TargetingManager.NextClosestNelvariShip(this.gameObject));
        // If player still the target the clear the target
        if (theCurrentTarget == thisShip)
            theCurrentTarget = null;

        if (theCurrentTarget) { SetUpTargeting(); }
        else { ClearTargetUIGraphics(); }
    }

    /// <summary>
    /// Target friendly escort ship
    /// </summary>
    public void TargetNextClosestFriendlyEscortShip(InputAction.CallbackContext ob)
    {
        SetCurrentTarget(TargetingManager.NextClosestNelvariCapital(this.gameObject));
        if (theCurrentTarget) { SetUpTargeting(); }
        else { ClearTargetUIGraphics(); }
    }

    /// <summary>
    /// Target whatever shp is directly in front of the player
    /// </summary>
    public void TargetShipInReticle(InputAction.CallbackContext ob)
    {

        // UPDATE THIS TO FIND SHIPS INSIDE THE SCREEN AREA, THEN INSIDE THE RETICULE
        // SOMEHOW USING WORLDPOINTTOSCREENPOINT

        hitDetect = Physics.BoxCast(myCollider.bounds.center, transform.localScale * rayBoxSize, transform.forward, out hit, transform.rotation, maxRayDistance, layerMask);
        if (hitDetect)
        {
            //Debug.Log("Hit : " + hit.collider.name);
            //Debug.Log("Hit : " + hit.collider.GetComponentInParent<AllShipBase>());
            SetCurrentTarget(hit.collider.GetComponentInParent<AllShipBase>().gameObject);
            if (theCurrentTarget) { SetUpTargeting(); }
            else { ClearTargetUIGraphics(); }
        }
    }

    //Draw the BoxCast as a gizmo to show where it currently is testing. Click the Gizmos button to see this
    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;

        //Check if there has been a hit yet
        if (hitDetect)
        {            
            Gizmos.DrawRay(transform.position, transform.forward * hit.distance); //Draw a Ray forward from GameObject toward the hit            
            Gizmos.DrawWireCube(transform.position + transform.forward * hit.distance, transform.localScale * rayBoxSize); //Draw a cube that extends to where the hit exists
        }
        //If there hasn't been a hit yet, draw the ray at the maximum distance
        else
        {            
            Gizmos.DrawRay(transform.position, transform.forward * maxRayDistance); // Draw a Ray forward from GameObject toward the maximum distance            
            Gizmos.DrawWireCube(transform.position + transform.forward * maxRayDistance, transform.localScale * rayBoxSize); // Draw a cube at the maximum distance
        }
    }

    // Will only ever target the closest enemy and not cycle through
    public void TargetClosestEnemyShip(InputAction.CallbackContext ob)
    {
        SetCurrentTarget(TargetingManager.ClosestHumanShip(this.gameObject));
        if (theCurrentTarget) { SetUpTargeting(); }
        else { ClearTargetUIGraphics(); }
    }

    public void TargetAttackingShip(InputAction.CallbackContext ob)
    {
        // Get the last weapon that impacted on the hull or shield and return the parent
        myAttacker = myShield.GetTheAttacker();

        if (myAttacker == null)
            myAttacker = myHull.GetTheAttacker();
        if (myAttacker != null)
        {
            SetCurrentTarget(myAttacker);
            SetUpTargeting();
        }
    }

    /// <summary>
    /// Cycle through sub systems on the current target
    /// Subsystems are: Engines, Weapons, Shields, Communications
    /// Individual engines, individual weapons
    /// </summary>
    public void TargetNextSubSystem(InputAction.CallbackContext ob)
    {
        if (theCurrentTarget)
        {
            SubComponent[] temp;
            temp = theCurrentTarget.GetComponent<AllShipBase>().GetSubComponents();

            if (temp != null && temp.Length >= 1) // If there is at least 1 sub component
            {
                int compNum = temp.Length;
                if (subComponentCounter == compNum)
                    subComponentCounter = 0;

                theSubTarget = temp[subComponentCounter];
                uiTargetCamera.SetUISubCompName(theSubTarget.GetHullName());
                uiTargetCamera.SetUISubCompHealth(theSubTarget.GetSubHullStrength().ToString() + "%");
                uiTargetIndicators.SetPredictiveAimColour(subSystemTargetColour);

                subComponentCounter++;
            }
            if (theSubTarget)
                uiTargetCamera.SetUISubCompHealth(theSubTarget.GetSubHullStrength().ToString() + "%");
        }
    }

    /// ************************************************************************************************************************************** ///
    /// ********************************************************* TARGETING GRAPHICS ********************************************************* ///
    /// ************************************************************************************************************************************** ///

    /// <summary>
    /// Initialising the target indicators and their colour
    /// </summary>
    private void InitialiseTargetIndicatorColours()
    {
        if (theCurrentTarget) { uiTargetIndicators.SetTargetIndicatorsColour(theCurrentTarget); }        
    }

    private void UITargetCamera(AllShipBase targetShip)
    {
        uiTargetCamera.gameObject.SetActive(true);
        //uiTargetIndicators.gameObject.SetActive(true);
        //uiTargetIndicators.SetActiveOnScreenIndicators(true);

        targetDistance = (int)Vector3.Distance(gameObject.transform.position, targetShip.transform.position);
        uiTargetIndicators.SetTargetDistanceText(targetDistance.ToString());        // Update player distance to target
        uiTargetIndicators.SetTargetHealth(targetShip.GetShipHullStrength());

        DrawTargetBox(targetShip.transform.position);
        DrawOffScreenIndicator(targetShip.transform.position);

        // Display sub component info
        if(theSubTarget)
        {
            uiTargetCamera.SetUISubCompName(theSubTarget.GetHullName());
            uiTargetCamera.SetUISubCompHealth(theSubTarget.GetSubHullStrength().ToString() + "%");
        }
        else
        {
            uiTargetCamera.SetUISubCompName("");
            uiTargetCamera.SetUISubCompHealth("");
        }

        uiTargetCamera.EnterTargetInformationOnCameraPanel(targetShip, targetDistance);
    }


    private void DrawPredictiveTargeter(GameObject target)
    {
        /// ENERGY WEAPON TARGETER
        Vector3 temp;
        temp = PredictShot(gameObject, target, guns.GetWeaponSpeed());    // Find predicted target place
        Vector3 targetPredictPosOnScreen = Camera.main.WorldToScreenPoint(temp);
        bool isTargetVisible = OffScreenIndicatorCore.IsTargetVisible(targetPredictPosOnScreen);
        targetDistance = (int)Vector3.Distance(gameObject.transform.position, theCurrentTarget.transform.position);


        // If visible turn on or off the guns and missiles indicators
        if (isTargetVisible && targetDistance < guns.GetWeaponRange())
            uiTargetIndicators.SetActivePredictiveAim(true);
        else
            uiTargetIndicators.SetActivePredictiveAim(false);

        if(uiTargetIndicators.targetPredictiveAim.IsActive())
            uiTargetIndicators.SetPredictiveAimPos(new Vector3(targetPredictPosOnScreen.x, targetPredictPosOnScreen.y, 0));

        /// MISSILE TARGETER
        if (isTargetVisible && targetDistance < missiles.GetActiveMissileRange() && missiles.GetActiveMissileCount() > 0)
            uiTargetIndicators.SetActiveMissileLockIndicator(true);
        else
            uiTargetIndicators.SetActiveMissileLockIndicator(false);

        if(target != null)
            uiTargetIndicators.SetTarget(target);
    }

    /// <summary>
    /// Displays and sets the position for the following
    /// Target Bracket
    /// Target Distance Text
    /// Target Health Box
    /// </summary>
    /// <param name="target"></param>
    private void DrawTargetBox(Vector3 target)
    {
        Vector3 targetPosOnScreen = Camera.main.WorldToScreenPoint(target);
        bool isTargetVisible = OffScreenIndicatorCore.IsTargetVisible(targetPosOnScreen);

        // If target is behind player, stop updating the position
        if (isTargetVisible)
        {
            uiTargetIndicators.SetActiveScalingTargetBox(true);
            uiTargetIndicators.SetActiveTargetDistanceText(true);
            uiTargetIndicators.SetActiveTargetHealth(true);
      
            uiTargetIndicators.SetDistanceTextPos(new Vector3(targetPosOnScreen.x, targetPosOnScreen.y-60, 0));
            uiTargetIndicators.SetTargetHealthSliderPos(new Vector3(targetPosOnScreen.x, targetPosOnScreen.y + 60, 0));
            // Debug.Log("Target is in front of this game object.");
        }
        else
        {
            uiTargetIndicators.SetActiveScalingTargetBox(false);
            uiTargetIndicators.SetActiveTargetDistanceText(false);
            uiTargetIndicators.SetActiveTargetHealth(false);
        }
    }
    
    /// <summary>
    /// Displays and sets the position for the following
    /// Off screen indicator
    /// Target Distance Text
    /// </summary>
    /// <param name="target"></param>
    private void DrawOffScreenIndicator(Vector3 target)
    {
        Vector3 targetPosOnScreen = Camera.main.WorldToScreenPoint(target);
        bool isTargetVisible = OffScreenIndicatorCore.IsTargetVisible(targetPosOnScreen);


        if (isTargetVisible)
        {
            uiTargetIndicators.SetActiveOffScreenIndicator(false);
        }
        else
        {
            uiTargetIndicators.SetActiveOffScreenIndicator(true);

            float angle = float.MinValue;
            OffScreenIndicatorCore.GetArrowIndicatorPositionAndAngle(ref targetPosOnScreen, ref angle, screenCentre, screenBounds);
            uiTargetIndicators.SetOffScreenIndicatorRot(Quaternion.Euler(0, 0, angle * Mathf.Rad2Deg)); // Sets the rotation for the arrow indicator.
            uiTargetIndicators.SetOffScreenIndicatorPos(targetPosOnScreen);

            uiTargetIndicators.SetOffScreenDistanceText(targetDistance.ToString());        // Update player distance to target 
            uiTargetIndicators.SetOffScreenDistanceRot(Quaternion.Euler(0.0f, 0.0f, 0f));
        }
    }

    /// <summary>
    /// The target camera view
    /// </summary>
    private void SetupTargetSizeForCamera()
    {
        targetSize = 0;
        // This finds the bounds of the first child in the hierarchy, which always HAS
        // to be the TargetCameraSize object for this to work
        targetSize = (int)theCurrentTarget.GetComponentInChildren<MeshFilter>().mesh.bounds.size.z;
        // targetSize should be multiplied by the scale of the mesh.
        targetSize *= (int)theCurrentTarget.GetComponentInChildren<MeshFilter>().transform.localScale.x;
        uiTargetCamera.gameObject.SetActive(true);
        if(targetSize == 0)
        {
            Debug.Log("Target size = 0 and is not set up correctly for target camera");
        }
    }

    // Player needs to predict where the enemy will bee
    // then move the predictive target reticule there (it is on the enemy itself
    // so player needs a reference to the enemy, itself, and its weapon
    // targetShip.ReturnShipSpeed().ToString()

    private Vector3 PredictShot(GameObject shooter, GameObject targetShip, float projectileSpeed)
    {
        // Debug.Log("Projectile speed: " + projectileSpeed);
        // Will be 0 if this is the first frame that the enemy has been targeted
        if (lastPosition == Vector3.zero)
        {
            lastPosition = targetShip.transform.position;
            return Vector3.zero;
        }
        // Get the target's Vector speed from its last position
        Vector3 targetShipSpeed = (targetShip.transform.position - lastPosition) / Time.deltaTime;
        lastPosition = targetShip.transform.position;
        Vector3 toTarget = targetShip.transform.position - shooter.transform.position;        

        float a = Vector3.Dot(targetShipSpeed, targetShipSpeed) - (projectileSpeed * projectileSpeed);
        float b = 2 * Vector3.Dot(targetShipSpeed, toTarget);
        float c = Vector3.Dot(toTarget, toTarget);

        float p = -b / (2 * a);

        //testing

        float q = (float)Mathf.Sqrt((b * b) - 4 * a * c) / (2 * a);

        float t1 = p - q;
        float t2 = p + p;
        float t;

        if (t1 < t2 && t2 > 0)
        {
            t = t2;
        }
        else
        {
            t = t1;
        }

        Vector3 aimSpot = targetShip.transform.position + targetShipSpeed * t;
        return aimSpot;        
    }

    public GameObject GetCurrentTarget() => theCurrentTarget == true ? theCurrentTarget.gameObject : null;

    public void SetCurrentTarget(GameObject target)
    {
        if (theCurrentTarget)
            theCurrentTarget.GetComponentInChildren<ScaleRelativeToCamera>().enabled = false;
        theCurrentTarget = target;
        if (theCurrentTarget)
            theCurrentTarget.GetComponentInChildren<ScaleRelativeToCamera>().enabled = true;
    }

    public float GetCurrentTargetSpeed()
    {         
        if (theCurrentTarget)
        {
            targetSpeedLastFrame = theCurrentTarget.GetComponent<AllShipBase>().GetShipSpeed();
            return targetSpeedLastFrame;    // Is actually this frame but W/E
        }        
        else
            return targetSpeedLastFrame;
    }

    public void ExitingLevelStopControls() => exitingLevel = true;
    public float GetTargetDistance() => targetDistance;
    public void SetDisembarking(bool value) => disembarking = value;
    //public void SetIsGamePaused(bool b) => isPaused = b;
}