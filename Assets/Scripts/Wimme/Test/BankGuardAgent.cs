using UnityEngine;
using UnityEngine.InputSystem;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

namespace Wimme.Test
{
    /// <summary>
    /// De ML-Agents PPO-bewaker. Combineert ray-perception en vector-observaties
    /// (eigen yaw/velocity, laatste geluid, speler-detectie, resterende tijd) tot
    /// twee continue acties: vooruit/achteruit en draaien. Beweegt in
    /// <see cref="FixedUpdate"/> met smoothing en grondhellings-projectie.
    /// Schakelt tussen patrol- en chase-snelheid afhankelijk van zicht op de speler.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class BankGuardAgent : Agent
    {
        [SerializeField] private float patrolSpeed = 3.5f;
        [SerializeField] private float chaseSpeed  = 5.5f;
        [SerializeField] private float rotationSpeed = 200f;
        [SerializeField] private float moveSmoothing = 8f;
        [SerializeField] private float turnSmoothing = 6f;
        [SerializeField] private float worldSize = 50f;
        [SerializeField] private int maxStepsPerEpisode = 3000;
        [SerializeField] private HeistEnvController env;

        [SerializeField] private float w_progress         = 0.03f;
        [SerializeField] private float w_reachDeposit     = 0.1f;
        [SerializeField] private float w_investigateNoise = 0.5f;
        [SerializeField] private float w_seePlayer        = 0.02f;
        [SerializeField] private float w_catchPlayer      = 100.0f;
        [SerializeField] private float w_thiefProximity   = 0.2f;
        [SerializeField] private float w_thiefProxScale   = 2.0f;
        [SerializeField] private float w_thiefProxDeadZone = 0.0f;
        [SerializeField] private float w_itemStolen       = 0.0f;
        [SerializeField] private float w_episodeLost      = 0.0f;
        [SerializeField] private float w_wallHit          = 0.0f;
        [SerializeField] private float w_timeStep         = -0.001f;

        [Header("V7 features (requires retrain)")]
        [Tooltip("Verticale awareness + last-known-position. Voegt 4 observaties toe (totaal 17). Het model moet exact dezelfde observatie-grootte hebben — uit-laten voor pre-v7-modellen.")]
        [SerializeField] private bool enableV7Observations = false;
        [SerializeField] private float lastKnownMemorySeconds = 10f;

        private float numDepositsParam, audioEnabled, alarmsEnabled, thiefEnabled, shapingEnabled = 1f;
        private Rigidbody rb;
        private float smoothedMove, smoothedTurn;
        private float pendingMove, pendingTurn;
        private float prevDistanceToTarget;
        private Vector3 lastInvestigatedNoisePos;
        private int idleSteps;

        private Vector3 lastKnownPlayerPos;
        private float lastKnownPlayerTime;
        private bool hasLastKnownPos;

        public override void Initialize()
        {
            rb = GetComponent<Rigidbody>();
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            bool isLiveGame = env != null && env.vrPlayer != null;
            MaxStep = isLiveGame ? 0 : maxStepsPerEpisode;
        }

        public override void OnEpisodeBegin()
        {
            ReadCurriculumParams();
            smoothedMove = 0f;
            smoothedTurn = 0f;
            idleSteps = 0;

            bool isLiveGame = env != null && env.vrPlayer != null;

            if (!isLiveGame && env != null && env.guardSpawn != null)
            {
                transform.position = env.guardSpawn.position + Vector3.up;
                transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            if (!isLiveGame && env != null)
                env.BeginEpisode(
                    Mathf.Clamp((int)numDepositsParam, 1, env.deposits.Count),
                    thiefEnabled > 0.5f, audioEnabled > 0.5f, alarmsEnabled > 0.5f);

            lastInvestigatedNoisePos = Vector3.positiveInfinity;
            hasLastKnownPos = false;
            lastKnownPlayerPos = Vector3.zero;
            prevDistanceToTarget = DistanceToPriorityTarget();
        }

        private void ReadCurriculumParams()
        {
            var ep = Academy.Instance.EnvironmentParameters;
            numDepositsParam   = ep.GetWithDefault("num_deposits", 6f);
            audioEnabled       = ep.GetWithDefault("audio_on", 1f);
            alarmsEnabled      = ep.GetWithDefault("alarms_on", 1f);
            thiefEnabled       = ep.GetWithDefault("thief_on", 1f);
            shapingEnabled     = ep.GetWithDefault("shaping", 1f);
            w_progress         = ep.GetWithDefault("w_progress", w_progress);
            w_reachDeposit     = ep.GetWithDefault("w_reach", w_reachDeposit);
            w_investigateNoise = ep.GetWithDefault("w_investigate", w_investigateNoise);
            w_catchPlayer      = ep.GetWithDefault("w_catch", w_catchPlayer);
            w_itemStolen       = ep.GetWithDefault("w_stolen", w_itemStolen);
        }

        public override void CollectObservations(VectorSensor sensor)
        {
            float half = Mathf.Max(worldSize / 2f, 0.01f);

            float yaw = transform.eulerAngles.y * Mathf.Deg2Rad;
            sensor.AddObservation(Mathf.Sin(yaw));
            sensor.AddObservation(Mathf.Cos(yaw));
            Vector3 v = rb != null ? rb.linearVelocity / Mathf.Max(patrolSpeed, 0.01f) : Vector3.zero;
            sensor.AddObservation(Mathf.Clamp(v.x, -1f, 1f));
            sensor.AddObservation(Mathf.Clamp(v.z, -1f, 1f));

            if (env != null && env.lastNoise != null && env.lastNoise.valid && audioEnabled > 0.5f)
            {
                Vector3 local = transform.InverseTransformDirection(env.lastNoise.position - transform.position);
                sensor.AddObservation(Mathf.Clamp(local.x / half, -1f, 1f));
                sensor.AddObservation(Mathf.Clamp(local.z / half, -1f, 1f));
                sensor.AddObservation(1f - Mathf.Clamp01((Time.time - env.lastNoise.timeEmitted) / 5f));
                sensor.AddObservation(env.lastNoise.loudness);
            }
            else { sensor.AddObservation(0f); sensor.AddObservation(0f); sensor.AddObservation(0f); sensor.AddObservation(0f); }

            bool see = TrySeePlayer(out Vector3 pLocal, out float pDist);
            sensor.AddObservation(see ? 1f : 0f);
            sensor.AddObservation(Mathf.Clamp(pLocal.x / half, -1f, 1f));
            sensor.AddObservation(Mathf.Clamp(pLocal.z / half, -1f, 1f));
            sensor.AddObservation(Mathf.Clamp(pDist / worldSize, 0f, 1f));

            if (see)
            {
                lastKnownPlayerPos = env.thiefTarget.position;
                lastKnownPlayerTime = Time.time;
                hasLastKnownPos = true;
            }

            sensor.AddObservation(env != null ? Mathf.Clamp01(env.timeLeft / Mathf.Max(env.episodeSeconds, 0.01f)) : 0f);
            // base: 13

            if (enableV7Observations)
            {
                sensor.AddObservation(Mathf.Clamp(pLocal.y / half, -1f, 1f));

                bool lkValid = hasLastKnownPos && !see && (Time.time - lastKnownPlayerTime) < lastKnownMemorySeconds;
                sensor.AddObservation(lkValid ? 1f : 0f);
                if (lkValid)
                {
                    Vector3 lkLocal = transform.InverseTransformDirection(lastKnownPlayerPos - transform.position);
                    sensor.AddObservation(Mathf.Clamp(lkLocal.x / half, -1f, 1f));
                    sensor.AddObservation(Mathf.Clamp(lkLocal.z / half, -1f, 1f));
                }
                else
                {
                    sensor.AddObservation(0f);
                    sensor.AddObservation(0f);
                }
                // v7 total: 17
            }
        }

        public override void OnActionReceived(ActionBuffers actions)
        {
            pendingMove = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
            pendingTurn = Mathf.Clamp(actions.ContinuousActions[1], -1f, 1f);

            AddReward(w_timeStep);

            float spd = rb != null ? rb.linearVelocity.magnitude : 0f;
            if (spd > 0.5f) { AddReward(0.005f); idleSteps = 0; } else { idleSteps++; }
            if (idleSteps > 100) AddReward(-0.01f);

            if (shapingEnabled > 0.5f)
            {
                float d = DistanceToPriorityTarget();
                if (!float.IsInfinity(prevDistanceToTarget) && !float.IsInfinity(d))
                    AddReward(w_progress * Mathf.Clamp(prevDistanceToTarget - d, -0.5f, 0.5f));
                prevDistanceToTarget = d;
            }

            if (TrySeePlayer(out _, out _)) AddReward(w_seePlayer);

            var tt = env != null ? env.thiefTarget : null;
            if (thiefEnabled > 0.5f && tt != null && tt.gameObject.activeSelf)
            {
                float dT = Vector3.Distance(transform.position, tt.position);
                if (dT > w_thiefProxDeadZone)
                    AddReward(w_thiefProximity * Mathf.Exp(-dT / Mathf.Max(w_thiefProxScale, 0.01f)));
                if (dT < 1.0f && env != null && !env.episodeOver)
                { AddReward(w_catchPlayer); env.EndEpisode(HeistEnvController.GuardOutcome.Caught); return; }
            }

            if (audioEnabled > 0.5f && env != null && env.lastNoise.valid)
            {
                float dn = Vector3.Distance(transform.position, env.lastNoise.position);
                if (dn < 2.5f && Vector3.Distance(env.lastNoise.position, lastInvestigatedNoisePos) > 1f)
                {
                    AddReward(w_investigateNoise * env.lastNoise.loudness);
                    lastInvestigatedNoisePos = env.lastNoise.position;
                }
            }

            if (enableV7Observations && hasLastKnownPos && !TrySeePlayer(out _, out _))
            {
                float age = Time.time - lastKnownPlayerTime;
                if (age < lastKnownMemorySeconds)
                {
                    float dLk = Vector3.Distance(transform.position, lastKnownPlayerPos);
                    if (dLk < 3f) { AddReward(0.3f); hasLastKnownPos = false; }
                }
                else { hasLastKnownPos = false; }
            }
        }

        void FixedUpdate()
        {
            if (rb == null) return;
            bool seeingPlayer = thiefEnabled > 0.5f && TrySeePlayer(out _, out _);
            float speed = seeingPlayer ? chaseSpeed : patrolSpeed;

            smoothedMove = Mathf.Lerp(smoothedMove, pendingMove, moveSmoothing * Time.fixedDeltaTime);
            smoothedTurn = Mathf.Lerp(smoothedTurn, pendingTurn, turnSmoothing * Time.fixedDeltaTime);

            // Projecteer de looprichting op de grondhellling, zodat de bewaker niet
            // bij iedere hobbeltje een stukje verticaal weg-stuitert.
            Vector3 desiredDir = transform.forward;
            Vector3 castOrigin = transform.position + Vector3.up * 0.1f;
            if (Physics.Raycast(castOrigin, Vector3.down, out RaycastHit hit, 2.0f, ~0, QueryTriggerInteraction.Ignore))
            {
                Vector3 projected = Vector3.ProjectOnPlane(transform.forward, hit.normal);
                if (projected.sqrMagnitude > 1e-4f) desiredDir = projected.normalized;
            }

            Vector3 desiredVel = desiredDir * smoothedMove * speed;
            Vector3 cur = rb.linearVelocity;
            rb.linearVelocity = new Vector3(desiredVel.x, cur.y, desiredVel.z);

            Quaternion turn = Quaternion.Euler(0f, smoothedTurn * rotationSpeed * Time.fixedDeltaTime, 0f);
            rb.MoveRotation(rb.rotation * turn);

            if (seeingPlayer && env != null && env.thiefTarget != null)
                Debug.DrawLine(transform.position + Vector3.up, env.thiefTarget.position + Vector3.up, Color.red);
            if (hasLastKnownPos && !seeingPlayer)
                Debug.DrawLine(transform.position + Vector3.up, lastKnownPlayerPos + Vector3.up, Color.yellow);
        }

        private float DistanceToPriorityTarget()
        {
            if (env == null) return float.PositiveInfinity;
            var tt = env.thiefTarget;
            if (thiefEnabled > 0.5f && tt != null && tt.gameObject.activeSelf
                && TrySeePlayer(out _, out float td)) return td;
            if (enableV7Observations && hasLastKnownPos && (Time.time - lastKnownPlayerTime) < lastKnownMemorySeconds)
                return Vector3.Distance(transform.position, lastKnownPlayerPos);
            if (audioEnabled > 0.5f && env.lastNoise != null && env.lastNoise.valid)
            {
                float age = Time.time - env.lastNoise.timeEmitted;
                if (age < 4f && env.lastNoise.loudness > 0.4f)
                    return Vector3.Distance(transform.position, env.lastNoise.position);
            }
            foreach (var d in env.deposits)
                if (d.alarmed && d.t != null && d.t.gameObject.activeSelf && !d.stolen)
                    return Vector3.Distance(transform.position, d.t.position);
            return float.PositiveInfinity;
        }

        private bool TrySeePlayer(out Vector3 localPos, out float dist)
        {
            localPos = Vector3.zero; dist = worldSize;
            var tt = env != null ? env.thiefTarget : null;
            if (tt == null || !tt.gameObject.activeSelf) return false;
            Vector3 toThief = tt.position - transform.position;
            dist = toThief.magnitude; localPos = transform.InverseTransformDirection(toThief);
            if (dist > 20f) return false;
            if (Vector3.Angle(transform.forward, toThief) > 75f) return false;
            if (Physics.Raycast(transform.position, toThief.normalized, out RaycastHit hit, dist, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.CompareTag("Wall")) return false;
            }
            return true;
        }

        void OnCollisionStay(Collision col)
        {
            if (col.collider.CompareTag("Wall")) AddReward(-0.01f);
        }

        void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Deposit") && env != null)
                foreach (var d in env.deposits)
                    if (d.t == other.transform && !d.stolen && d.t.gameObject.activeSelf)
                    { AddReward(w_reachDeposit + (d.alarmed ? 0.5f : 0f)); d.alarmed = false; prevDistanceToTarget = DistanceToPriorityTarget(); break; }

            TryCatch(other);
        }

        void OnTriggerStay(Collider other) { TryCatch(other); }

        private void TryCatch(Collider other)
        {
            if (env == null || env.episodeOver) return;
            var tt = env.thiefTarget;
            if (tt != null && (other.transform == tt || other.transform.IsChildOf(tt)))
            { AddReward(w_catchPlayer); env.EndEpisode(HeistEnvController.GuardOutcome.Caught); }
        }

        public void OnItemStolen(HeistEnvController.DepositState d)
        { AddReward(w_itemStolen); prevDistanceToTarget = DistanceToPriorityTarget(); }

        public void OnEnvironmentEnded(HeistEnvController.GuardOutcome outcome)
        {
            switch (outcome)
            {
                case HeistEnvController.GuardOutcome.Caught: break;
                case HeistEnvController.GuardOutcome.AllStolen: AddReward(w_episodeLost); break;
                case HeistEnvController.GuardOutcome.TimeUp:
                    if (env != null) { int s = 0; foreach (var d in env.deposits) if (d.t != null && d.t.gameObject.activeSelf && !d.stolen) s++; AddReward(0.25f * s); }
                    break;
            }
            EndEpisode();
        }

        public override void Heuristic(in ActionBuffers actionsOut)
        {
            var c = actionsOut.ContinuousActions;
            var kb = Keyboard.current; if (kb == null) return;
            c[0] = (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);
            c[1] = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
        }
    }
}
