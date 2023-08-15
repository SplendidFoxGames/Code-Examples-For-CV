using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.UI.Image;
using UnityEngine.UIElements;
using UnityEngine.Rendering;

/// <summary>
/// FIGHTER, BOMBER, CORVETTE, FRIGATE
/// </summary>

public abstract class AISmallFighterShipBase : AIShipBase
{
    //protected float attackRunTime = 3f;
    //protected float attackRunTimer = 0f;
    protected float attackRunDistance = 0f;     // Testing distance instead of time
    [SerializeField] protected float scanTargetDistance = 999999f;   // By default ships can scan the entire map, but this can be changed for mission specific reasons
    [SerializeField] protected bool primaryEscort = false;          // Primary escorts will stick with the ship and only attack when enemy is in range (or shoots them)
    protected GameObject theCurrentEscortTarget;
    protected EscortPoint escortPointTarget = null;

    private bool allowTargetChange = true;       // if false then Never allow this ship to change targets, even if the AIShipHullStrength and ShieldImpact classes request it

    protected float dodgeTime = 4.0f;               // Dodge every second if you are still in danger
    protected float dodgeTimer = 0;
    protected Vector3 lastDodgeDirection;

    protected float rayScanTime = 1.0f;             // Scan every second for collisions
    protected float rayScanTimer = 0;
    protected float rayCastOffset = 0.35f;

    protected float attackRunMaxTime = 20.0f;      // In case a ship gets stuck in the attack run, maybe it can't escape the enemy, after this timer it will come out of it and carry on as normal
    protected float attackRunMaxTimer = 0.0f;

    [SerializeField] protected float turnSpeed = 5000f; // 600 for small ships?

    protected Vector3 targetLastPosition = Vector3.zero;
    protected Vector3 shootHere;

    //[SerializeField] protected float shipStrafeSpeed = 9000;
    // JUST for collision avoidance
    [SerializeField]
    private LayerMask ignoreCollisionLayer;  // Radar sphere, shields, collision avoidance, projectiles need to be ignored

    private new void Start()
    {
        base.Start();
        InitialiseAIShipBase();
        dodgeTime += Random.Range(-0.4f, 0.4f);

        // Randomise the turn speed by 10% +/-
        float _20pc = (turnSpeed / 100) * 20;
        float randomVal = Random.Range(-_20pc, _20pc);
        turnSpeed += randomVal;
    }

    // Update is called once per frame
    public void Update()
    {
        if(!flyForwards)
        {
            switch (AIState)
            {
                case AIStates.IDLE:
                    IdleAIState();
                    break;
                case AIStates.ATTACKING:
                    AttackingAIState();
                    break;
                case AIStates.ATTACKRUN:    // Add code somewhere to match speed
                    AttackRunAIState();
                    break;
                case AIStates.ESCORTING:
                    // Escort a friendly ship and defend it
                    // While escorting, you must keep scanning for enemy ships
                    if (!primaryEscort)
                        ScanForEnemyTargets();
                    EscortShipAIState();
                    break;
                case AIStates.DODGING:
                    DodgingAIState();
                    break;
                case AIStates.DISEMBARKING: // Exiting a carrier
                    break;
                case AIStates.PATROL:
                    PatrolAIState();
                    break;
                case AIStates.WARPING:  // DO nothing. The warp script will change this when needed
                    break;
            }
        }
        AccelerateDecelerate();
        shipThrottleSpeed = Mathf.Clamp(shipThrottleSpeed, minSpeed, maxSpeed);

        //// Ray Forward
        ////Debug.DrawRay(transform.position, transform.forward * (6.5f * shipCurrentSpeed), Color.yellow);
        ////Ray Up
        //Debug.DrawRay(transform.position, (transform.forward + transform.TransformDirection(new Vector3(0, 0.35f, 0))) * (5f * shipCurrentSpeed), Color.yellow);
        ////Ray Down
        //Debug.DrawRay(transform.position, (transform.forward + transform.TransformDirection(new Vector3(0, -0.35f, 0))) * (5f * shipCurrentSpeed), Color.yellow);
        ////Ray Right
        //Debug.DrawRay(transform.position, (transform.forward + transform.TransformDirection(new Vector3(0.35f, 0, 0))) * (5f * shipCurrentSpeed), Color.yellow);
        ////Ray Left
        //Debug.DrawRay(transform.position, (transform.forward + transform.TransformDirection(new Vector3(-0.35f, 0, 0))) * (5f * shipCurrentSpeed), Color.yellow);

        CollisionAvoidance();
    }

    public virtual void FixedUpdate()
    {
           
        MoveShip();
        if (warpArrivalComplete && AIState == AIStates.ATTACKING && theCurrentTarget != null)
        {
            shootHere = PredictShot(this.gameObject, theCurrentTarget, weapons.GetWeaponSpeed());      // Get projectile speed from projectile weapons.GetWeaponSpeed()
            FaceTheTarget();
        }


        if(AIState == AIStates.DODGING)
        {
            if (Time.time <= dodgeTimer)
            {
                // Do nothing. But the ship will continue flying forward as normal
                // after the torque has turned it
            }
            else
            {
                SetThrottleSpeed(maxSpeed);
                AIState = AIPreviousState;
            }
        }
    }

    //*********************************************************************************//
    //******************************* IDLE AI STATE ***********************************//
    //*********************************************************************************//

    protected override void ScanForEnemyTargets()
    {
        if (Time.time > targetScanTimer)
        {
            // If primary escort, look for escorts first
            if (primaryEscort)
            {
                FindShipToEscort();

                if (theCurrentTarget)
                    return;
                FindClosestShipToAttack();
            }
            else
            {
                FindClosestShipToAttack();
                if (theCurrentTarget)
                    return;
                FindShipToEscort(); // Comment this out if it causes problems
            }
            // else look for enemies first

            targetScanTime = Random.Range(2f, 4f);
            targetScanTimer = Time.time + targetScanTime;
        }
    }

    protected override void MoveShip()
    {
        myRigidBody.AddForce(transform.forward * shipCurrentSpeed * speedMultiplier);
    }

    protected override void AttackingAIState()
    {
        // Should only be called when target is dead and a new one needs to be found
        if (theCurrentTarget == null)
        {
            distanceToTarget = 100000;
            SetAIStateIdle();
        }
        else
        {
            ReCalculateDistanceToTarget();  // Find distance to target every frame

            //float temp = theCurrentTarget.GetComponentInChildren<MeshFilter>().transform.localScale.x;
            if (distanceToTarget > 600 + theCurrentTargetMeshBounds) { SetThrottleSpeed(maxSpeed); }
            else if (distanceToTarget > 350 + theCurrentTargetMeshBounds) { SetThrottleSpeed((maxSpeed / 2)); }
            else if (distanceToTarget > 200 + theCurrentTargetMeshBounds) { SetThrottleSpeed((maxSpeed / 3)); }
            else if (AIState != AIStates.ATTACKRUN)
            {
                SetAIStateAttackRun();
            }
       }
    }

    protected override void FaceTheTarget()
    {
        Vector3 theVector = shootHere - transform.position;
        Quaternion targetRotation = Quaternion.identity;

        if (!float.IsNaN(shootHere.x))   // If X is nan, then all vector values are nan
        {
            targetRotation = Quaternion.LookRotation(theVector);
            myRigidBody.MoveRotation(Quaternion.Lerp(transform.rotation, targetRotation, shipRotationSpeed * Time.deltaTime));
        }

        Quaternion myRotation = myRigidBody.rotation;
        Quaternion difference = targetRotation * Quaternion.Inverse(myRotation);

        // If this ship is facing close to it's target (which is the shootHere point) then you can fire
        float tempDistOffset = 0.009f;
        if (difference.x >= -tempDistOffset && difference.x <= tempDistOffset &&
            difference.y >= -tempDistOffset && difference.y <= tempDistOffset &&
            difference.z >= -tempDistOffset && difference.z <= tempDistOffset)
        {
            weapons.SetCanFire(true);
            Debug.DrawRay(transform.position, transform.forward * 2500, Color.white);
        }            
        else
        {
            weapons.SetCanFire(false);
            Debug.DrawRay(transform.position, transform.forward * 2500, Color.red);
        }
    }

    protected override void FindClosestShipToAttack()
    {
        // Check which species this ship is and target the correct enemy
        // Ships which are escorting other ships will search for an enemy.
        // If no enemy exists they need to ignore the "return null" part, or else
        // they will clear their escort target
        GameObject temp;
        if (GetSpecies() == AllShipBase.Species.NELVARI)
        {
            
            temp = TargetingManager.NextClosestHumanShip(gameObject);
            if(temp == null)
                return; // Just ignore it return
            else
                theCurrentTarget = TargetingManager.NextClosestHumanShip(gameObject);
        }            
        else
        {
            temp = TargetingManager.NextClosestNelvariShip(gameObject, true, scanTargetDistance);
            if (temp == null)
                return; // Just ignore it
            else
                theCurrentTarget = TargetingManager.NextClosestNelvariShip(gameObject, true, scanTargetDistance);
        }

        // Only relevant if the ship has an enemy target
        if (theCurrentTarget)
        {
            if (theCurrentTarget.tag == "Instructor" || theCurrentTarget.tag == "Tutorial")
            {
                theCurrentTarget = TargetingManager.NextClosestNelvariShip(gameObject, true, scanTargetDistance);
            } // If target is something on the tutorial level, ask for another

            // This picks up the TargetCameraSize which is the first in the hierarchy on any ship
            SetCurrentTargetMeshBounds();
            SetThrottleSpeed(maxSpeed);
            if (theCurrentEscortTarget)
                theCurrentEscortTarget.GetComponent<AINelvariTransport>().RelinquishEscortPoint(escortPointTarget);
            escortPointTarget = null;
            AIState = AIStates.ATTACKING;            
        }
    }

    //protected void SetCurrentTargetMeshBounds()
    //{
    //    theCurrentTargetMeshBounds = theCurrentTarget.GetComponentInChildren<MeshFilter>().transform.localScale.x;
    //    weapons.SetTarget(theCurrentTarget.gameObject, theCurrentTargetMeshBounds);
    //}


    protected void FindShipToEscort()
    {
        if (Time.time > targetScanTimer)
        {
            // Check which species this ship is and target the correct enemy
            if (GetSpecies() == AllShipBase.Species.NELVARI)
                theCurrentTarget = TargetingManager.NextClosestNelvariTransport(gameObject);
            else
                theCurrentTarget = TargetingManager.NextClosestHumanTransport(gameObject);

            if (theCurrentTarget)
            {
                theCurrentEscortTarget = theCurrentTarget;
                MatchTargetSpeed();
                escortPointTarget = theCurrentEscortTarget.GetComponent<AINelvariTransport>().GetNextEmptyEscortPoint(this.gameObject);

                // If you dont find one, search again, but no more after that (maybe fix this later to be more robust)
                if (escortPointTarget == null)
                {
                    //theCurrentTarget = tm.NextClosestNelvariEscort(gameObject);
                    escortPointTarget = theCurrentEscortTarget.GetComponent<AINelvariTransport>().GetNextEmptyEscortPoint(this.gameObject);
                }
                AIState = AIStates.ESCORTING;
            }
            targetScanTimer = Time.time + targetScanTime;
        }
    }
    //*********************************************************************************//
    //**************************** ATTACK RUN AI STATE ********************************//
    //*********************************************************************************//

        /// <summary>
        /// Testing changing attack run state. Currently it works on a random timer
        /// Will set it to make sure the ship is far enough away from the target before turning
        /// back. Hopefully this will stop little ships getting stuck on large ships or starbases
        /// </summary>
    protected void AttackRunAIState()
    {
        //if (Time.time > attackRunTimer)
        if (theCurrentTarget)
            CalculateDistanceToTarget(theCurrentTarget);
        else
            SetAIStateIdle();

        if (theCurrentTarget && distanceToTarget >=  attackRunDistance ||
            Time.time >= attackRunMaxTimer)
            AIState = AIStates.ATTACKING;
    }

    // ONLY set by the AI Collision Avoidance when running into the target ship
    public void SetAIStateAttackRun()
    {
        weapons.SetCanFire(false);

        attackRunDistance = Random.Range(300, 800);
        attackRunDistance += theCurrentTargetMeshBounds;
        TurnShipRandom();

        AIState = AIStates.ATTACKRUN;
        attackRunMaxTimer = Time.time + attackRunMaxTime;
        SetThrottleSpeed(maxSpeed);
    }

    private void TurnShipRandom()
    {
        int direction = Random.Range(1, 7);
        float turnMultiplier = Random.Range(2.3f, 2.8f);
        switch (direction)
        {
            case 1:
                myRigidBody.AddRelativeTorque(-turnSpeed * turnMultiplier, 0, 0);
                break;
            case 2:
                myRigidBody.AddRelativeTorque(0, turnSpeed * turnMultiplier, 0);
                break;
            case 3:
                myRigidBody.AddRelativeTorque(turnSpeed * turnMultiplier, 0, 0);
                break;
            case 4:
                myRigidBody.AddRelativeTorque(0, -turnSpeed * turnMultiplier, 0);
                break;
            case 5:
                myRigidBody.AddRelativeTorque(-turnSpeed * turnMultiplier, turnSpeed * 2.5f, 0);
                break;
            case 6:
                myRigidBody.AddRelativeTorque(turnSpeed * turnMultiplier, -turnSpeed * 2.5f, 0);
                break;
            case 7:
                myRigidBody.AddRelativeTorque(0, 0, -turnSpeed * turnMultiplier);
                break;
            case 8:
                myRigidBody.AddRelativeTorque(-turnSpeed * turnMultiplier, turnSpeed * turnMultiplier, 0);
                break;
            case 9:
                myRigidBody.AddRelativeTorque(-turnSpeed * turnMultiplier, 0, -turnSpeed * turnMultiplier);
                break;
        }
    }

    //*********************************************************************************//
    //***************************** ESCORTING AI STATE ********************************//
    //*********************************************************************************//
    protected void EscortShipAIState()
    {        
        // If escort ship exists, calculate, if not, go back to idle state
        if (theCurrentTarget != null)
            ReCalculateDistanceToTarget(escortPointTarget);
        else
            SetAIStateIdle();

        if (distanceToTarget > 400) { SetThrottleSpeed(maxSpeed); }
        else if (distanceToTarget > 250) { SetThrottleSpeed((maxSpeed / 2) * 2); }
        else if (distanceToTarget <= 100) { MatchTargetSpeed(); }
        // else, go full speed to catch up

        EscortTheTarget();
        // Scan for enemies. If an enemy fighter gets within xxxx then attack it
        if(Time.time > targetScanTimer)
        {
            FindShipToAttackDistance(1000);
            targetScanTime = Random.Range(2f, 4f);
            targetScanTimer = Time.time + targetScanTime;
        }
    }

    protected void EscortTheTarget()
    {
        if (escortPointTarget)
        {
            Quaternion targetRotation = Quaternion.LookRotation(escortPointTarget.transform.position - transform.position);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, shipRotationSpeed * Time.deltaTime);
        }
        else // The escort target has been lost, probably due to being in a fight. Clear all targets and reset to idle and try again
        {
            SetAIStateIdle();            
        }
    }

    protected void FindShipToAttackDistance(float distance)
    {
        GameObject tempTarget;
        if (GetSpecies() == AllShipBase.Species.NELVARI)
            tempTarget = TargetingManager.NextClosestHumanShip(gameObject);
        else
            tempTarget = TargetingManager.NextClosestNelvariShip(gameObject);

        if(tempTarget)
        {
            float tempDistance = 0;
            tempDistance = CalculateDistanceToTarget(tempTarget);
            // If target is over 1000 away do nothing, else attack it
            if (tempDistance > 1000)
            {

            }
            else
            {
                SetThrottleSpeed(maxSpeed);
                if (theCurrentEscortTarget)
                    theCurrentEscortTarget.GetComponent<AINelvariTransport>().RelinquishEscortPoint(escortPointTarget);
                escortPointTarget = null;
                theCurrentTarget = tempTarget;
                weapons.SetTarget(theCurrentTarget.gameObject, theCurrentTargetMeshBounds);

                AIState = AIStates.ATTACKING;
            }
        }
    }

    protected float CalculateDistanceToTarget(GameObject target)
    {
        return distanceToTarget = Vector3.Distance(target.transform.position, this.transform.position);
    }

    protected void ReCalculateDistanceToTarget(EscortPoint escortPoint)
    {
        if (escortPoint)
        {
            distanceToTarget = Vector3.Distance(escortPoint.transform.position, this.transform.position);
        }
        else // The escort target has been lost, probably due to being in a fight. Clear all targets and reset to idle and try again
        {
            SetAIStateIdle();
        }
    }

    //*********************************************************************************//
    //****************************** PATROL AI STATE **********************************//
    //*********************************************************************************//
    void PatrolAIState()
    {
        aiPatrolTimer -= Time.deltaTime;
        if(aiPatrolTimer <=0)
        {
            // Set speed
            SetThrottleSpeed(65);
            // change direction
            TurnShipRandom();

            aiPatrolTimer = Random.Range(45.0f, 65.0f);
        }
        
        // Scan for targets
        if (theCurrentTarget == null)
        {
            distanceToTarget = 100000;
            ScanForEnemyTargets();
        }

        // Swap back to idle
    }
    //*********************************************************************************//
    //*************************** DISEMBARKING AI STATE *******************************//
    //*********************************************************************************//
    public void DisembarkAIState()
    {
        Collider[] theColliders;

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

        GetComponent<AIWarpDrive>().SetWarpIn(false);
        SetThrottleSpeed(shipCurrentSpeed = maxSpeed);
        AIState = AIStates.DISEMBARKING;
        //myRigidBody.AddRelativeTorque(Vector3.up * 500);
        Invoke("Disembarked", 10.0f);
    }

    private void Disembarked()
    {
        SetAIStateIdle();
        flyForwards = false;
        Collider[] theColliders;

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
    }

    //*********************************************************************************//
    //***************************** DODGING AI STATE **********************************//
    //*********************************************************************************//
    private void DodgingAIState()
    {
        // When an impact is noticed, the ship is set in a direction to dodge
        // Travel forward and not towards the target for x seconds
        // Resume previous state and head back towards target
        if (Time.time > dodgeTimer)
        {
            AIState = AIPreviousState;
        }
    }

    ////*********************************************************************************//
    ////****************************** HELPER METHODS ***********************************//
    ////*********************************************************************************//
    public void ChangeAIState(AIStates state)
    {
        theCurrentTarget = null;
        AIState = state;
    }

    /// <summary>
    /// Direction is TORQUE. So the ship will rotate on the direction axis. So for example
    /// Vector3.up doesnt make the ship turn up. It adds torque to the UP vector, which turns the ship right
    /// </summary>
    /// <param name="direction"></param>
    public void TurnShip(Vector3 direction)
    {
        Debug.Log("Turning");
        if (myRigidBody)  { myRigidBody.AddRelativeTorque(direction * turnSpeed); }
    }

    // Called by shield or hull when attacked
    public void Strafe()
    {        
        int strafeDecision = Random.Range(1, 100);
        // 10% chance to strafe
        if (strafeDecision >= 90) 
        {   
            int x = Random.Range(-1, 2);
            int y = Random.Range(-1, 2);
            // Prevent X or Y being 0
            if(x==0)
                x = 1;
            if (y == 0)
                y = 1;
            Vector3 dir = new Vector3(x, y, 0);

            myRigidBody.AddRelativeForce(dir * shipStrafeSpeed, ForceMode.Acceleration);
        }        
    }

    public void Strafe(Vector3 direction)
    {
        myRigidBody.AddRelativeForce(direction * shipStrafeSpeed, ForceMode.Acceleration);
    }

    /// <summary>
    /// Change targets when a weapon hits you
    /// </summary>
    /// <param name="newTarget"></param>
    public void ChangeTargetsAttack(GameObject newTarget)
    {
        if (!allowTargetChange)
            return;

        GameObject tempParent = newTarget.GetComponent<WeaponProjectile>().GetParentShip();
        GameObject tempTurret = newTarget.GetComponent<WeaponProjectile>().GetTurret();
        // If the parent target is another small ship then just attack that ship
        // If the parent target is a capital ship then pick either attacking the ship itself, or the gun, or the weapon system.

        if (theCurrentTarget == tempTurret || theCurrentTarget == tempTurret)
            return;

        if (tempTurret)
        {
            theCurrentTarget = tempTurret;
            theCurrentTargetMeshBounds = 100;
        }
            
        else if (tempParent)
            theCurrentTarget = tempParent;

        //theCurrentTarget = newTarget;
        //Debug.Log(thisShip.ReturnShipName() + " has changed target to: " + theCurrentTarget);
        if(theCurrentTarget)
            weapons.SetTarget(theCurrentTarget.gameObject, theCurrentTargetMeshBounds);
        AIState = AIStates.ATTACKING;
    }

    /// <summary>
    /// Explicitly change target to a ship
    /// Only use to set in code for gameplay reasons.
    /// </summary>
    /// <param name="theShip"></param>
    public void ChangeTargetShip(GameObject newTarget)
    {
        if (theCurrentTarget == newTarget)
            return;
        theCurrentTarget = newTarget;
        //Debug.Log(thisShip.ReturnShipName() + " has changed target to: " + theCurrentTarget);
        theCurrentTargetMeshBounds = theCurrentTarget.GetComponentInChildren<MeshFilter>().transform.localScale.x;
        if(!weapons)
            weapons = GetComponentInChildren<AIWeaponFire>();
        if (weapons)
            weapons.SetTarget(theCurrentTarget.gameObject, theCurrentTargetMeshBounds);

        AIState = AIStates.ATTACKING;
    }

    /// <summary>
    /// Change targets to escort a ship
    /// </summary>
    public void ChangeTargetEscort(GameObject newTarget)
    {
        theCurrentTarget = newTarget;
        if (theCurrentTarget)
        {
            theCurrentEscortTarget = theCurrentTarget;
            MatchTargetSpeed();
            escortPointTarget = theCurrentEscortTarget.GetComponent<AINelvariTransport>().GetNextEmptyEscortPoint(this.gameObject);

            // If you dont find one, search again, but no more after that (maybe fix this later to be more robust)
            if (escortPointTarget == null)
            {
                //theCurrentTarget = tm.NextClosestNelvariEscort(gameObject);
                escortPointTarget = theCurrentEscortTarget.GetComponent<AINelvariTransport>().GetNextEmptyEscortPoint(this.gameObject);
            }
            AIState = AIStates.ESCORTING;
        }
        targetScanTimer = Time.time + targetScanTime;
    }

    public void ChangeTargetsFollow(GameObject newTarget)
    {
        if (theCurrentTarget == newTarget)
            return;
        theCurrentTarget = newTarget;
        AIState = AIStates.ATTACKING;
        maxSpeed = 80;
        SetThrottleSpeed((maxSpeed /= 3));
    }

    // Ensures that the ship will stick with the transport unless attacked
    // If they aren't primary, then they will break off and immediately go for the enemy
    public void SetPrimaryEscort(bool b) => primaryEscort = b;
    

    public GameObject GetTarget() => theCurrentTarget;

    private Vector3 PredictShot(GameObject shooter, GameObject targetShip, float projectileSpeed)
    {
        // Get the target's Vector speed from its last position
        Vector3 targetShipSpeed = (targetShip.transform.position - targetLastPosition) / Time.deltaTime;
        targetLastPosition = targetShip.transform.position;

        // Get a vector to the target ship
        Vector3 toTarget = targetShip.transform.position - shooter.transform.position;

        float a = Vector3.Dot(targetShipSpeed, targetShipSpeed) - (projectileSpeed * projectileSpeed);
        float b = 2 * Vector3.Dot(targetShipSpeed, toTarget);
        float c = Vector3.Dot(toTarget, toTarget);

        float p = -b / (2 * a);
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

    /// <summary>
    /// Fire a ray every second
    /// If ray hits something then keep turning until it no longer hits anything
    /// If not, just change the turn direction every second for randomness
    /// </summary>
    public void CollisionAvoidance()
    {
        if (AIState != AIStates.DISEMBARKING /*&& AIState != AIStates.DODGING*/ &&
            Time.time >= rayScanTimer)
        {
            RaycastHit hitInfo1;
            RaycastHit hitInfo2;
            RaycastHit hitInfo3;
            RaycastHit hitInfo4;
            bool rayHasHit = false;
            

            //Ray Up
            if (StandardRayCast.RayCastCollisionAvoidance(transform.position, transform.forward + transform.TransformDirection(new Vector3(0, rayCastOffset, 0)), 5 * maxSpeed, ignoreCollisionLayer, out hitInfo1)) // Up cast, so dodge down
                rayHasHit = true;           

            ////Ray Down
            if (StandardRayCast.RayCastCollisionAvoidance(transform.position, transform.forward + transform.TransformDirection(new Vector3(0, -rayCastOffset, 0)), 5 * maxSpeed, ignoreCollisionLayer, out hitInfo2)) // Down cast, so dodge up
                rayHasHit = true;            

            ////Ray Right
            if (StandardRayCast.RayCastCollisionAvoidance(transform.position, transform.forward + transform.TransformDirection(new Vector3(rayCastOffset, 0, 0)), 5 * maxSpeed, ignoreCollisionLayer, out hitInfo3)) // left cast, so dodge right
                rayHasHit = true;          

            // Ray Left
            if (StandardRayCast.RayCastCollisionAvoidance(transform.position, transform.forward + transform.TransformDirection(new Vector3(-rayCastOffset, 0, 0)), 5 * maxSpeed, ignoreCollisionLayer, out hitInfo4)) // right cast, so dodge left
                rayHasHit = true;
        

            if (rayHasHit)
            {
                rayHasHit = false;
                //Debug.Log("Point has hit here in the world: " + hitInfo1.point);
                //Debug.Log("Objecct Hit: " + hitInfo1.collider.gameObject.name);
                //Debug.Log("Point distance from the ship: " + Vector3.Distance(transform.position, hitInfo1.point));
                // Find shortest distance, that will be the closest part of the object to the user, so dodge in the opposite direction
                int result1 = (int)Vector3.Distance(transform.position, hitInfo1.point);
                int result2 = (int)Vector3.Distance(transform.position, hitInfo2.point);
                int result3 = (int)Vector3.Distance(transform.position, hitInfo3.point);
                int result4 = (int)Vector3.Distance(transform.position, hitInfo4.point);

                string minName = "up";

                int min1 = (int)Mathf.Min(result1, result2);
                if(min1 == result2)
                    minName = "Down";

                int min2 = (int)Mathf.Min(result3, result4);
                if (min2 == result4)
                    minName = "Left";

                int min =  (int)Mathf.Min(min1, min2);
                if (min == min2)
                    minName = minName == "Left" ? "Left" : "Right";

                switch (minName)
                {
                    case "Up":
                        myRigidBody.AddRelativeTorque(turnSpeed, 0, 0); // down
                        break;
                    case "Down":
                        myRigidBody.AddRelativeTorque(-turnSpeed, 0, 0); // up
                        break;
                    case "Right":
                        myRigidBody.AddRelativeTorque(0, -turnSpeed, 0); // Left
                        break;
                    case "Left":
                        myRigidBody.AddRelativeTorque(0, turnSpeed, 0); // Right
                        break;
                    default:
                        break;
                }
                weapons.SetCanFire(false);
                //Debug.Log(minName + " is the closest hit");
                SetDodgeState();
                
            }
            rayScanTimer = Time.time + rayScanTime;
        }
    }

    private void SetDodgeState()
    {
        // Don't set previous state to dodging or the ship will get stuck dodging for the rest of its life
        SetThrottleSpeed( maxSpeed / 2);
        if (AIState != AIStates.DODGING)
            AIPreviousState = AIState;
        AIState = AIStates.DODGING;
        dodgeTimer = Time.time + dodgeTime;
    }

    public void SetMaxSpeed(int speed) => maxSpeed = speed;

    public void SetAllowTargetChange(bool value) => allowTargetChange = value;
}
