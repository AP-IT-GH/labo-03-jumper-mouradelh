using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

/// <summary>
/// JumperAgent - An ML-Agents implementation that learns to jump over approaching obstacles.
/// This agent uses reinforcement learning to develop a strategy for timing jumps to avoid collisions.
/// </summary>
public class JumperAgent : Agent
{
    // Core components
    private Rigidbody rb;
    private Vector3 startPos;
    private JumperEnvironment environment;
    
    // Obstacle tracking
    private GameObject closestObstacle;
    
    // Jump control variables
    private bool isGrounded = true;
    private float lastJumpTime = 0f;
    private float jumpCooldown = 0.5f; // Time required between jumps to prevent spam
    
    // Energy system to discourage constant jumping
    private float energy = 1.0f;
    private float energyRecoveryRate = 0.1f;
    private float jumpEnergyCost = 0.5f;
    
    // Tracking for consecutive jumps
    private int consecutiveJumps = 0;
    
    // Configurable parameters
    public float jumpForce = 5f;
    public Transform obstacleSpawnPoint;

    /// <summary>
    /// Initializes the agent and retrieves necessary components
    /// </summary>
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        startPos = transform.localPosition;
        environment = GetComponentInParent<JumperEnvironment>();
    }
    
    /// <summary>
    /// Handle energy recovery when grounded
    /// </summary>
    void Update()
    {
        // Recover energy over time when grounded
        if (isGrounded && energy < 1.0f)
        {
            energy += energyRecoveryRate * Time.deltaTime;
            energy = Mathf.Clamp01(energy); // Keep energy between 0 and 1
        }
    }

    /// <summary>
    /// Reset the agent state at the beginning of each episode
    /// </summary>
    public override void OnEpisodeBegin()
    {
        // Reset position and physics
        transform.localPosition = startPos;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        isGrounded = true;
        
        // Reset energy and jump tracking
        energy = 1.0f;
        consecutiveJumps = 0;
        lastJumpTime = 0f;

        // Reset the entire environment (obstacles, etc)
        environment.ResetEnvironment();
    }

    /// <summary>
    /// Collect observations from the environment for the neural network
    /// </summary>
    public override void CollectObservations(VectorSensor sensor)
    {
        // Agent's position and velocity (4 values)
        sensor.AddObservation(new Vector2(transform.localPosition.x, transform.localPosition.y));
        sensor.AddObservation(new Vector2(rb.linearVelocity.x, rb.linearVelocity.y));

        // Find the closest obstacle for decision-making
        closestObstacle = environment.GetClosestObstacle();

        // Add obstacle information if one exists (4 values)
        if (closestObstacle != null)
        {
            sensor.AddObservation(new Vector2(closestObstacle.transform.localPosition.x, closestObstacle.transform.localPosition.y));
            sensor.AddObservation(new Vector2(closestObstacle.GetComponent<Rigidbody>().linearVelocity.x, closestObstacle.GetComponent<Rigidbody>().linearVelocity.y));
        }
        else
        {
            // No obstacles - provide zero vectors as placeholders
            sensor.AddObservation(Vector2.zero);
            sensor.AddObservation(Vector2.zero);
        }
        
        // Add agent's current state information (3 values)
        sensor.AddObservation(isGrounded); // Whether agent can jump
        sensor.AddObservation(energy); // Current energy level
        sensor.AddObservation(consecutiveJumps); // Number of consecutive jumps
    }

    /// <summary>
    /// Process actions from the neural network and apply rewards/penalties
    /// </summary>
    public override void OnActionReceived(ActionBuffers actions)
    {
        int action = actions.DiscreteActions[0];

        // Calculate distance to closest obstacle for timing jumps
        float distanceToClosestObstacle = float.MaxValue;
        if (closestObstacle != null)
        {
            distanceToClosestObstacle = closestObstacle.transform.position.x - transform.position.x;
        }

        // JUMP ACTION
        if (action == 1 && isGrounded && Time.time > lastJumpTime + jumpCooldown && energy >= jumpEnergyCost)
        {
            // Apply jump force
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            isGrounded = false;
            lastJumpTime = Time.time;

            // Consume energy for jumping
            energy -= jumpEnergyCost;

            // Track consecutive jumps
            consecutiveJumps++;

            // Apply progressive penalty for consecutive jumps to discourage spam
            float consecutiveJumpPenalty = -0.1f * consecutiveJumps;
            AddReward(consecutiveJumpPenalty);
            
            // Only reward jumps that are appropriately timed relative to obstacles
            if (distanceToClosestObstacle > 0 && distanceToClosestObstacle < 3f)
            {
                // Well-timed jump - reward more for better timing
                float timingQuality = 1.0f - (Mathf.Abs(distanceToClosestObstacle - 1.5f) / 1.5f);
                float jumpReward = 0.3f * Mathf.Max(0.5f, timingQuality);
                AddReward(jumpReward);
                Debug.Log($"Well-timed jump, reward: {jumpReward:F2}");
            }
            else
            {
                // Unnecessary jump - apply penalty
                AddReward(-0.4f);
                Debug.Log("Unnecessary jump, penalty: -0.2");
            }
        }
        // NO JUMP ACTION
        else if (action == 0 && isGrounded)
        {
            // Reset consecutive jump counter when not jumping
            consecutiveJumps = 0;

            // Penalize staying still when obstacle is close and requires jumping
            if (distanceToClosestObstacle > 0 && distanceToClosestObstacle < 1.5f)
            {
                AddReward(-0.3f);
                Debug.Log("Failed to jump when needed, penalty: -0.3");
            }
        }

        // Small negative reward every step to encourage efficiency
        AddReward(-0.01f);
    }

    /// <summary>
    /// Manual control for testing the agent without using ML
    /// </summary>
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var discreteActions = actionsOut.DiscreteActions;
        discreteActions[0] = 0; // Default: do nothing

        if (Input.GetKeyDown(KeyCode.Space))
        {
            discreteActions[0] = 1; // Jump
            Debug.Log("Manual jump triggered");
        }
    }

    /// <summary>
    /// Handle collisions with obstacles and ground
    /// </summary>
    void OnCollisionEnter(Collision collision)
    {
        // Reset jump timer on any collision to prevent immediate jumps
        lastJumpTime = Time.time;
        
        if (collision.gameObject.CompareTag("Obstacle"))
        {
            // Hitting an obstacle is a failure - large negative reward
            AddReward(-6f);
            EndEpisode();
            Debug.Log("Collision with obstacle! Reward: -1.5");
        }
        else if (collision.gameObject.CompareTag("Floor"))
        {
            // Landing on the ground allows for jumping again
            isGrounded = true;
        }
    }

    /// <summary>
    /// Detect when the agent leaves the ground
    /// </summary>
    private void OnCollisionExit(Collision collision)
    {
        if (collision.gameObject.CompareTag("Floor"))
        {
            isGrounded = false;
        }
    }
}