using UnityEngine;
using UnityEngine.InputSystem;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

namespace Wimme.Test
{
    [RequireComponent(typeof(Rigidbody))]
    public class BankGuardAgent : Agent
    {
        // ---------- Movement ----------
        [SerializeField] private float patrolSpeed = 3.5f;
        [SerializeField] private float chaseSpeed  = 5.5f;
        [SerializeField] private float rotationSpeed = 200f;

        // ---------- World scale (used for normalization only) ----------
        [SerializeField] private float worldSize = 50f;

        // ---------- Episode ----------
        [SerializeField] private int maxStepsPerEpisode = 3000;

        // ---------- Refs ----------
        [SerializeField] private HeistEnvController env;

        // ---------- Reward weights (v5b — high catch reward + proximity dead zone) ----------
        [SerializeField] private float w_progress         = 0.03f;
        [SerializeField] private float w_reachDeposit     = 0.1f;
        [SerializeField] private float w_investigateNoise = 0.5f;
        [SerializeField] private float w_seePlayer        = 0.05f;
        [SerializeField] private float w_catchPlayer      = 100.0f;
        [SerializeField] private float w_thiefProximity   = 0.05f;
        [SerializeField] private float w_thiefProxScale   = 5.0f;
        [SerializeField] private float w_thiefProxDeadZone = 2.0f;
        [SerializeField] private float w_itemStolen       = 0.0f;
        [SerializeField] private float w_episodeLost      = 0.0f;
        [SerializeField] private float w_wallHit          = 0.0f;
        [SerializeField] private float w_timeStep         = -0.001f;

        // ---------- Curriculum toggles (driven by Academy EnvironmentParameters) ----------
        private float numDepositsParam = 1f;
        private float audioEnabled     = 0f;
        private float alarmsEnabled    = 0f;
        private float thiefEnabled     = 0f;
        private float shapingEnabled   = 1f;

        // ---------- Internal ----------
        private Rigidbody rb;
        private float pendingMove;
        private float pendingTurn;
        private float prevDistanceToTarget;
        private Vector3 lastInvestigatedNoisePos;
        private int idleSteps;

        public override void Initialize()
        {
            rb = GetComponent<Rigidbody>();
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            MaxStep = maxStepsPerEpisode;
        }

        public override void OnEpisodeBegin()
        {
            ReadCurriculumParams();

            if (env != null)
            {
                if (env.guardSpawn != null)
                {
                    transform.position = env.guardSpawn.position + Vector3.up;
                    transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
                env.BeginEpisode(
                    activeDepositCount: Mathf.Clamp((int)numDepositsParam, 1, env.deposits.Count),
                    thiefEnabled: thiefEnabled > 0.5f,
                    audioEnabled: audioEnabled > 0.5f,
                    alarmsEnabled: alarmsEnabled > 0.5f);
            }

            lastInvestigatedNoisePos = Vector3.positiveInfinity;
            prevDistanceToTarget = DistanceToPriorityTarget();
        }

        private void ReadCurriculumParams()
        {
            // Defaults match the trained policy's deployment task: full game with
            // thief, audio, alarms, and all 6 deposits active. During training the
            // mlagents Academy overrides these via the curriculum YAML; during
            // inference (no Academy connection) the defaults below kick in so the
            // agent sees the same world it was trained on.
            var ep = Academy.Instance.EnvironmentParameters;
            numDepositsParam = ep.GetWithDefault("num_deposits", 6f);
            audioEnabled     = ep.GetWithDefault("audio_on", 1f);
            alarmsEnabled    = ep.GetWithDefault("alarms_on", 1f);
            thiefEnabled     = ep.GetWithDefault("thief_on", 1f);
            shapingEnabled   = ep.GetWithDefault("shaping", 1f);
            w_progress       = ep.GetWithDefault("w_progress",   w_progress);
            w_reachDeposit   = ep.GetWithDefault("w_reach",      w_reachDeposit);
            w_investigateNoise = ep.GetWithDefault("w_investigate", w_investigateNoise);
            w_catchPlayer    = ep.GetWithDefault("w_catch",      w_catchPlayer);
            w_itemStolen     = ep.GetWithDefault("w_stolen",     w_itemStolen);
        }

        public override void CollectObservations(VectorSensor sensor)
        {
            // Perception comes from RayPerceptionSensorComponent3D (Wall/Player/Deposit
            // tags) attached to this GameObject — that's how the agent SEES the world.
            // Vector observations only carry information rays can't capture: own motion
            // state, audio cue, currently-visible thief vector, episode timer.

            float half = Mathf.Max(worldSize / 2f, 0.01f);

            // Self orientation + velocity (4). No absolute position — keeps the policy
            // perception-driven so it transfers to other scenes.
            float yaw = transform.eulerAngles.y * Mathf.Deg2Rad;
            sensor.AddObservation(Mathf.Sin(yaw));
            sensor.AddObservation(Mathf.Cos(yaw));
            Vector3 v = rb != null ? rb.linearVelocity / Mathf.Max(patrolSpeed, 0.01f) : Vector3.zero;
            sensor.AddObservation(Mathf.Clamp(v.x, -1f, 1f));
            sensor.AddObservation(Mathf.Clamp(v.z, -1f, 1f));

            // Noise (4) — body-local direction + freshness + loudness, zeroed when audio off.
            if (env != null && env.lastNoise != null && env.lastNoise.valid && audioEnabled > 0.5f)
            {
                Vector3 local = transform.InverseTransformDirection(env.lastNoise.position - transform.position);
                sensor.AddObservation(Mathf.Clamp(local.x / half, -1f, 1f));
                sensor.AddObservation(Mathf.Clamp(local.z / half, -1f, 1f));
                float age = Mathf.Clamp01((Time.time - env.lastNoise.timeEmitted) / 5f);
                sensor.AddObservation(1f - age);
                sensor.AddObservation(env.lastNoise.loudness);
            }
            else { sensor.AddObservation(0f); sensor.AddObservation(0f); sensor.AddObservation(0f); sensor.AddObservation(0f); }

            // Player (4) — only valid when in cone of sight (also encoded in rays).
            bool seePlayer = TrySeePlayer(out Vector3 playerLocal, out float playerDist);
            sensor.AddObservation(seePlayer ? 1f : 0f);
            sensor.AddObservation(Mathf.Clamp(playerLocal.x / half, -1f, 1f));
            sensor.AddObservation(Mathf.Clamp(playerLocal.z / half, -1f, 1f));
            sensor.AddObservation(Mathf.Clamp(playerDist / worldSize, 0f, 1f));

            // Timer (1)
            float tFrac = env != null ? Mathf.Clamp01(env.timeLeft / Mathf.Max(env.episodeSeconds, 0.01f)) : 0f;
            sensor.AddObservation(tFrac);

            // TOTAL: 4 + 4 + 4 + 1 = 13 vector observations (was 51)
        }

        public override void OnActionReceived(ActionBuffers actions)
        {
            pendingMove = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
            pendingTurn = Mathf.Clamp(actions.ContinuousActions[1], -1f, 1f);

            AddReward(w_timeStep);

            // Movement reward: discourage camping, encourage active patrol.
            float speed = rb != null ? rb.linearVelocity.magnitude : 0f;
            if (speed > 0.5f)
                AddReward(0.005f);
            else
                idleSteps++;
            if (speed > 0.5f) idleSteps = 0;
            if (idleSteps > 100)
                AddReward(-0.01f);

            if (shapingEnabled > 0.5f)
            {
                float d = DistanceToPriorityTarget();
                if (!float.IsInfinity(prevDistanceToTarget) && !float.IsInfinity(d))
                {
                    float delta = prevDistanceToTarget - d;
                    AddReward(w_progress * Mathf.Clamp(delta, -0.5f, 0.5f));
                }
                prevDistanceToTarget = d;
            }

            if (TrySeePlayer(out _, out _)) AddReward(w_seePlayer);

            // Proximity-to-thief bonus with DEAD ZONE: rewards approach but NOT
            // lingering at catching distance. Inside the dead zone (< 2m) the ONLY
            // positive reward is the big catch bonus (+100). This prevents the policy
            // from learning "follow forever" instead of "commit to catch".
            if (thiefEnabled > 0.5f && env != null && env.thief != null && env.thief.gameObject.activeSelf)
            {
                float dThief = Vector3.Distance(transform.position, env.thief.transform.position);
                if (dThief > w_thiefProxDeadZone)
                    AddReward(w_thiefProximity * Mathf.Exp(-dThief / Mathf.Max(w_thiefProxScale, 0.01f)));
            }

            // Noise investigation reward
            if (audioEnabled > 0.5f && env != null && env.lastNoise.valid)
            {
                float distNoise = Vector3.Distance(transform.position, env.lastNoise.position);
                if (distNoise < 2.5f && Vector3.Distance(env.lastNoise.position, lastInvestigatedNoisePos) > 1f)
                {
                    AddReward(w_investigateNoise * env.lastNoise.loudness);
                    lastInvestigatedNoisePos = env.lastNoise.position;
                }
            }
        }

        void FixedUpdate()
        {
            if (rb == null) return;
            float speed = (thiefEnabled > 0.5f && TrySeePlayer(out _, out _)) ? chaseSpeed : patrolSpeed;

            Vector3 desiredDir = transform.forward;
            Vector3 castOrigin = transform.position + Vector3.up * 0.1f;
            if (Physics.Raycast(castOrigin, Vector3.down, out RaycastHit hit,
                                2.0f, ~0, QueryTriggerInteraction.Ignore))
            {
                Vector3 projected = Vector3.ProjectOnPlane(transform.forward, hit.normal);
                if (projected.sqrMagnitude > 1e-4f)
                    desiredDir = projected.normalized;
            }

            Vector3 desiredVel = desiredDir * pendingMove * speed;
            Vector3 v = rb.linearVelocity;
            rb.linearVelocity = new Vector3(desiredVel.x, v.y, desiredVel.z);

            Quaternion turn = Quaternion.Euler(0f, pendingTurn * rotationSpeed * Time.fixedDeltaTime, 0f);
            rb.MoveRotation(rb.rotation * turn);
        }

        private float DistanceToPriorityTarget()
        {
            if (env == null) return float.PositiveInfinity;

            // Priority 1: visible thief — the primary objective. Shape directly toward him.
            if (thiefEnabled > 0.5f && env.thief != null && env.thief.gameObject.activeSelf
                && TrySeePlayer(out _, out float thiefDist))
            {
                return thiefDist;
            }
            // Priority 2: fresh, loud noise (audio cue to thief location)
            if (audioEnabled > 0.5f && env.lastNoise != null && env.lastNoise.valid)
            {
                float age = Time.time - env.lastNoise.timeEmitted;
                if (age < 4f && env.lastNoise.loudness > 0.4f)
                    return Vector3.Distance(transform.position, env.lastNoise.position);
            }
            // Priority 3: alarmed deposit (someone tripped it, probably the thief)
            foreach (var d in env.deposits)
            {
                if (d.alarmed && d.t != null && d.t.gameObject.activeSelf && !d.stolen)
                    return Vector3.Distance(transform.position, d.t.position);
            }
            // No active target: no shaping signal (return +inf disables delta-distance reward).
            return float.PositiveInfinity;
        }

        private bool TrySeePlayer(out Vector3 localPos, out float dist)
        {
            localPos = Vector3.zero; dist = worldSize;
            if (env == null || env.thief == null || !env.thief.gameObject.activeSelf) return false;

            Vector3 toThief = env.thief.transform.position - transform.position;
            dist = toThief.magnitude;
            localPos = transform.InverseTransformDirection(toThief);

            if (dist > 20f) return false;
            float angle = Vector3.Angle(transform.forward, toThief);
            if (angle > 75f) return false;

            if (Physics.Raycast(transform.position, toThief.normalized, out RaycastHit hit, dist + 0.1f))
            {
                if (hit.transform == env.thief.transform) return true;
                if (hit.collider.CompareTag("Wall")) return false;
            }
            return false;
        }

        void OnCollisionEnter(Collision c)
        {
            if (c.gameObject.CompareTag("Wall")) AddReward(w_wallHit);
        }

        void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Deposit") && env != null)
            {
                foreach (var d in env.deposits)
                {
                    if (d.t == other.transform && !d.stolen && d.t.gameObject.activeSelf)
                    {
                        AddReward(w_reachDeposit + (d.alarmed ? 0.5f : 0f));
                        d.alarmed = false;            // securing the deposit clears its alarm
                        prevDistanceToTarget = DistanceToPriorityTarget();
                        break;
                    }
                }
            }

            if (env != null && env.thief != null && other.transform == env.thief.transform)
            {
                AddReward(w_catchPlayer);
                env.EndEpisode(HeistEnvController.GuardOutcome.Caught);
            }
        }

        public void OnItemStolen(HeistEnvController.DepositState d)
        {
            AddReward(w_itemStolen);
            prevDistanceToTarget = DistanceToPriorityTarget();
        }

        public void OnEnvironmentEnded(HeistEnvController.GuardOutcome outcome)
        {
            switch (outcome)
            {
                case HeistEnvController.GuardOutcome.Caught:    /* already rewarded */ break;
                case HeistEnvController.GuardOutcome.AllStolen: AddReward(w_episodeLost); break;
                case HeistEnvController.GuardOutcome.TimeUp:
                    // partial credit: any unstolen, active deposit at end is a small win
                    if (env != null)
                    {
                        int saved = 0;
                        foreach (var d in env.deposits)
                            if (d.t != null && d.t.gameObject.activeSelf && !d.stolen) saved++;
                        AddReward(0.25f * saved);
                    }
                    break;
            }
            EndEpisode();
        }

        public override void Heuristic(in ActionBuffers actionsOut)
        {
            var c = actionsOut.ContinuousActions;
            var kb = Keyboard.current; if (kb == null) return;
            float v = 0f, h = 0f;
            if (kb.wKey.isPressed) v += 1f; if (kb.sKey.isPressed) v -= 1f;
            if (kb.dKey.isPressed) h += 1f; if (kb.aKey.isPressed) h -= 1f;
            c[0] = v; c[1] = h;
        }
    }
}
