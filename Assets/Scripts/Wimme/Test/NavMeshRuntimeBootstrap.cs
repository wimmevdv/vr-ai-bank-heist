using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace Wimme.Test
{
    /// <summary>
    /// Rebakes every NavMeshSurface in the loaded scene at player startup, then
    /// kicks any NavMeshAgent that failed to attach. We use this in standalone
    /// training builds because the editor-baked NavMeshData assets go stale
    /// whenever EnvRoot.prefab geometry changes without a fresh bake, and that
    /// shows up as "Failed to create agent because it is not close enough to
    /// the NavMesh" spam plus the trainer timing out before any episode runs.
    /// </summary>
    public static class NavMeshRuntimeBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RebakeAllSurfaces()
        {
            // The editor uses inspector-baked data and we don't want to clobber
            // it every Play. Only run in standalone player builds.
            if (Application.isEditor) return;

            // Only bake surfaces that are missing their NavMeshData — when the
            // editor-baked assets are still valid (matching prefab geometry),
            // re-baking all 384 surfaces on startup takes ~30s and triggers the
            // mlagents UnityTimeOutException. Keep this as a recovery path for
            // future stale-bake scenarios but skip the happy path.
            var surfaces = Object.FindObjectsByType<NavMeshSurface>(FindObjectsSortMode.None);
            int baked = 0;
            foreach (var s in surfaces)
            {
                if (s.navMeshData == null)
                {
                    s.BuildNavMesh();
                    baked++;
                }
            }
            if (baked > 0)
                Debug.Log($"[NavMeshRuntimeBootstrap] Rebaked {baked}/{surfaces.Length} NavMeshSurfaces missing data.");

            int retried = 0;
            foreach (var agent in Object.FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None))
            {
                if (agent.isOnNavMesh) continue;
                bool was = agent.enabled;
                agent.enabled = false;
                agent.enabled = was;
                retried++;
            }
            if (retried > 0)
                Debug.Log($"[NavMeshRuntimeBootstrap] Re-toggled {retried} NavMeshAgents to retry attach.");
        }
    }
}
