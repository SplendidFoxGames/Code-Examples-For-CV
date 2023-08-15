using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;

public class UIEnemyLocator : MonoBehaviour
{
    private float scanTimer = 0;
    private float scanTime = 0.05f;
    private float closestEnemyDist = 999.0f;
    private GameObject thePlayer;

    [SerializeField] private Image[] displayedSprites;
    [SerializeField] private Image displayedArrowPointer;

    [SerializeField] private Sprite[] numberSprites;
    [SerializeField] private Sprite[] pointers;
    [SerializeField] private TextMeshProUGUI uiDistText;

    private List<GameObject> enemyList = new List<GameObject>();
    float m_Angle;
    float m_cross;
    // Start is called before the first frame update
    void Start()
    {
        scanTimer = scanTime;
    }

    // Update is called once per frame
    void Update()
    {
        if (thePlayer == null)
            return;

        Vector2 tempVectorPosition = Vector2.zero;
        FindClosestEnemy(tempVectorPosition);
        PointMarkerToClosestEnemy(tempVectorPosition);
    }

    public void FindClosestEnemy(Vector2 tempVectorPosition)
    {
        GameObject boss = (from e in enemyList select e).FirstOrDefault(e => e.GetComponent<BossHealthController>());
        if (boss != null)
        {
            closestEnemyDist = Vector2.Distance(thePlayer.transform.position, boss.transform.position);
            tempVectorPosition = boss.transform.position;
        }
        else
        {
            // Find the closest transform to the player Then calculate the distance to that transform
            Transform subset = (from g in enemyList orderby (thePlayer.transform.position - g.transform.position).sqrMagnitude select g).First().transform;
            tempVectorPosition = subset.position;
            closestEnemyDist = Vector2.Distance(thePlayer.transform.position, subset.position);
        }

        // Split out the numbers into individual strings
        string[] tmpResult = closestEnemyDist.ToString("0:0:0").Split(':');
        // Parse each string to an int, and use that int to select the correct sprite from the array
        int parseNum = 0;
        for (int i = 0; i < tmpResult.Length; i++)
        {
            int.TryParse(tmpResult[i], out parseNum);
            displayedSprites[i].sprite = numberSprites[parseNum];
        }
        closestEnemyDist = 999;
    }

    public void PointMarkerToClosestEnemy(Vector2 tempVectorPosition)
    {
        float tempY, tempLeftX = 0;

        tempY = Mathf.Abs(tempVectorPosition.y - thePlayer.transform.position.y);
        tempLeftX = Mathf.Abs(tempVectorPosition.x - thePlayer.transform.position.x);

        // it points to the direction of the nearest enemy, and if the enemy is at a diagonal,
        // it points to whichever direction it is furthest away
        // for example, the enemy is 2 squares above but 6 squares to the right, it will point to the right

        if (tempY > tempLeftX)
        {
            if (tempVectorPosition.y > thePlayer.transform.position.y)
                displayedArrowPointer.sprite = pointers[0];
            else if (tempVectorPosition.y < thePlayer.transform.position.y)
                displayedArrowPointer.sprite = pointers[2];
        }
        else
        {
            if (tempVectorPosition.x > thePlayer.transform.position.x)
                displayedArrowPointer.sprite = pointers[1];
            else if (tempVectorPosition.x < thePlayer.transform.position.x)
                displayedArrowPointer.sprite = pointers[3];

        }
    }

    public void AddEnemyToList(GameObject enemy) => enemyList.Add(enemy);
    public void RemoveEnemyFromList(GameObject enemy) => enemyList.Remove(enemy);
    public void AddThePlayer(GameObject player) => thePlayer = player;
    public void RemoveThePlayer() => thePlayer = null;
}
