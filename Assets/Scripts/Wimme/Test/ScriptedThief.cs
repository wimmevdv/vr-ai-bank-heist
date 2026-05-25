using UnityEngine;
using UnityEngine.AI;

namespace Wimme.Test
{
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

        public void ResetAt(Vector3 pos, HeistEnvController controller)
        {
            env = controller;

            // Find the nearest valid point on the NavMesh — prevents the
            // "Failed to create agent because there is no valid NavMesh"
            // spam when spawn transforms drift slightly off the mesh.
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
                // After moving, try to attach to the NavMesh on next frame
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

            // Emit noise from movement
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
                // Stealing triggers an alarm-like noise
                env.RegisterNoise(carrying.t.position, 0.6f);
            }
        }

        private void TickStealing()
        {
            if (Time.time < waitUntil) return;
            env.MarkStolen(carrying.t);
            nav.speed = runMoveSpeed;          // panicked retreat after grabbing
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
