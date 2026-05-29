using UnityEngine;
using Wimme.Test;

/// <summary>
/// Brug tussen de AI-laag en de gameplay-laag. Detecteert wanneer
/// <see cref="HeistEnvController.episodeOver"/> op true springt en roept
/// <see cref="HeistManager.LoseCaught"/> aan, zodat de env-controller geen
/// directe afhankelijkheid op de gameplay-laag hoeft te kennen.
/// </summary>
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
