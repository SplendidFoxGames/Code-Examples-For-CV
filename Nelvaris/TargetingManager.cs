using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Allows the player to target any ship in the level
// Player will query LM for whatever ship type it wants to target
// LM will find it and return it

public static class TargetingManager
{
    [Tooltip("If in a nebula for example")]
    public static float targetDistanceLimit = 99999f;
    public static bool useTargetDistanceLimit = false;

    // These 2 are set each time a ship scans for a target, based on their own limit
    private static bool usePerShipDistanceLimit = false;
    private static float perShipDistanceLimit = 99999f;

    public static List<AllShipBase> allTargetables = new List<AllShipBase>();

    public static List<AllShipBase> newNelvariShips = new List<AllShipBase>();
    public static List<AllShipBase> newNelvariCapitalShips = new List<AllShipBase>();
    public static List<AllShipBase> newNelvariTransportShips = new List<AllShipBase>();

    public static List<AllShipBase> newHumanShips = new List<AllShipBase>();
    public static List<AllShipBase> newHumanEscortShips = new List<AllShipBase>();

    private static PlayerShip thePlayer;

    private static int closestHumanTargetCounter = -1;
    private static int closestNelvariTargetCounter = -1;
    private static int closestLegotianTargetCounter = -1;
    private static int closestShipTargetCounter = -1;  // Any ship
    private static int closestAllTargetables = -1;

    private static int aiShipCounter = -1;


    /// <summary>
    /// Find ANY target
    /// Should include special cases like asteroids and other objects.
    /// </summary>
    /// <param name="other"></param>
    /// <returns></returns>
    public static GameObject NextClosestTargetAny(GameObject other)
    {
        if (other.GetComponent<PlayerShip>())
            return FindNextClosestTarget(other, allTargetables, ref closestShipTargetCounter);
        else
            return FindNextClosestTarget(other, allTargetables, ref aiShipCounter);
    }

    /// <summary>
    /// Find ANY Human Ship
    /// </summary>
    public static GameObject NextClosestHumanShip(GameObject other)
    {
        if (other.GetComponent<PlayerShip>())
            return FindNextClosestTarget(other, newHumanShips, ref closestHumanTargetCounter);
        else
            return FindNextClosestTarget(other, newHumanShips, ref aiShipCounter);
    }

    /// <summary>
    /// Find only the closest Human ship and don't cycle
    /// </summary>
    public static GameObject ClosestHumanShip(GameObject other)
    {
        if(newHumanShips.Count > 0)
        {
            //SortShipListByProximity(other, newHumanShips);
            AllShipBase[] result = (from s in newHumanShips orderby (other.transform.position - s.transform.position).sqrMagnitude select s).ToArray();
            // Don't target if ship is further away than the limit
            float tempDistance1 = Vector3.Distance(result[0].transform.position, other.transform.position);

            if 
                (useTargetDistanceLimit && tempDistance1 >= targetDistanceLimit) { return null; }
            else
                return result[0].gameObject;
        }
        else
        {
            return null;
        }        
    }

    /// <summary>
    ///  Carriers, cruisers and transports (all huge ships)
    /// Other is the ship doing the targeting
    /// </summary>
    public static GameObject NextClosestHumanCapital(GameObject other)
    {
        if (newHumanEscortShips.Count > 0)
        {
            if (closestHumanTargetCounter >= newHumanEscortShips.Count - 1)
            {
                closestHumanTargetCounter = -1;
            }
            closestHumanTargetCounter++;
            return newHumanEscortShips[closestHumanTargetCounter].gameObject;
        }
        else
        {
            return null;
        }
    }



    ///**********************************************************************************************///
    ///********************************* Find Nelvari Ships *****************************************///
    ///**********************************************************************************************///

    /// <summary>
    /// Find ANY Nelvari ship
    /// </summary>
    public static GameObject NextClosestNelvariShip(GameObject other, bool usePershipDistance = false, float perShipDistance = 99999f)
    {
        usePerShipDistanceLimit = usePershipDistance;
        perShipDistanceLimit = perShipDistance;

        if (other.GetComponent<PlayerShip>())
            return FindNextClosestTarget(other, newNelvariShips, ref closestNelvariTargetCounter);
        else
            return FindNextClosestTarget(other, newNelvariShips, ref aiShipCounter);
    }

    /// <summary>
    /// Transport only ships
    /// </summary>
    public static GameObject NextClosestNelvariTransport(GameObject other)
    {
        if (other.GetComponent<PlayerShip>())
            return FindNextClosestTarget(other, newNelvariTransportShips, ref closestShipTargetCounter);
        else
            return FindNextClosestTarget(other, newNelvariTransportShips, ref aiShipCounter);
    }

    public static GameObject NextClosestHumanTransport(GameObject other, bool usePershipDistance = false, float perShipDistance = 99999f)
    {
        return null;   
    }

    /// <summary>
    /// Find ships you are meant to be escorting // Carriers, cruisers and transports (all huge ships)
    /// Other is the ship doing the targeting
    /// </summary>
    public static GameObject NextClosestNelvariCapital(GameObject other)
    {
        //if(isPlayer == true)
        if(other.GetComponent<PlayerShip>())
            return FindNextClosestTarget(other, newNelvariCapitalShips, ref closestShipTargetCounter);
        else
            return FindNextClosestTarget(other, newNelvariCapitalShips, ref aiShipCounter);
    }

    ///General
    public static GameObject FindNextClosestTarget(GameObject other, List<AllShipBase> ships, ref int shipTargetCounter)
    {
        if (ships.Count > 0)
        {
            if (shipTargetCounter >= ships.Count - 1)
            {
                shipTargetCounter = -1;
            }
            shipTargetCounter++;

            AllShipBase[] result = (from s in ships orderby (other.transform.position - s.transform.position).sqrMagnitude select s).ToArray();
            
            // First test the GLOBAL target distance (for example can't scan far in thick nebula
            float tempDistance1 = Vector3.Distance(result[shipTargetCounter].transform.position, other.transform.position);
            if (useTargetDistanceLimit && tempDistance1 >= targetDistanceLimit) { shipTargetCounter = -1; return null; } // Reset the ship counter, so the next AI to look starts at the beginning

            // Next test the PER SHIP target distance (ships can set their own distance for mission reasons, i.e. only look for targets in the immediate area you spawn into)
            if (usePerShipDistanceLimit && tempDistance1 >= perShipDistanceLimit) { shipTargetCounter = -1; return null; }

            // If the found ship is the player ship, just go to the next one in the list
            if (other == result[shipTargetCounter].gameObject)
            {
                shipTargetCounter++;
                if (ships.Count > 1)
                    return result[shipTargetCounter].gameObject;
                else
                    return null;
            }                

            return result[shipTargetCounter].gameObject;
        }
        else
        {
            return null;
        }
    }

    // Called by all ships when they first arrive in scene
    public static void AddShipToList(AllShipBase theShip)
    {
        AllShipBase.ShipClass temp = theShip.GetShipClass();

        // If a Human escortable ship (or large ship to monitor) add to list
        if (temp == AllShipBase.ShipClass.HUMAN_TRANSPORT ||
           temp == AllShipBase.ShipClass.HUMAN_CARRIER ||
           temp == AllShipBase.ShipClass.HUMAN_CRUISER ||
           temp == AllShipBase.ShipClass.HUMAN_DESTROYER ||
           temp == AllShipBase.ShipClass.HUMAN_SUPER_DESTROYER)
            newHumanEscortShips.Add(theShip);

        // If a Nelvari escortable ship (or large ship to monitor) add to list
        if (temp == AllShipBase.ShipClass.NELVARI_TRANSPORT ||
            temp == AllShipBase.ShipClass.NELVARI_CARRIER ||
            temp == AllShipBase.ShipClass.NELVARI_CRUISER ||
            temp == AllShipBase.ShipClass.NELVARI_DESTROYER ||
            temp == AllShipBase.ShipClass.NELVARI_SUPER_DESTROYER ||
            temp == AllShipBase.ShipClass.NELVARI_STATION)
            newNelvariCapitalShips.Add(theShip);

        // Just transport ships
        if (temp == AllShipBase.ShipClass.NELVARI_TRANSPORT)
            newNelvariTransportShips.Add(theShip);

        // ADD ONE FOR HUMAN TRANSPORT IF NEEDED

        // All human ships
        if (theShip.GetSpecies() == AllShipBase.Species.HUMAN)
            newHumanShips.Add(theShip);

        // All Nelvari ships
        if (theShip.GetSpecies() == AllShipBase.Species.NELVARI)
            newNelvariShips.Add(theShip);

            // All ships go to the all targetable list
            allTargetables.Add(theShip);

        PrintShipCount();
    }

    // Called by all ships when they explode
    public static void RemoveShipFromList(AllShipBase theShip)
    {
        //AllShipBase sToRemove = null;
        // Find the list(s) the ship is in and remove
        if (theShip.GetSpecies() == AllShipBase.Species.HUMAN)
        {
            // All human ships
            newHumanShips.Remove(theShip);
            newHumanEscortShips.Remove(theShip);
        }

        if (theShip.GetSpecies() == AllShipBase.Species.NELVARI)
        {
            // All ships list
            newNelvariShips.Remove(theShip);
            // Escort ships list
            newNelvariCapitalShips.Remove(theShip);
            // Transport Ships
            newNelvariTransportShips.Remove(theShip);
            // Capital Ships
            newNelvariCapitalShips.Remove(theShip);
        }
        allTargetables.Remove(theShip);
    }
    
    public static void WarpAllNelvariShipsOut()
    {
        foreach (AllShipBase nelvari in newNelvariShips)
        {
            if (nelvari.GetComponent<PlayerShip>())
                return;
            nelvari.GetComponent<AIWarpDrive>().SetWarpingOut();
        }
    }

    /// <summary>
    /// Sets the limit that all ships can scan for in the scene
    /// </summary>
    /// <param name="limit"></param>
    public static void SetTargetDistanceLimit(float limit)
    {
        useTargetDistanceLimit = true;
        targetDistanceLimit = limit;
    }

    public static int GetEnemyShipCount()
    {
        return newHumanShips.Count;
    }

    /// <summary>
    /// DEBUG
    /// </summary>
    public static void PrintShipCount()
    {
        //Debug.Log(allTargetables.Count + " ships in play");
    }

    public static PlayerShip GetThePlayer()
    {
        if (thePlayer == null)
        {
            thePlayer = (from s in newNelvariShips where s == s.GetComponent<PlayerShip>() select s.GetComponent<PlayerShip>()).SingleOrDefault();
            return thePlayer;
        }
        else
            return thePlayer;
    }

    public static void ClearAllLists()
    {
        allTargetables.Clear();
        newNelvariShips.Clear();
        newNelvariCapitalShips.Clear();
        newNelvariTransportShips.Clear();
        newHumanShips.Clear();
        newHumanEscortShips.Clear();

    }
}