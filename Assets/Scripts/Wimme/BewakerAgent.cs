using UnityEngine;
using UnityEngine.InputSystem;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using System.Collections.Generic;

/// <summary>
/// Bewaker-agent voor Heist and Seek.
/// FASE 1: leer navigeren en alle deposits afgaan (patrouille).
///
/// DESIGN: patrouille = een ketting van Obelix-problemen. De omgeving kiest
/// telkens EEN doel-deposit; de agent observeert enkel de richting daarheen
/// (plus de Ray Perception Sensor voor muren/deposits). Bij aankomst volgt
/// meteen een ander doel.
///
/// ANTI WALL-HUGGING: de afstand-shaping is altijd actief tijdens vrije
/// beweging (bootstrap), MAAR wordt uitgezet zolang de agent fysiek tegen een
/// muur duwt; daar krijgt hij bovendien een kleine continue straf per stap.
/// Zo verdwijnt het lokale optimum "tegen de muur in rechte lijn dichtbij"
/// zonder het leersignaal in open ruimte te slopen.
///
/// UITBREIDBAAR (fase 2): een sound cue is gewoon een tijdelijk doel met
/// voorrang — overschrijf het huidige doel, dezelfde machinerie werkt door.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class GuardAgent : Agent
{
    [Header("Movement")]
    [SerializeField] private float patrolSpeed = 5f;
    [SerializeField] private float rotationSpeed = 180f;

    [Header("Field Settings")]
    [Tooltip("Gebruikt om observaties te normaliseren. ~grootte van de bank.")]
    [SerializeField] private float fieldSize = 100f;

    [Header("Spawn")]
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private bool randomizeSpawnRotation = true;
    [SerializeField] private float spawnPositionJitter = 0.5f;

    [Header("Episode")]
    [Tooltip("Aanrader: ~5000. Lange episodes + sparse reward leren traag.")]
    [SerializeField] private int maxStepsPerEpisode = 5000;

    [Header("Rewards")]
    [Tooltip("Reward bij het bereiken van het toegewezen doel-deposit.")]
    [SerializeField] private float reachReward = 1.0f;
    [Tooltip("Straf per beslissing. Ontmoedigt treuzelen/stilstaan.")]
    [SerializeField] private float timeStepPenalty = -0.0005f;
    [Tooltip("Eenmalige tik op het moment dat hij een muur raakt.")]
    [SerializeField] private float wallHitPenalty = -0.05f;
    [Tooltip("Continue straf elke stap zolang hij tegen een muur duwt. " +
             "Botst rechtstreeks tegen de shaping op; opdraaien als hij blijft hangen.")]
    [SerializeField] private float wallStayPenaltyPerStep = -0.01f;
    [Tooltip("Veilige potential-based shaping: (vorigeAfstand - nieuweAfstand) * scale. " +
             "Telescopeert, dus niet te farmen door heen-en-weer te bewegen. " +
             "Uitgezet zolang hij een muur raakt (geen reward voor muur-hugging).")]
    [SerializeField] private float distanceShapingScale = 0.05f;

    [Header("Debug")]
    [SerializeField] private bool showDebugOverlay = true;
    [SerializeField] private bool disableTimePenaltyInHeuristic = true;
    [SerializeField] private bool drawGizmos = true;

    // ---------- Static: één agent claimt de overlay tijdens parallel training ----------
    private static GuardAgent s_overlayOwner;

    // ---------- Internal state ----------
    private Rigidbody rb;
    private List<Transform> depositTransforms;
    private Transform currentTarget;
    private int currentTargetId;
    private float prevDistanceToTarget;
    private int wallContacts;
    private Vector3 startPositionLocal;
    private Quaternion startRotationLocal;

    private bool TouchingWall => wallContacts > 0;

    // Debug overlay state
    private float lastRewardGained;
    private string lastRewardSource = "—";

    public override void Initialize()
    {
        rb = GetComponent<Rigidbody>();
        MaxStep = maxStepsPerEpisode;
        startPositionLocal = transform.localPosition;
        startRotationLocal = transform.localRotation;

        depositTransforms = new List<Transform>();
        Transform searchRoot = transform.parent != null ? transform.parent : transform.root;
        CollectDepositsRecursive(searchRoot, depositTransforms);

        Debug.Log($"GuardAgent: {depositTransforms.Count} deposits gevonden in dit environment.");
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
        if (s_overlayOwner == this) s_overlayOwner = null;
    }

    public override void OnEpisodeBegin()
    {
        if (rb == null) Initialize();

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        wallContacts = 0;

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

        AssignNewTarget(avoidCurrent: false);
    }

    /// <summary>
    /// Kies een willekeurig deposit als doel. Reset de shaping-referentie zodat
    /// de afstandssprong bij een doelwissel geen valse straf/reward geeft.
    /// </summary>
    private void AssignNewTarget(bool avoidCurrent)
    {
        if (depositTransforms == null || depositTransforms.Count == 0)
        {
            currentTarget = null;
            currentTargetId = 0;
            prevDistanceToTarget = 0f;
            return;
        }

        Transform chosen;
        if (depositTransforms.Count == 1 || !avoidCurrent || currentTarget == null)
        {
            chosen = depositTransforms[Random.Range(0, depositTransforms.Count)];
        }
        else
        {
            do { chosen = depositTransforms[Random.Range(0, depositTransforms.Count)]; }
            while (chosen == currentTarget);
        }

        currentTarget = chosen;
        currentTargetId = chosen.GetInstanceID();
        prevDistanceToTarget = Vector3.Distance(transform.position, chosen.position);
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (currentTarget != null)
        {
            Vector3 worldDelta = currentTarget.position - transform.position;
            Vector3 localDelta = transform.InverseTransformDirection(worldDelta);
            float dist = worldDelta.magnitude;

            sensor.AddObservation(Mathf.Clamp(localDelta.x / fieldSize, -1f, 1f));
            sensor.AddObservation(Mathf.Clamp(localDelta.z / fieldSize, -1f, 1f));
            sensor.AddObservation(Mathf.Clamp(dist / fieldSize, 0f, 1f));
        }
        else
        {
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            sensor.AddObservation(1f);
        }

        Vector3 localVel = transform.InverseTransformDirection(rb.linearVelocity);
        sensor.AddObservation(Mathf.Clamp(localVel.x / patrolSpeed, -1f, 1f));
        sensor.AddObservation(Mathf.Clamp(localVel.z / patrolSpeed, -1f, 1f));

        // TOTAAL: 5 observations (Behavior Parameters > Space Size = 5)
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        float move = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
        float turn = Mathf.Clamp(actions.ContinuousActions[1], -1f, 1f);

        Vector3 moveStep = transform.forward * move * patrolSpeed * Time.deltaTime;
        rb.MovePosition(rb.position + moveStep);

        float turnStep = turn * rotationSpeed * Time.deltaTime;
        transform.Rotate(0f, turnStep, 0f);

        bool isHeuristic = GetComponent<Unity.MLAgents.Policies.BehaviorParameters>().BehaviorType
                          == Unity.MLAgents.Policies.BehaviorType.HeuristicOnly;
        if (!(isHeuristic && disableTimePenaltyInHeuristic))
            AddReward(timeStepPenalty);

        if (TouchingWall)
            AddReward(wallStayPenaltyPerStep);

        ApplyDistanceShaping();
    }

    /// <summary>
    /// Potential-based shaping op het huidige doel. Som telescopeert: naar het
    /// doel toe en weer weg = netto nul, dus niet exploiteerbaar door wiebelen.
    /// Uitgezet zolang hij een muur raakt — daar zou de rechte-lijn-gradiënt
    /// hem alleen maar harder tegen de muur duwen. prevDistance loopt wel door,
    /// zodat er geen reward-sprong is als hij loskomt.
    /// </summary>
    private void ApplyDistanceShaping()
    {
        if (currentTarget == null || distanceShapingScale == 0f) return;

        float newDist = Vector3.Distance(transform.position, currentTarget.position);
        float delta = prevDistanceToTarget - newDist;
        prevDistanceToTarget = newDist;

        if (!TouchingWall && delta != 0f)
            AddReward(delta * distanceShapingScale);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Wall"))
        {
            wallContacts++;
            AddRewardWithDebug(wallHitPenalty, "wall hit");
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        if (collision.gameObject.CompareTag("Wall"))
            wallContacts = Mathf.Max(0, wallContacts - 1);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Deposit")) return;
        if (currentTarget == null) return;
        if (other.transform.GetInstanceID() != currentTargetId) return;

        AddRewardWithDebug(reachReward, $"REACHED '{other.name}'");
        AssignNewTarget(avoidCurrent: true);
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

        string targetName = currentTarget != null ? currentTarget.name : "—";
        float targetDist = currentTarget != null
            ? Vector3.Distance(transform.position, currentTarget.position)
            : 0f;
        GUI.Label(new Rect(20, 55, 360, 20), $"Target: {targetName}  (d={targetDist:F1})", style);
        GUI.Label(new Rect(20, 75, 360, 20),
            $"Tegen muur: {(TouchingWall ? "JA (shaping uit + straf)" : "nee (shaping aan)")}",
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
        GUI.Label(new Rect(20, 155, 360, 20),
            "Overlay: 1 van " + FindObjectsOfType<GuardAgent>().Length, style);
        GUI.Label(new Rect(20, 175, 360, 20), "WASD = bewegen | Esc om af te sluiten", style);
    }

    void OnDrawGizmos()
    {
        if (!drawGizmos) return;

        Vector3 center = transform.parent != null ? transform.parent.position : Vector3.zero;
        Gizmos.color = new Color(1f, 1f, 0f, 0.4f);
        Gizmos.DrawWireCube(center + Vector3.up * 0.1f, new Vector3(fieldSize, 0.1f, fieldSize));

        if (spawnPoints != null)
        {
            Gizmos.color = new Color(0f, 1f, 0f, 0.9f);
            foreach (var sp in spawnPoints)
                if (sp != null) Gizmos.DrawSphere(sp.position, 0.4f);
        }

        if (Application.isPlaying && currentTarget != null)
        {
            // Groen = shaping aan, rood = tegen muur (shaping uit + straf)
            Gizmos.color = TouchingWall ? Color.red : Color.green;
            Gizmos.DrawLine(transform.position, currentTarget.position);
            Gizmos.DrawWireSphere(currentTarget.position, 0.5f);
        }
    }
}
