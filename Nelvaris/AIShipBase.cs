using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class AIShipBase : AllShipBase
{
    [SerializeField] protected bool flyForwards = false;  // DEBUG

    public enum AIStates { IDLE, ATTACKING, ATTACKRUN, ESCORTING, DODGING, DISEMBARKING, PATROL, ESCAPING, LAUNCHING, WARPING, FOLLOWING,
                             TRAVELLING, REPAIRING, REARMING,   // These are for the repair ship
    };
    [SerializeField] protected AIStates AIState;
    protected AIStates AIPreviousState;    
    protected float targetScanTime = 2f;
    protected float targetScanTimer = 2f;

    protected WarpVectorPoint escapeVector;     // So all ships face the correct direction when leaving
    protected CommsChat commsChat;
    protected AIWeaponFire weapons;
    protected AIWarpDrive warpDrive;

    protected float theCurrentTargetMeshBounds = 0f;
    protected GameObject theCurrentTargetMesh;

    protected float aiSwapTimer = 25.0f;
    protected float aiPatrolTimer = 0.0f;

    [SerializeField] protected bool canReportStatus = true;   // Report when enemies are dead, hull low, etc

    ////Start is called before the first frame update
    //// Never gets called base its not the base class?
    protected void InitialiseAIShipBase()
    {
        //commsChat = FindObjectOfType<CommsChat>();
        weapons = GetComponentInChildren<AIWeaponFire>();        
        warpDrive = GetComponent<AIWarpDrive>();

        // If targets are set in the inspector, then this gets the mesh bounds. When this happens the AI is already in attack and will not get the mesh bounds itself
        // if the AI scans and targets the ship, it can get the mesh bounds itself
        if (theCurrentTarget)
            SetCurrentTargetMeshBounds();
    }

    //*********************************************************************************//
    //******************************* IDLE AI STATE ***********************************//
    //*********************************************************************************//
    public void SetAIStateIdle()
    {
        AIState = AIStates.IDLE;
        theCurrentTarget = null;
    }

    protected void IdleAIState()
    {
        if (theCurrentTarget == null)
        {
            distanceToTarget = 100000;
            ScanForEnemyTargets();
        }
        else
        {
            theCurrentTarget = null;    // If for whatever reason the ship is idle with a target, clear the target and get it again so you can change to correct state
        }

        SetThrottleSpeed(40);

        // After 15-20 seconds idle go back Patrolling
        aiSwapTimer -= Time.deltaTime;
        if (aiSwapTimer < -0)
        {
            AIState = AIStates.PATROL;
            aiSwapTimer = Random.Range(25.0f, 60.0f);

        }
    }

    protected abstract void FindClosestShipToAttack();
    protected abstract void ScanForEnemyTargets();  

    //*********************************************************************************//
    //**************************** ATTACKING AI STATE *********************************//
    //*********************************************************************************//
    protected abstract void AttackingAIState();
    
    ////*********************************************************************************//
    ////****************************** HELPER METHODS ***********************************//
    ////*********************************************************************************//
    protected void ReCalculateDistanceToTarget()
    {
        if (theCurrentTarget)
        {
            distanceToTarget = Vector3.Distance(theCurrentTarget.transform.position, this.transform.position);
        }
    }

    //*********************************************************************************//
    //****************************** Escape AI STATE **********************************//
    //*********************************************************************************//
    public void EscapeAIState()
    {
        if (!enginesDisabled && mySpecies != Species.ZANGY)
        {
            AIState = AIStates.ESCAPING;
            // Call the comms chat to say you are low on health and escaping
            if (!commsChat)
                commsChat = FindObjectOfType<CommsChat>();

            CommsReportStatus();
            // Change current target to an escape vector (a location above and diagonal from the ship
            theCurrentTarget = null;
            FaceEscapeVector();
            // have the ship face the escape vector (This gets done when the ship enters the Escaping AI state
            // go to warp and set "is escaping"
            Invoke("EscapeBattle", 10);
        }
        else
        {
            // Enemy does nothing when cant escape
        }
    }

    //*********************************************************************************//
    //****************************** WARP AI STATE **********************************//
    //*********************************************************************************//
    public void SetAIStateWarping()
    {
        AIState = AIStates.WARPING;
        theCurrentTarget = null;
    }

    private void EscapeBattle()
    {
        SetAIStateWarping();
        if(warpDrive)
            warpDrive.SetWarpingOut();
    }

    protected void FaceEscapeVector()
    {
        escapeVector = FindObjectOfType<WarpVectorPoint>();

        if (escapeVector)
        {
            Quaternion targetRotation = Quaternion.LookRotation(escapeVector.transform.position - transform.position);
            targetRotation *= Quaternion.FromToRotation(Vector3.forward, Vector3.left); // To broadside targets
            myRigidBody.MoveRotation(Quaternion.Slerp(transform.rotation, targetRotation, 0.1f * Time.deltaTime));
        }
    }

    protected abstract void FaceTheTarget();

    public void SetWarpStatus(bool s) => warpArrivalComplete = s;
    public WarpVectorPoint GetWarpVectorPoint() => escapeVector;

    public override SubComponent[] GetSubComponents() => subComponents;
    public AIStates GetAIState() => AIState;

    /// <summary>
    /// Stops all AI other than speed
    /// </summary>
    public void SetFlyForwards(bool value) => flyForwards = value;

    protected void SetCurrentTargetMeshBounds()
    {
        theCurrentTargetMeshBounds = theCurrentTarget.GetComponentInChildren<MeshFilter>().transform.localScale.x;

        if(weapons == null)
            weapons = GetComponentInChildren<AIWeaponFire>();
        if (weapons == null) // Some ships won't have weapons
            return;
        else
            weapons.SetTarget(theCurrentTarget.gameObject, theCurrentTargetMeshBounds);
    }

    protected void CommsReportStatus()
    {
        if (canReportStatus)
        {
            if (!commsChat)
                commsChat = FindObjectOfType<CommsChat>();

            int temp;
            temp = Random.Range(0, 10);
            if (temp >= 9 && commsChat)
            {
                if (mySpecies == Species.HUMAN)
                    commsChat.ReportEnemyEscaping(GetShipName(), gameObject);
                else
                    commsChat.ReportNelvariLowHull(GetShipName(), gameObject);
            }
        }
    }

    public void ReportHullStatus()
    {
        if (!commsChat)
            commsChat = FindObjectOfType<CommsChat>();

        commsChat.ReportShipDamaged(shipName, gameObject);
    }
}
