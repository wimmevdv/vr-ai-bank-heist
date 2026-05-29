using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Wimme.Test
{
    /// <summary>
    /// Beheert de trainings- en gameplay-omgeving rond de bewaker: deposit-state,
    /// episode-timer, audio-events en optionele domain-randomisatie van deposit-
    /// posities. Bij live-play met een VR-speler wordt de step-limiet uitgeschakeld
    /// zodat de heist door <see cref="HeistManager"/> wordt afgesloten i.p.v. door
    /// een max-step.
    /// </summary>
    public class HeistEnvController : MonoBehaviour
    {
        [Header("Refs")]
        public Transform guardSpawn;
        public Transform[] thiefSpawns;
        public Transform dropOffZone;
        public BankGuardAgent guard;
        public ScriptedThief thief;
        [Tooltip("Drag the VR player (XR Origin root) here for live play. Leave empty for training with ScriptedThief.")]
        public Transform vrPlayer;
        public Transform[] depositSlots;

        public Transform thiefTarget => vrPlayer != null ? vrPlayer : (thief != null ? thief.transform : null);

        [Header("Episode")]
        public float episodeSeconds = 60f;
        public int maxDepositsActive = 6;

        [Header("Audio events")]
        public float thiefMovingNoiseRadius = 6f;
        public float alarmNoiseRadius = 20f;
        public float distractorChancePerSec = 0.05f;

        [Header("Distractor sources")]
        public Transform[] distractorPoints;

        [Header("Domain randomization")]
        [Tooltip("Verplaats actieve deposits naar willekeurige NavMesh-punten bij elk episode-begin, zodat de policy niet op vaste posities kan trainen.")]
        public bool randomizeDepositPositions = true;
        [Tooltip("World-space bounds waarbinnen NavMesh-samples worden gepakt. Moet ruimer zijn dan de begaanbare ruimte.")]
        public Bounds randomizeBounds = new Bounds(Vector3.zero, new Vector3(80f, 4f, 80f));

        // ----- Runtime state -----
        public List<DepositState> deposits { get; private set; } = new List<DepositState>();
        public NoiseEvent lastNoise { get; private set; }
        public float timeLeft { get; private set; }
        public bool episodeOver { get; private set; }

        public class DepositState
        {
            public Transform t;
            public bool stolen;
            public bool alarmed;
            public float alarmEndsAt;
        }

        public class NoiseEvent
        {
            public Vector3 position;
            public float loudness;       // [0..1]
            public float timeEmitted;    // Time.time
            public bool valid;
        }

        void Awake()
        {
            for (int i = 0; i < depositSlots.Length; i++)
            {
                deposits.Add(new DepositState { t = depositSlots[i] });
            }
            lastNoise = new NoiseEvent();
        }

        public void BeginEpisode(int activeDepositCount, bool thiefEnabled, bool audioEnabled, bool alarmsEnabled)
        {
            episodeOver = false;
            timeLeft = episodeSeconds;
            lastNoise.valid = false;

            for (int i = 0; i < deposits.Count; i++)
            {
                var d = deposits[i];
                d.stolen = false;
                d.alarmed = false;
                d.alarmEndsAt = 0f;
                bool active = i < activeDepositCount;
                if (d.t != null) d.t.gameObject.SetActive(active);

                if (active && randomizeDepositPositions && d.t != null)
                {
                    if (TrySampleNavMeshPoint(out var pos))
                        d.t.position = pos + Vector3.up * 0.25f;
                }
            }

            if (alarmsEnabled && activeDepositCount > 0)
            {
                int idx = Random.Range(0, activeDepositCount);
                deposits[idx].alarmed = true;
                deposits[idx].alarmEndsAt = Time.time + Random.Range(8f, 15f);
                RegisterNoise(deposits[idx].t.position, 1f);
            }

            // ScriptedThief alleen actief tijdens training; bij live-play bestuurt
            // de menselijke VR-speler de thiefTarget.
            if (vrPlayer == null && thief != null)
            {
                thief.gameObject.SetActive(thiefEnabled);
                if (thiefEnabled)
                {
                    Vector3 spawnPos = Vector3.zero;
                    bool found = false;
                    if (randomizeDepositPositions && TrySampleNavMeshPoint(out var rndPos))
                    {
                        spawnPos = rndPos;
                        found = true;
                    }
                    if (!found && thiefSpawns != null && thiefSpawns.Length > 0)
                    {
                        spawnPos = thiefSpawns[Random.Range(0, thiefSpawns.Length)].position;
                        found = true;
                    }
                    if (found) thief.ResetAt(spawnPos, this);
                }
            }
        }

        void Update()
        {
            if (episodeOver) return;
            timeLeft -= Time.deltaTime;

            foreach (var d in deposits)
            {
                if (d.alarmed && Time.time > d.alarmEndsAt) d.alarmed = false;
            }

            if (distractorPoints != null && distractorPoints.Length > 0)
            {
                if (Random.value < distractorChancePerSec * Time.deltaTime)
                {
                    var dp = distractorPoints[Random.Range(0, distractorPoints.Length)];
                    RegisterNoise(dp.position, 0.3f);
                }
            }

            int stolen = 0;
            int active = 0;
            foreach (var d in deposits)
            {
                if (d.t != null && d.t.gameObject.activeSelf) active++;
                if (d.stolen) stolen++;
            }
            // Bij live-play stopt HeistManager de heist; geen step-/TimeUp-einde hier.
            if (vrPlayer != null) return;
            if (timeLeft <= 0f) EndEpisode(GuardOutcome.TimeUp);
            else if (active > 0 && stolen >= active) EndEpisode(GuardOutcome.AllStolen);
        }

        public enum GuardOutcome { TimeUp, AllStolen, Caught }

        public void EndEpisode(GuardOutcome outcome)
        {
            if (episodeOver) return;
            episodeOver = true;
            if (guard != null) guard.OnEnvironmentEnded(outcome);
        }

        /// <summary>Registreert één geluid-event dat de bewaker bij zijn volgende observatie meeneemt.</summary>
        public void RegisterNoise(Vector3 worldPos, float loudness)
        {
            lastNoise.position = worldPos;
            lastNoise.loudness = Mathf.Clamp01(loudness);
            lastNoise.timeEmitted = Time.time;
            lastNoise.valid = true;
        }

        public DepositState FindNearestUntouched(Vector3 from)
        {
            DepositState best = null; float bestSqr = float.MaxValue;
            foreach (var d in deposits)
            {
                if (d.t == null || !d.t.gameObject.activeSelf || d.stolen) continue;
                float s = (d.t.position - from).sqrMagnitude;
                if (s < bestSqr) { bestSqr = s; best = d; }
            }
            return best;
        }

        public void MarkStolen(Transform depositT)
        {
            foreach (var d in deposits)
            {
                if (d.t == depositT) { d.stolen = true; if (guard != null) guard.OnItemStolen(d); break; }
            }
        }

        private bool TrySampleNavMeshPoint(out Vector3 result)
        {
            for (int i = 0; i < 20; i++)
            {
                Vector3 sample = randomizeBounds.center + new Vector3(
                    Random.Range(-randomizeBounds.extents.x, randomizeBounds.extents.x),
                    Random.Range(-randomizeBounds.extents.y, randomizeBounds.extents.y),
                    Random.Range(-randomizeBounds.extents.z, randomizeBounds.extents.z));
                if (NavMesh.SamplePosition(sample, out var hit, 5f, NavMesh.AllAreas))
                {
                    result = hit.position;
                    return true;
                }
            }
            result = Vector3.zero;
            return false;
        }
    }
}
