using UnityEngine;
using UnityEngine.AI;

namespace Wimme.Test
{
    /// <summary>
    /// NavMesh-AI die de speler vervangt tijdens training. Doorloopt een state-machine
    /// (kies-deposit → steel → ren naar drop-off → herhaal) en produceert voetstap-
    /// noise zodat de bewaker dezelfde audio-signalen krijgt als bij een menselijke speler.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class ScriptedThief : MonoBehaviour
    {
        public float stealSeconds = 2.5f;
        public float dropSeconds = 1f;
        public float quietMoveSpeed = 1.5f;
        public float runMoveSpeed = 3.5f;

        private NavMeshAgent nav;
        private HeistEnvController env;
        private HeistEnvController.DepositState carrying;
        private float waitUntil;
        private enum State { GoSteal, Stealing, GoDrop, Dropping, Idle }
        private State state;

        void Awake() { nav = GetComponent<NavMeshAgent>(); }

        /// <summary>Herstart de dief op de dichtstbijzijnde NavMesh-positie en kiest een nieuw doel.</summary>
        public void ResetAt(Vector3 pos, HeistEnvController controller)
        {
            env = controller;

            // SamplePosition voorkomt het "Failed to create agent — no valid NavMesh"-
            // spam wanneer spawn-transforms net naast het mesh liggen.
            Vector3 safePos = pos;
            if (NavMesh.SamplePosition(pos, out var hit, 5.0f, NavMesh.AllAreas))
                safePos = hit.position;

            if (!nav.enabled) nav.enabled = true;

            if (nav.isOnNavMesh)
            {
                nav.Warp(safePos);
            }
            else
            {
                transform.position = safePos;
                if (!nav.Warp(safePos))
                    Debug.LogWarning($"[ScriptedThief] Could not warp onto NavMesh near {safePos}");
            }

            carrying = null;
            state = State.GoSteal;
            ChooseNextTarget();
        }

        void Update()
        {
            if (env == null || env.episodeOver) return;

            switch (state)
            {
                case State.GoSteal: TickGoSteal(); break;
                case State.Stealing: TickStealing(); break;
                case State.GoDrop: TickGoDrop(); break;
                case State.Dropping: TickDropping(); break;
            }

            if (nav.velocity.sqrMagnitude > 0.1f)
            {
                float loud = nav.velocity.magnitude / Mathf.Max(runMoveSpeed, 0.01f);
                if (Random.value < 0.1f) env.RegisterNoise(transform.position, 0.3f + 0.5f * loud);
            }
        }

        private void ChooseNextTarget()
        {
            carrying = env.FindNearestUntouched(transform.position);
            if (carrying == null) { state = State.Idle; return; }
            nav.speed = quietMoveSpeed;
            nav.SetDestination(carrying.t.position);
            state = State.GoSteal;
        }

        private void TickGoSteal()
        {
            if (carrying == null || carrying.stolen) { ChooseNextTarget(); return; }
            if (!nav.pathPending && nav.remainingDistance < 0.8f)
            {
                state = State.Stealing;
                waitUntil = Time.time + stealSeconds;
                env.RegisterNoise(carrying.t.position, 0.6f);
            }
        }

        private void TickStealing()
        {
            if (Time.time < waitUntil) return;
            env.MarkStolen(carrying.t);
            // Versnel naar runMoveSpeed na de greep: paniek-retreat oogt menselijker
            // en geeft de bewaker een hoorbaar luider signaal om op te reageren.
            nav.speed = runMoveSpeed;
            nav.SetDestination(env.dropOffZone.position);
            state = State.GoDrop;
        }

        private void TickGoDrop()
        {
            if (!nav.pathPending && nav.remainingDistance < 1.0f)
            {
                state = State.Dropping;
                waitUntil = Time.time + dropSeconds;
            }
        }

        private void TickDropping()
        {
            if (Time.time < waitUntil) return;
            ChooseNextTarget();
        }
    }
}
