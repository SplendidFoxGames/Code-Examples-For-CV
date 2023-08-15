using UnityEngine;

public abstract class AllShipBase : MonoBehaviour
{

    protected enum Throttle { Zero, OneThird, TwoThird, Max, MatchTarget, Free }
    public enum Species { HUMAN, NELVARI, LEGOTIAN, ZANGY, OTHER};     // From player's perspective. Determines what colour the UI targets should be. 
    public enum ShipClass
    {
        HUMAN_FIGHTER, HUMAN_BOMBER, HUMAN_CORVETTE, HUMAN_FRIGATE, HUMAN_TRANSPORT, HUMAN_CRUISER,
        HUMAN_DESTROYER, HUMAN_CARRIER, HUMAN_SUPER_DESTROYER, HUMAN_CAPITAL_TURRET, HUMAN_SENTRYGUN, HUMAN_CARGO, HUMAN_STATION, HUMAN_CAPITAL_HULL_COMP,

        NELVARI_FIGHTER, NELVARI_BOMBER, NELVARI_CORVETTE, NELVARI_FRIGATE, NELVARI_TRANSPORT, NELVARI_CRUISER,
        NELVARI_DESTROYER, NELVARI_CARRIER, NELVARI_SUPER_DESTROYER, NELVARI_CAPITAL_TURRET, NELVARI_SENTRYGUN, NELVARI_CARGO, NELVARI_STATION, NELVARI_CAPITAL_HULL_COMP,

        LEGOTIAN_FIGHTER, LEGOTIAN_BOMBER, LEGOTIAN_CORVETTE, LEGOTIAN_FRIGATE, LEGOTIAN_TRANSPORT, LEGOTIAN_CRUISER,
        LEGOTIAN_DESTROYER, LEGOTIAN_CARRIER, LEGOTIAN_SUPER_DESTROYER, LEGOTIAN_CAPITAL_TURRET, LEGOTIAN_SENTRYGUN, LEGOTIAN_CARGO, LEGOTIAN_STATION, LEGOTIAN_CAPITAL_HULL_COMP,

        ZANGY_MALE, ZANGY_FEMALE, ZANGY_BABY, ZANGY_LARVE,

        NELVARI_TUTORIAL_FIGHTER_DRONE,

        ASTEROID,
    };    
    
    [SerializeField] protected GameObject theCurrentTarget;
    [SerializeField] protected SubComponent theCurrentSubComponent;     // For capital ship sub components, shields, engines, weapons, and individual guns
    protected float distanceToTarget;
    protected bool matchTargetSpeed = false;

    [SerializeField] protected Species mySpecies;
    [SerializeField] protected ShipClass myShipClass;
    [SerializeField] protected string shipName;
    [SerializeField] protected string subClassName;  // The ship is a Crusier, but what kind of Crusier? Exploration Cruiser for example
        
    [SerializeField] protected float accelerateSpeed = 900;
    [SerializeField] protected float decelerateSpeed = 900;

    [SerializeField] protected float shipThrottleSpeed = 900;   // This is the speed that this ship is accelerating/decelerating towards
    [SerializeField] protected float shipCurrentSpeed = 900;        // Speed based on Acceleration Speed or Deceleration Speed * Time.deltatime
    [SerializeField] protected float maxSpeed = 900;
    [SerializeField] protected float minSpeed = 900;
    [SerializeField] protected float shipRotationSpeed = 900;
    [SerializeField] protected float shipStrafeSpeed = 50;

    protected float collisionTime = 2f;
    protected float collisionTimer = 0f;
    [SerializeField] protected bool warpArrivalComplete;

    protected Rigidbody myRigidBody;
    protected float speedMultiplier = 0;            // The speed gets multiplied by the mass. So that 80 speed for a massive ship moves the same speed as 80 for a tiny ship
    protected ShipHullStrengthBase hullStrength;
    protected bool isDead = false;

    protected bool enginesDisabled = false;
    protected bool weaponsDisabled = false;
    protected bool shieldsDisabled = false;

    protected SubComponent[] subComponents;


    // Start is called before the first frame update
    protected void Start()
    {
        myRigidBody = GetComponent<Rigidbody>();
        if(myRigidBody)
            speedMultiplier = myRigidBody.mass;
        hullStrength = GetComponentInChildren<ShipHullStrengthBase>();
        
        subComponents = GetComponentsInChildren<SubComponent>();

        //AddShipToTargetList();
    }

    public float GetCurrentSpeed()
    {
        return shipCurrentSpeed;
    }

    public Species GetSpecies()
    {
        return mySpecies;
    }

    public ShipClass GetShipClass()
    {
        return myShipClass;
    }

    public string GetShipName()
    {
        return shipName;
    }
    
    public string GetSubClassName()
    {
        return subClassName;
    }

    public int GetShipHullStrength()
    {
        return hullStrength.GetHullStrength();
    }

    public float GetShipSpeed()
    {
        return shipCurrentSpeed;
    }

    protected void AccelerateDecelerate()
    {
        if (shipThrottleSpeed > shipCurrentSpeed)    // Accel
        {
            shipCurrentSpeed += accelerateSpeed * Time.deltaTime;
            shipCurrentSpeed = Mathf.Clamp(shipCurrentSpeed, minSpeed, shipThrottleSpeed);
        }
        else if (shipThrottleSpeed < shipCurrentSpeed)    // Decel
        {
            shipCurrentSpeed -= decelerateSpeed * Time.deltaTime;
            shipCurrentSpeed = Mathf.Clamp(shipCurrentSpeed, shipThrottleSpeed, maxSpeed);
        }
    }

    protected void MatchTargetSpeed()
    {
        if (theCurrentTarget)
            SetThrottleSpeed(theCurrentTarget.GetComponent<AllShipBase>().GetShipSpeed());
    }    

    protected abstract void MoveShip();
    // Called by the player/enemy ship class to see
    // if the ship has been destroyed
    // Stops the AI from updating if it has
    public bool IsShipDead() { return isDead; }

    // Gets set by the Hull component when the ship dies.
    public void SetShipDead() { isDead = true; }
    public void SetShipName(string n) { shipName = n; }

    public void EnableEngines()
    {
        enginesDisabled = false;
    }

    public void DisableEngines()
    {
        SetThrottleSpeed(0);
        enginesDisabled = true;
    }

    public bool GetEngineStatus()
    {
        return enginesDisabled;
    }

    abstract public SubComponent[] GetSubComponents();

    public void SetThrottleSpeed(float s)
    {
        // Throttle speed can't be set lower or higher than the min or max speed values, so clamp it!
        s = Mathf.Clamp(s, minSpeed, maxSpeed);
        shipThrottleSpeed = s;
    }

    public float GetMinSpeed() => minSpeed;
    public float GetMaxSpeed() => maxSpeed;


    private void OnEnable()
    {
        TargetingManager.AddShipToList(this);
    }
    private void OnDisable()
    {
        TargetingManager.RemoveShipFromList(this);
    }

}
