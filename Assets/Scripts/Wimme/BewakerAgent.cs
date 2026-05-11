using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using System.Collections.Generic;
 
/// Bewaker-agent voor Heist and Seek.
/// FASE 1: leer rondlopen, muren vermijden, hele veld verkennen, deposits passeren.
/// Hooks voor fase 2 (sound triggers) en fase 4 (speler vangen) staan al klaar maar
/// zijn uitgeschakeld. 
[RequireComponent(typeof(Rigidbody))]
public class GuardAgent : Agent
{
    [Header("Movement")]
    [SerializeField] private float patrolSpeed = 2f;
    [SerializeField] private float chaseSpeed = 4f;        // gebruikt vanaf fase 4
    [SerializeField] private float rotationSpeed = 180f;
 
    [Header("Field Settings")]
    [Tooltip("Lengte/breedte van het hele speelveld in units")]
    [SerializeField] private float fieldSize = 30f;
    [Tooltip("Aantal cellen waarin het veld opgedeeld wordt voor verkenning-reward.")]
    [SerializeField] private int gridSize = 10;
    [Tooltip("Optioneel.")]
    [SerializeField] private Transform spawnPoint;
 
    [Header("Rewards - Fase 1")]
    [SerializeField] private float timeStepPenalty = -0.001f;
    [SerializeField] private float wallHitPenalty = -0.05f;
    [SerializeField] private float idlePenalty = -0.005f;
    [SerializeField] private float newCellReward = 0.05f;
    [SerializeField] private float depositVisitReward = 0.3f;
 
    [Header("Rewards: Fase 4 (nog niet activeren)")]
    [SerializeField] private bool enablePlayerDetection = false;
    [SerializeField] private float playerVisibleReward = 0.02f;   // per frame zicht op speler
    [SerializeField] private float playerCaughtReward = 5f;
 
    // ---------- Internal state ----------
    private Rigidbody rb;
    private bool[,] visitedCells;
    private HashSet<int> visitedDeposits;
    private float idleTimer;
    private Vector3 lastPosition;
    private Vector3 startPositionLocal;
    private Quaternion startRotationLocal;
 
    public override void Initialize()
    {
        rb = GetComponent<Rigidbody>();
        MaxStep = 5000;
        startPositionLocal = transform.localPosition;
        startRotationLocal = transform.localRotation;
    }
 
    public override void OnEpisodeBegin()
    {
        // Reset positie
        if (spawnPoint != null)
        {
            transform.localPosition = spawnPoint.localPosition;
            transform.localRotation = spawnPoint.localRotation;
        }
        else
        {
            transform.localPosition = startPositionLocal;
            transform.localRotation = startRotationLocal;
        }
 
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
 
        // Reset trackers
        visitedCells = new bool[gridSize, gridSize];
        visitedDeposits = new HashSet<int>();
        idleTimer = 0f;
        lastPosition = transform.position;
    }
 
    public override void CollectObservations(VectorSensor sensor)
    {

        float halfField = fieldSize / 2f;
        sensor.AddObservation(transform.localPosition.x / halfField);
        sensor.AddObservation(transform.localPosition.z / halfField);
 
        float yRot = transform.eulerAngles.y * Mathf.Deg2Rad;
        sensor.AddObservation(Mathf.Sin(yRot));
        sensor.AddObservation(Mathf.Cos(yRot));
 
    }
 
    public override void OnActionReceived(ActionBuffers actions)
    {
        float move = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
        float turn = Mathf.Clamp(actions.ContinuousActions[1], -1f, 1f);
 
        // hangt af of de speler zichtbaar is, trainigns fase 4
        float currentSpeed = (enablePlayerDetection && CanSeePlayer()) ? chaseSpeed : patrolSpeed;
 
        // Beweging via Rigidbody (respecteert fysica zoals muren)
        Vector3 moveStep = transform.forward * move * currentSpeed * Time.deltaTime;
        rb.MovePosition(rb.position + moveStep);
 
        // Rotatie
        float turnStep = turn * rotationSpeed * Time.deltaTime;
        transform.Rotate(0f, turnStep, 0f);
 
        AddReward(timeStepPenalty);
        TrackCellVisit();
        TrackIdle();
 
        if (enablePlayerDetection && CanSeePlayer())
            AddReward(playerVisibleReward);
    }
 
    /// Geeft kleine reward voor elke nieuwe gridcel die de bewaker bezoekt deze episode.
    /// Dit dwingt het hele veld te verkennen i.p.v. rondjes in 1 hoek.
    private void TrackCellVisit()
    {
        float halfField = fieldSize / 2f;
        int x = Mathf.FloorToInt((transform.localPosition.x + halfField) / fieldSize * gridSize);
        int z = Mathf.FloorToInt((transform.localPosition.z + halfField) / fieldSize * gridSize);
 
        if (x >= 0 && x < gridSize && z >= 0 && z < gridSize && !visitedCells[x, z])
        {
            visitedCells[x, z] = true;
            AddReward(newCellReward);
        }
    }
 
    /// Penalty voor langere tijd stilstaan (anti-camp behavior).
    private void TrackIdle()
    {
        float distMoved = Vector3.Distance(transform.position, lastPosition);
        if (distMoved < 0.05f)
        {
            idleTimer += Time.deltaTime;
            if (idleTimer > 1.5f)
                AddReward(idlePenalty);
        }
        else
        {
            idleTimer = 0f;
            lastPosition = transform.position;
        }
    }
 
    /// Stub voor fase 4. Simpele forward raycast.
    /// Later kan je dit vervangen door info uit de RayPerceptionSensor.
    private bool CanSeePlayer()
    {
        if (Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, 15f))
            return hit.collider.CompareTag("Player");
        return false;
    }
 
 
    private void OnCollisionStay(Collision collision)
    {
        // Continue penalty zolang bewaker tegen een muur aanduwt
        if (collision.gameObject.CompareTag("Wall"))
            AddReward(wallHitPenalty);
    }
 
    private void OnTriggerEnter(Collider other)
    {
        // Deposits = trigger colliders, geven reward eerste keer per episode
        if (other.CompareTag("Deposit"))
        {
            int id = other.GetInstanceID();
            if (!visitedDeposits.Contains(id))
            {
                visitedDeposits.Add(id);
                AddReward(depositVisitReward);
            }
        }
 
        // Fase 4: speler aanraken
        if (enablePlayerDetection && other.CompareTag("Player"))
        {
            AddReward(playerCaughtReward);
            EndEpisode();
        }
    }
 
    /// Manuele besturing voor testen (Behavior Type = Heuristic Only).
    /// W/S = vooruit/achteruit, A/D = draaien.
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuous = actionsOut.ContinuousActions;
        continuous[0] = Input.GetAxis("Vertical");
        continuous[1] = Input.GetAxis("Horizontal");
    }
}