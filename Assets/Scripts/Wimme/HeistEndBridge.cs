using UnityEngine;
using Wimme.Test;

/// <summary>
/// Brug tussen <see cref="HeistEnvController"/> (bewaker-bookkeeping van
/// teamgenoot) en <see cref="HeistManager"/> (speler-game-loop).
///
/// Polst <c>env.episodeOver</c> elke frame en interpreteert de overgang
/// false → true als een Caught-event. Tijdens live-spel garandeert
/// HeistEnvController dat alleen Caught EndEpisode kan triggeren (de Update
/// in env doet 'if (vrPlayer != null) return;' boven TimeUp/AllStolen), dus
/// elke transitie is per definitie een betrappen.
///
/// Polling i.p.v. een event-API toevoegen aan HeistEnvController, zodat geen
/// enkele regel van de AI-code aangeraakt hoeft te worden (frozen volgens
/// instructies van teamgenoot).
///
/// Setup: één leeg GameObject "HeistEndBridge" in de scene. Sleep de
/// HeistEnvController in het env-veld.
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
