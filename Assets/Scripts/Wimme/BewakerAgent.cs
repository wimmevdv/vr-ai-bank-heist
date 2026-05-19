using UnityEngine;
using UnityEngine.InputSystem;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using System.Collections.Generic;

/// <summary>
/// Bewaker-agent voor Heist and Seek.
/// FASE 1: leer rondlopen, deposits ontdekken en bewaken.
///
/// ANTI-CAMPING: patrouille-reward vereist roteren tussen deposits.
/// LoS-reward alleen tijdens beweging (geen reward voor staren).
/// PARALLEL: slechts 1 agent toont overlay om het scherm leesbaar te houden.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class GuardAgent : Agent
{
    [Header("Movement")]
    [SerializeField] private float patrolSpeed = 4f;
    [SerializeField] private float chaseSpeed = 6f;
    [SerializeField] private float rotationSpeed = 240f;

    [Header("Field Settings")]
    [SerializeField] private float fieldSize = 30f;
    [SerializeField] private int gridSize = 10;

    [Header("Spawn")]
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private bool randomizeSpawnRotation = true;
    [SerializeField] private float spawnPositionJitter = 0.5f;

    [Header("Episode")]
    [SerializeField] private int maxStepsPerEpisode = 2000;

    [Header("Rewards - Penalties")]
    [SerializeField] private float timeStepPenalty = -0.0002f;
    [SerializeField] private float wallHitPenalty = -0.02f;
    [SerializeField] private float idlePenalty = -0.005f;

    [Header("Rewards - Exploration")]
    [SerializeField] private float newCellReward = 0.15f;
    [SerializeField] private float newRoomReward = 0.5f;

    [Header("Rewards - Deposits")]
    [SerializeField] private float firstDepositVisitReward = 1.0f;
    [SerializeField] private float depositPatrolReward = 0.5f;
    [Tooltip("Cooldown per deposit (sec). Wordt sowieso afgedwongen.")]
    [SerializeField] private float depositCooldown = 15f;
    [SerializeField] private float allDepositsFoundBonus = 2.0f;

    [Header("Anti-Camping - Rotation Queue")]
    [Tooltip("Aantal ANDERE deposits dat de bewaker moet aandoen voor dezelfde weer telt. 0 = uit, 2 = moet 2 andere bezoeken eerst.")]
    [SerializeField] private int patrolRotationSize = 2;
    [Tooltip("Penalty voor opnieuw triggeren van een deposit in rotation queue. 0 = geen straf, alleen geen reward.")]
    [SerializeField] private float campingPenalty = -0.05f;

    [Header("Deposit Line-of-Sight Reward")]
    [SerializeField] private bool enableDepositVisionReward = true;
    [Tooltip("Reward per frame als deposit in zicht ÉN bewaker beweegt.")]
    [SerializeField] private float depositVisionReward = 0.0005f;
    [SerializeField] private float depositVisionRange = 12f;
    [SerializeField] private float depositVisionHalfAngle = 60f;
    [Tooltip("Minimale snelheid om LoS-reward te krijgen. Voorkomt staren-en-cashen.")]
    [SerializeField] private float minSpeedForVisionReward = 1.5f;

    [Header("Rewards: Fase 4 (nog niet activeren)")]
    [SerializeField] private bool enablePlayerDetection = false;
    [SerializeField] private float playerVisibleReward = 0.02f;
    [SerializeField] private float playerCaughtReward = 5f;

    [Header("Debug")]
    [SerializeField] private bool showDebugOverlay = true;
    [SerializeField] private bool disableTimePenaltyInHeuristic = true;
    [SerializeField] private bool drawGridGizmo = true;
    [SerializeField] private bool drawVisionGizmo = true;
    [SerializeField] private bool drawNearestDepositGizmo = true;

    // ---------- Static: één agent claimt de overlay tijdens parallel training ----------
    private static GuardAgent s_overlayOwner;

    // ---------- Internal state ----------
    private Rigidbody rb;
    private bool[,] visitedCells;
    private HashSet<int> visitedRooms;
    private HashSet<int> discoveredDeposits;
    private Dictionary<int, float> depositLastVisitTime;
    private Queue<int> recentlyVisitedDeposits;
    private List<Transform> depositTransforms;
    private int totalDepositsInScene;
    private bool allDepositsBonusGiven;
    private float idleTimer;
    private Vector3 lastPosition;
    private Vector3 startPositionLocal;
    private Quaternion startRotationLocal;

    // Debug overlay state
    private float lastRewardGained;
    private string lastRewardSource = "—";
    private Vector3 lastNearestDepositPos;
    private bool hasNearestDeposit;

    public override void Initialize()
    {
        rb = GetComponent<Rigidbody>();
        MaxStep = maxStepsPerEpisode;
        startPositionLocal = transform.localPosition;
        startRotationLocal = transform.localRotation;

        depositLastVisitTime = new Dictionary<int, float>();
        recentlyVisitedDeposits = new Queue<int>();
        visitedRooms = new HashSet<int>();
        discoveredDeposits = new HashSet<int>();

        depositTransforms = new List<Transform>();
        Transform searchRoot = transform.parent != null ? transform.parent : transform.root;
        CollectDepositsRecursive(searchRoot, depositTransforms);
        totalDepositsInScene = depositTransforms.Count;

        Debug.Log($"GuardAgent: {totalDepositsInScene} deposits gevonden in dit environment.");
    }

    private void CollectDepositsRecursive(Transform parent, List<Transform> list)
    {
        foreach (Transform child in parent)
        {
            if (child.CompareTag("Deposit")) list.Add(child);
            CollectDepositsRecursive(child, list);
        }
    }

    private void OnDestroy()
    {
        // Release overlay-eigenaarschap zodat een andere agent het kan overnemen
        if (s_overlayOwner == this) s_overlayOwner = null;
    }

    public override void OnEpisodeBegin()
    {
        if (rb == null) Initialize();

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            Transform chosen = spawnPoints[Random.Range(0, spawnPoints.Length)];
            Vector3 spawnPos = chosen.position;
            spawnPos.y = transform.parent != null
                ? transform.parent.position.y + startPositionLocal.y
                : startPositionLocal.y;
            spawnPos.x += Random.Range(-spawnPositionJitter, spawnPositionJitter);
            spawnPos.z += Random.Range(-spawnPositionJitter, spawnPositionJitter);
            transform.position = spawnPos;

            if (randomizeSpawnRotation)
                transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            else
                transform.rotation = chosen.rotation;
        }
        else
        {
            transform.localPosition = startPositionLocal;
            transform.localRotation = startRotationLocal;
        }

        visitedCells = new bool[gridSize, gridSize];
        visitedRooms.Clear();
        discoveredDeposits.Clear();
        depositLastVisitTime.Clear();
        recentlyVisitedDeposits.Clear();
        allDepositsBonusGiven = false;
        idleTimer = 0f;
        lastPosition = transform.position;
        hasNearestDeposit = false;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        float halfField = fieldSize / 2f;
        sensor.AddObservation(transform.localPosition.x / halfField);
        sensor.AddObservation(transform.localPosition.z / halfField);

        float yRot = transform.eulerAngles.y * Mathf.Deg2Rad;
        sensor.AddObservation(Mathf.Sin(yRot));
        sensor.AddObservation(Mathf.Cos(yRot));

        Transform nearest = FindNearestTargetDeposit();
        if (nearest != null)
        {
            hasNearestDeposit = true;
            lastNearestDepositPos = nearest.position;

            Vector3 worldDelta = nearest.position - transform.position;
            Vector3 localDelta = transform.InverseTransformDirection(worldDelta);
            float dist = worldDelta.magnitude;

            sensor.AddObservation(Mathf.Clamp(localDelta.x / fieldSize, -1f, 1f));
            sensor.AddObservation(Mathf.Clamp(localDelta.z / fieldSize, -1f, 1f));
            sensor.AddObservation(Mathf.Clamp(dist / fieldSize, 0f, 1f));
        }
        else
        {
            hasNearestDeposit = false;
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            sensor.AddObservation(1f);
        }

        sensor.AddObservation(totalDepositsInScene > 0
            ? (float)discoveredDeposits.Count / totalDepositsInScene
            : 0f);

        // TOTAAL: 8 observations
    }

    private Transform FindNearestTargetDeposit()
    {
        if (depositTransforms == null || depositTransforms.Count == 0) return null;

        Transform nearest = null;
        float minDist = float.MaxValue;

        foreach (var dep in depositTransforms)
        {
            if (dep == null) continue;
            if (discoveredDeposits.Contains(dep.GetInstanceID())) continue;

            float d = (dep.position - transform.position).sqrMagnitude;
            if (d < minDist) { minDist = d; nearest = dep; }
        }
        if (nearest != null) return nearest;

        foreach (var dep in depositTransforms)
        {
            if (dep == null) continue;
            if (recentlyVisitedDeposits.Contains(dep.GetInstanceID())) continue;

            float d = (dep.position - transform.position).sqrMagnitude;
            if (d < minDist) { minDist = d; nearest = dep; }
        }
        if (nearest != null) return nearest;

        foreach (var dep in depositTransforms)
        {
            if (dep == null) continue;
            float d = (dep.position - transform.position).sqrMagnitude;
            if (d < minDist) { minDist = d; nearest = dep; }
        }
        return nearest;
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        float move = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
        float turn = Mathf.Clamp(actions.ContinuousActions[1], -1f, 1f);

        float currentSpeed = (enablePlayerDetection && CanSeePlayer()) ? chaseSpeed : patrolSpeed;

        Vector3 moveStep = transform.forward * move * currentSpeed * Time.deltaTime;
        rb.MovePosition(rb.position + moveStep);

        float turnStep = turn * rotationSpeed * Time.deltaTime;
        transform.Rotate(0f, turnStep, 0f);

        bool isHeuristic = GetComponent<Unity.MLAgents.Policies.BehaviorParameters>().BehaviorType
                          == Unity.MLAgents.Policies.BehaviorType.HeuristicOnly;
        if (!(isHeuristic && disableTimePenaltyInHeuristic))
        {
            AddReward(timeStepPenalty);
        }

        TrackCellVisit();
        TrackIdle();
        TrackDepositVisibility();

        if (enablePlayerDetection && CanSeePlayer())
            AddRewardWithDebug(playerVisibleReward, "player visible");
    }

    private void TrackCellVisit()
    {
        float halfField = fieldSize / 2f;
        int x = Mathf.FloorToInt((transform.localPosition.x + halfField) / fieldSize * gridSize);
        int z = Mathf.FloorToInt((transform.localPosition.z + halfField) / fieldSize * gridSize);

        if (x >= 0 && x < gridSize && z >= 0 && z < gridSize && !visitedCells[x, z])
        {
            visitedCells[x, z] = true;
            AddRewardWithDebug(newCellReward, $"new cell [{x},{z}]");
        }
    }

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

    private void TrackDepositVisibility()
    {
        if (!enableDepositVisionReward) return;
        if (rb.linearVelocity.magnitude < minSpeedForVisionReward) return;

        Collider[] nearby = Physics.OverlapSphere(transform.position, depositVisionRange);
        foreach (var col in nearby)
        {
            if (!col.CompareTag("Deposit")) continue;

            Vector3 toDeposit = col.transform.position - transform.position;
            float dist = toDeposit.magnitude;
            if (dist < 0.01f) continue;
            Vector3 dir = toDeposit / dist;

            float angle = Vector3.Angle(transform.forward, dir);
            if (angle > depositVisionHalfAngle) continue;

            if (HasLineOfSight(transform.position, col.transform.position))
            {
                AddReward(depositVisionReward);
                return;
            }
        }
    }

    private bool HasLineOfSight(Vector3 from, Vector3 to)
    {
        Vector3 direction = to - from;
        float distance = direction.magnitude;
        RaycastHit[] hits = Physics.RaycastAll(from, direction.normalized, distance);
        foreach (var h in hits)
        {
            if (h.collider.CompareTag("Wall")) return false;
        }
        return true;
    }

    private bool CanSeePlayer()
    {
        if (Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, 15f))
            return hit.collider.CompareTag("Player");
        return false;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Wall"))
            AddRewardWithDebug(wallHitPenalty, "wall hit");
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Room"))
        {
            int rid = other.GetInstanceID();
            if (!visitedRooms.Contains(rid))
            {
                visitedRooms.Add(rid);
                AddRewardWithDebug(newRoomReward, $"new room '{other.name}'");
            }
        }

        if (other.CompareTag("Deposit"))
        {
            HandleDepositTrigger(other);
        }

        if (enablePlayerDetection && other.CompareTag("Player"))
        {
            AddRewardWithDebug(playerCaughtReward, "PLAYER CAUGHT");
            EndEpisode();
        }
    }

    private void HandleDepositTrigger(Collider other)
    {
        int id = other.GetInstanceID();

        if (!discoveredDeposits.Contains(id))
        {
            discoveredDeposits.Add(id);
            depositLastVisitTime[id] = Time.time;
            RegisterPatrolVisit(id);
            AddRewardWithDebug(firstDepositVisitReward, $"FIRST '{other.name}'");

            if (!allDepositsBonusGiven &&
                totalDepositsInScene > 0 &&
                discoveredDeposits.Count >= totalDepositsInScene)
            {
                allDepositsBonusGiven = true;
                AddRewardWithDebug(allDepositsFoundBonus, "ALL DEPOSITS FOUND!");
            }
            return;
        }

        if (patrolRotationSize > 0 && recentlyVisitedDeposits.Contains(id))
        {
            if (campingPenalty != 0f)
                AddRewardWithDebug(campingPenalty, $"camping '{other.name}'");
            return;
        }

        if (depositLastVisitTime.ContainsKey(id) &&
            Time.time - depositLastVisitTime[id] < depositCooldown)
        {
            return;
        }

        depositLastVisitTime[id] = Time.time;
        RegisterPatrolVisit(id);
        AddRewardWithDebug(depositPatrolReward, $"patrol '{other.name}'");
    }

    private void RegisterPatrolVisit(int depositId)
    {
        if (patrolRotationSize <= 0) return;

        if (recentlyVisitedDeposits.Contains(depositId))
        {
            var temp = new Queue<int>();
            foreach (var i in recentlyVisitedDeposits)
                if (i != depositId) temp.Enqueue(i);
            recentlyVisitedDeposits = temp;
        }

        recentlyVisitedDeposits.Enqueue(depositId);
        while (recentlyVisitedDeposits.Count > patrolRotationSize)
            recentlyVisitedDeposits.Dequeue();
    }

    private void AddRewardWithDebug(float amount, string source)
    {
        AddReward(amount);
        lastRewardGained = amount;
        lastRewardSource = source;
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuous = actionsOut.ContinuousActions;
        var kb = Keyboard.current;
        if (kb == null) return;

        float vertical = 0f;
        if (kb.wKey.isPressed || kb.upArrowKey.isPressed) vertical += 1f;
        if (kb.sKey.isPressed || kb.downArrowKey.isPressed) vertical -= 1f;

        float horizontal = 0f;
        if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) horizontal += 1f;
        if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) horizontal -= 1f;

        continuous[0] = vertical;
        continuous[1] = horizontal;
    }

    void OnGUI()
    {
        if (!showDebugOverlay) return;

        // Slechts één agent toont overlay tijdens parallel training
        if (s_overlayOwner == null) s_overlayOwner = this;
        if (s_overlayOwner != this) return;

        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 14;
        style.normal.textColor = Color.white;

        GUI.color = new Color(0, 0, 0, 0.6f);
        GUI.Box(new Rect(10, 10, 380, 190), "");
        GUI.color = Color.white;

        GUI.Label(new Rect(20, 15, 360, 20), $"Step: {StepCount} / {MaxStep}", style);
        GUI.Label(new Rect(20, 35, 360, 20), $"Cumulative reward: {GetCumulativeReward():F3}", style);
        GUI.Label(new Rect(20, 55, 360, 20),
            $"Rooms: {visitedRooms?.Count ?? 0}  |  Deposits: {discoveredDeposits?.Count ?? 0}/{totalDepositsInScene}",
            style);
        GUI.Label(new Rect(20, 75, 360, 20),
            $"Rotation queue: [{string.Join(",", recentlyVisitedDeposits ?? new Queue<int>())}]",
            style);

        Color rewardColor = lastRewardGained >= 0
            ? new Color(0.4f, 1f, 0.4f, 1f)
            : new Color(1f, 0.4f, 0.4f, 1f);

        GUIStyle rewardStyle = new GUIStyle(style);
        rewardStyle.normal.textColor = rewardColor;

        GUI.Label(new Rect(20, 105, 360, 20), $"Last reward: {lastRewardGained:+0.000;-0.000}", rewardStyle);
        GUI.Label(new Rect(20, 125, 360, 20), $"Source: {lastRewardSource}", rewardStyle);

        style.fontSize = 11;
        style.normal.textColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        GUI.Label(new Rect(20, 155, 360, 20), "Overlay: agent 1 van " + FindObjectsOfType<GuardAgent>().Length, style);
        GUI.Label(new Rect(20, 175, 360, 20), "WASD = bewegen | Esc om af te sluiten", style);
    }

    void OnDrawGizmos()
    {
        if (drawGridGizmo)
        {
            Vector3 center = transform.parent != null ? transform.parent.position : Vector3.zero;
            float halfField = fieldSize / 2f;

            Gizmos.color = new Color(1f, 1f, 0f, 0.8f);
            Gizmos.DrawWireCube(center + Vector3.up * 0.1f, new Vector3(fieldSize, 0.1f, fieldSize));

            Gizmos.color = new Color(0f, 1f, 1f, 0.25f);
            float cellSize = fieldSize / gridSize;
            for (int i = 0; i <= gridSize; i++)
            {
                float pos = -halfField + i * cellSize;
                Gizmos.DrawLine(
                    center + new Vector3(pos, 0.1f, -halfField),
                    center + new Vector3(pos, 0.1f, halfField)
                );
                Gizmos.DrawLine(
                    center + new Vector3(-halfField, 0.1f, pos),
                    center + new Vector3(halfField, 0.1f, pos)
                );
            }

            if (spawnPoints != null)
            {
                for (int i = 0; i < spawnPoints.Length; i++)
                {
                    if (spawnPoints[i] == null) continue;
                    Gizmos.color = new Color(0f, 1f, 0f, 0.9f);
                    Gizmos.DrawSphere(spawnPoints[i].position, 0.4f);
                }
            }
        }

        if (drawVisionGizmo && enableDepositVisionReward && Application.isPlaying)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.7f);
            Vector3 origin = transform.position;
            Quaternion leftRot = Quaternion.AngleAxis(-depositVisionHalfAngle, Vector3.up);
            Quaternion rightRot = Quaternion.AngleAxis(depositVisionHalfAngle, Vector3.up);
            Vector3 leftDir = leftRot * transform.forward * depositVisionRange;
            Vector3 rightDir = rightRot * transform.forward * depositVisionRange;
            Gizmos.DrawLine(origin, origin + leftDir);
            Gizmos.DrawLine(origin, origin + rightDir);
            Gizmos.DrawLine(origin + leftDir, origin + rightDir);
        }

        if (drawNearestDepositGizmo && Application.isPlaying && hasNearestDeposit)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(transform.position, lastNearestDepositPos);
            Gizmos.DrawWireSphere(lastNearestDepositPos, 0.5f);
        }
    }
}