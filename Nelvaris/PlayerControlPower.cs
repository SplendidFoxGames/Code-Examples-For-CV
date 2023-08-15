using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PlayerControlPower : MonoBehaviour {

    public float totalSystemPower = 1200;   // 1200 default

    private Slider playerWeaponPowerSlider; // UI
    private PlayerWeaponSystem playerWeapons;

    private Shield playerShield;
    private Slider playerShieldPowerSlider; // UI

    private PlayerShip playerEngine; 
    private Slider playerEnginePowerSlider;  // UI

    private Slider[] allSliders;

    private bool exitingLevel = false;      // When player completes objectives and exits level

    // Use this for initialization
    void Start ()
    {
        allSliders = FindObjectOfType<UIPowerSliderSystem>().returnPowerSliders();
        playerWeaponPowerSlider = allSliders[0];
        playerShieldPowerSlider = allSliders[1];
        playerEnginePowerSlider = allSliders[2];

        playerWeapons = GetComponentInChildren<PlayerWeaponSystem>();
        playerShield = GetComponentInChildren<Shield>();
        playerEngine = GetComponent<PlayerShip>();

    }

    // Update is called once per frame
    void Update()
    {        
        if(!exitingLevel)
        {
            UpdateThePowerSliders();
        }
    }

    /// <summary>
    /// Increase functions
    /// </summary>
    public void IncreaseWP()
    {
        float increaseValue = 0;
        increaseValue += DecreaseShieldPower(100);
        increaseValue += DecreaseEnginePower(100);

        IncreaseWeaponPower(increaseValue);
    }
    public void DecreaseWP()
    {
        if (playerWeapons.ReturnTotalWeaponPower() > 0)
        {
            float decreaseValue = 0;
            decreaseValue += DecreaseWeaponPower(200);

            IncreaseShieldPower(decreaseValue / 2);
            IncreaseEnginePower(decreaseValue / 2);
        }
    }
    public void IncreaseSP()
    {
        if (playerShield.ReturnTotalShieldStrength() < 1200)
        {

            if (playerWeapons.ReturnTotalWeaponPower() >= 100)
            {
                IncreaseShieldPower(100);
                DecreaseWeaponPower(100);
            }
            if (playerEngine.ReturnTotalEnginePower() >= 100)
            {
                IncreaseShieldPower(100);
                DecreaseEnginePower(100);
            }
        }
    }
    public void DecreaseSP()
    {
        if (playerShield.ReturnTotalShieldStrength() > 0)
        {
            float temp;
            temp = DecreaseShieldPower(200);

            IncreaseWeaponPower(temp / 2);
            IncreaseEnginePower(temp / 2);
        }
    }
    public void IncreaseEP()
    {
        if (playerEngine.ReturnTotalEnginePower() < 1200)
        {

            if (playerShield.ReturnTotalShieldStrength() >= 100)
            {
                IncreaseEnginePower(100);
                DecreaseShieldPower(100);
            }
            if (playerWeapons.ReturnTotalWeaponPower() >= 100)
            {
                IncreaseEnginePower(100);
                DecreaseWeaponPower(100);
            }
        }
    }
    public void DecreaseEP()
    {
        if (playerEngine.ReturnTotalEnginePower() > 0)
        {
            DecreaseEnginePower(200);

            IncreaseShieldPower(100);
            IncreaseWeaponPower(100);
        }
    }

    // Weapon Power
    private void IncreaseWeaponPower(float power)  { playerWeapons.IncreaseTotalWeaponPower(power); }
    private float DecreaseWeaponPower(float power) { return playerWeapons.DecreaseTotalWeaponPower(power); }

    // Shield Power
    private void IncreaseShieldPower(float power)  { playerShield.IncreaseTotalShieldPower(power); }
    private float DecreaseShieldPower(float power) { return playerShield.DecreaseTotalShieldPower(power); }

    // Engine Power
    private void IncreaseEnginePower(float power)  { playerEngine.IncreaseTotalEnginePower(power); }
    private float DecreaseEnginePower(float power) { return playerEngine.DecreaseTotalEnginePower(power); }

    private void UpdateThePowerSliders()
    {
        playerWeaponPowerSlider.value = playerWeapons.ReturnTotalWeaponPower();
        playerShieldPowerSlider.value = playerShield.ReturnTotalShieldStrength();
        playerEnginePowerSlider.value = playerEngine.ReturnTotalEnginePower();
    }

    public void ExitingLevelStopControls()
    {
        exitingLevel = true;
    }
}
