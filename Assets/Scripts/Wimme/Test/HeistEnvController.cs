using System.Collections.Generic;
using UnityEngine;

namespace Wimme.Test
{
    public class HeistEnvController : MonoBehaviour
    {
        [Header("Refs")]
        public Transform guardSpawn;
        public Transform[] thiefSpawns;
        public Transform dropOffZone;
        public BankGuardAgent guard;
        public ScriptedThief thief;
        public Transform[] depositSlots;

        [Header("Episode")]
        public float episodeSeconds = 60f;
        public int maxDepositsActive = 6;

        [Header("Audio events")]
        public float thiefMovingNoiseRadius = 6f;
        public float alarmNoiseRadius = 20f;
        public float distractorChancePerSec = 0.05f;

        [Header("Distractor sources")]
        public Transform[] distractorPoints;

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

            // Reset deposits: enable the first N, rest hidden
            for (int i = 0; i < deposits.Count; i++)
            {
                var d = deposits[i];
                d.stolen = false;
                d.alarmed = false;
                d.alarmEndsAt = 0f;
                bool active = i < activeDepositCount;
                if (d.t != null) d.t.gameObject.SetActive(active);
            }

            // Random alarm: pick at most one deposit to alarm partway through
            if (alarmsEnabled && activeDepositCount > 0)
            {
                int idx = Random.Range(0, activeDepositCount);
                deposits[idx].alarmed = true;
                deposits[idx].alarmEndsAt = Time.time + Random.Range(8f, 15f);
                RegisterNoise(deposits[idx].t.position, 1f);
            }

            // Reset thief
            if (thief != null)
            {
                thief.gameObject.SetActive(thiefEnabled);
                if (thiefEnabled && thiefSpawns != null && thiefSpawns.Length > 0)
                {
                    var s = thiefSpawns[Random.Range(0, thiefSpawns.Length)];
                    thief.ResetAt(s.position, this);
                }
            }
        }

        void Update()
        {
            if (episodeOver) return;
            timeLeft -= Time.deltaTime;

            // Alarm timeout
            foreach (var d in deposits)
            {
                if (d.alarmed && Time.time > d.alarmEndsAt) d.alarmed = false;
            }

            // Random distractor noises
            if (distractorPoints != null && distractorPoints.Length > 0)
            {
                if (Random.value < distractorChancePerSec * Time.deltaTime)
                {
                    var dp = distractorPoints[Random.Range(0, distractorPoints.Length)];
                    RegisterNoise(dp.position, 0.3f);
                }
            }

            // Episode end: timer out, or all deposits stolen
            int stolen = 0;
            int active = 0;
            foreach (var d in deposits)
            {
                if (d.t != null && d.t.gameObject.activeSelf) active++;
                if (d.stolen) stolen++;
            }
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
    }
}
