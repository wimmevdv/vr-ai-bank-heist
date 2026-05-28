using UnityEngine;
using Wimme.Test;


public class HeistEndBridge : MonoBehaviour
{
    [SerializeField] private HeistEnvController env;

    private bool lastEpisodeOver;

    private void Awake()
    {
        if (env == null) env = FindAnyObjectByType<HeistEnvController>();
        if (env == null)
            Debug.LogWarning("[HeistEndBridge] Geen HeistEnvController in scene gevonden. " +
                             "Catch-events bereiken HeistManager niet.");
    }

    private void Update()
    {
        if (env == null) return;

        bool now = env.episodeOver;
        if (now && !lastEpisodeOver)
        {
            if (HeistManager.Instance != null)
                HeistManager.Instance.LoseCaught();
            else
                Debug.LogWarning("[HeistEndBridge] EndEpisode gedetecteerd maar geen HeistManager.Instance.");
        }
        lastEpisodeOver = now;
    }
}
