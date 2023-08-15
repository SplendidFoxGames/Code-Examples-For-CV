using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerWeaponSystem : MonoBehaviour {

    public float weaponRegenerateRate = 0.1f;
    private float weaponRegenTimer = 1.0f;      // Regen every 1 second
    private float timer = 0;

    // These 4 are for turning off when the game gets paused
    // Access to all types of weapons
    private PlayerGunFire gun;
    [SerializeField]
    private float totalWeaponPower = 400;
    private float currentWeaponPower;

    private bool disembarking = false; // So cant fire when leaving a carrier
    //private bool isPaused = false;
    private bool isFiring = false;
    public bool IsFiring
    { 
        get { return isFiring; }
        set { isFiring = value; }
    }
    

    void Start()
    {
        currentWeaponPower = totalWeaponPower;
        gun = GetComponent<PlayerGunFire>();
    }

    // Update is called once per frame
    void Update ()
    {
        if(!disembarking)
        {
            if (isFiring)
            {
                gun.FireWeapon();
            }
            RegenerateWeaponPower();        
        }
    }
    public void TestFire()
    {
        gun.FireWeapon();
    }
    // Called each time a gun fires
    public bool DrainWeaponPower(float power)
    {
        currentWeaponPower -= power;
        if(currentWeaponPower <0)
        {
            currentWeaponPower = 0;
            Debug.Log("Weapons out of power");
            return false;
        }
        else
        {
            return true;
        }
    }

    // Regenerate weapon power over time, baseed on the totalWeaponPower
    private void RegenerateWeaponPower()
    {
        if(Time.time > timer)
        {
            currentWeaponPower += weaponRegenerateRate;
            currentWeaponPower = Mathf.Clamp(currentWeaponPower, 0, totalWeaponPower);
            timer = Time.time + weaponRegenTimer;
        }
    }

    // For the HUD indicator of current weapon power
    public float ReturnCurrentWeaponPower()
    {
        return currentWeaponPower;
    }

    public void IncreaseTotalWeaponPower(float power)
    {
        totalWeaponPower += power;
        totalWeaponPower = Mathf.Clamp(totalWeaponPower, 0, 1200);

        currentWeaponPower += power;
        currentWeaponPower = Mathf.Clamp(currentWeaponPower, 0, 1200);
    }

    public float DecreaseTotalWeaponPower(float power)
    {
        // If you have the power comply with the full amount
        if(totalWeaponPower > power)
        {
            totalWeaponPower -= power;
            totalWeaponPower = Mathf.Clamp(totalWeaponPower, 0, 1200);
            return power;
        }
        else // Comply with what power you have left
        {
            float temp = totalWeaponPower;
            totalWeaponPower -= power;
            totalWeaponPower = Mathf.Clamp(totalWeaponPower, 0, 1200);
            return temp;
        }
    }

    // For the power distribution system
    public float ReturnTotalWeaponPower()
    {
        return totalWeaponPower;
    }

    public void SetDisembarking(bool value)
    {
        disembarking = value;
    }
}
