using System.Collections.Generic;
using UnityEngine;

public class JumperEnvironment : MonoBehaviour
{
    public GameObject obstaclePrefab;
    public Transform obstacleSpawnPoint;
    public float minSpeed = 2f; // Reduced from 5
    public float maxSpeed = 5f; // Reduced from 10
    public float spawnInterval = 3f; // Increased from 2

    private List<GameObject> obstacles = new();
    private float spawnTimer;
    private float currentSpeed;
    private List<GameObject> obstaclesToReward = new(); // Track obstacles that haven't been dodged yet

    void Start()
    {
        currentSpeed = Random.Range(minSpeed, maxSpeed);
        spawnTimer = 0f;
    }

    public void ResetEnvironment()
    {
        foreach (var obs in obstacles)
        {
            if (obs != null) Destroy(obs);
        }
        obstacles.Clear();
        obstaclesToReward.Clear();

        spawnTimer = 0f;
        currentSpeed = Random.Range(minSpeed, maxSpeed);

        SpawnObstacle();
    }

    void Update()
    {
        // Increment the spawn timer
        spawnTimer += Time.deltaTime;
        if (spawnTimer >= spawnInterval)
        {
            SpawnObstacle();
            spawnTimer = 0f;
        }

        var agent = GetComponentInChildren<JumperAgent>();
        if (agent != null)
        {
            float agentX = agent.transform.position.x;

            // Check for obstacles that have passed the agent
            for (int i = obstacles.Count - 1; i >= 0; i--)
            {
                var obs = obstacles[i];
                if (obs == null) continue;

                float obsX = obs.transform.position.x;
            
                // If the obstacle has moved far enough left, destroy it
                if (obsX < -12f)
                {
                    if (obstaclesToReward.Contains(obs))
                    {
                        obstaclesToReward.Remove(obs);
                    }
                    Destroy(obs);
                    obstacles.RemoveAt(i);
                    continue;
                }

                // If the obstacle has passed the agent and hasn't been rewarded yet
                if (obsX < agentX && obstaclesToReward.Contains(obs))
                {
                    agent.AddReward(3.0f); // Reward for dodging an obstacle
                    Debug.Log("Agent dodged an obstacle! Reward: 1");
                    obstaclesToReward.Remove(obs); // Remove from reward list
                }
            }

            // Survival reward
            agent.AddReward(0.01f);
        }
    }
    
    void SpawnObstacle()
    {
        currentSpeed = Random.Range(minSpeed, maxSpeed);
        GameObject newObstacle = Instantiate(obstaclePrefab, obstacleSpawnPoint.position, Quaternion.identity);
        newObstacle.transform.SetParent(transform);
        newObstacle.GetComponent<Rigidbody>().linearVelocity = new Vector3(-currentSpeed, 0, 0);
        obstacles.Add(newObstacle);
        obstaclesToReward.Add(newObstacle); // Add to reward list
    }

    public GameObject GetClosestObstacle()
    {
        GameObject closest = null;
        float minDist = float.MaxValue;
        Vector3 agentPos = transform.Find("Agent").position;

        foreach (var obs in obstacles)
        {
            if (obs == null) continue;
            float dist = Vector3.Distance(agentPos, obs.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                closest = obs;
            }
        }
        return closest;
    }
}