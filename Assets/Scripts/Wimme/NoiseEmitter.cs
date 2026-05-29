using UnityEngine;
using Wimme.Test;

/// <summary>
/// Stuurt geluid-events naar de AI-bewaker via <see cref="HeistEnvController.RegisterNoise"/>.
/// Werkt in twee modi: handmatig (<see cref="Emit()"/> aanroepen via UnityEvent of code)
/// of automatisch (gekoppelde <see cref="AudioSource"/> triggert één event bij elke
/// transitie van niet-spelend naar spelend).
/// </summary>
public class NoiseEmitter : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("Mag leeg blijven — vindt dan automatisch HeistEnvController in scene.")]
    [SerializeField] private HeistEnvController env;
    [Tooltip("Optioneel — pusht noise automatisch als deze AudioSource begint te spelen.")]
    [SerializeField] private AudioSource audioSource;

    [Header("Noise")]
    [Range(0f, 1f)]
    [Tooltip("Hoe luid voor de AI. 0 = onhoorbaar, 1 = alarm-niveau.")]
    [SerializeField] private float loudness = 0.6f;

    private bool wasPlaying;

    private void Awake()
    {
        if (env == null) env = FindAnyObjectByType<HeistEnvController>();
    }

    private void Update()
    {
        if (audioSource == null) return;
        bool playing = audioSource.isPlaying;
        if (playing && !wasPlaying) Emit();
        wasPlaying = playing;
    }

    /// <summary>Stuurt één noise-event naar de AI op deze positie, met de Inspector-loudness.</summary>
    public void Emit()
    {
        if (env == null) return;
        env.RegisterNoise(transform.position, loudness);
    }

    /// <summary>Stuurt één noise-event met een ge-overschreven loudness (0-1).</summary>
    public void Emit(float overrideLoudness)
    {
        if (env == null) return;
        env.RegisterNoise(transform.position, Mathf.Clamp01(overrideLoudness));
    }
}
