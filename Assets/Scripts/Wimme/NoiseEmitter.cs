using UnityEngine;
using Wimme.Test;

/// <summary>
/// Drop op een GameObject dat geluid moet maken in de game-wereld
/// (alarmen, kassa, glas, deur, voetstap-eindpunten van NPCs, ...).
/// Roept HeistEnvController.RegisterNoise aan zodat de AI-bewaker reageert.
///
/// Twee gebruiksmodi:
///  1) Handmatig — call Emit() vanuit een UnityEvent of code-call.
///  2) Auto — koppel een AudioSource: elke keer dat clip start (isPlaying gaat
///     van false naar true) wordt eenmalig een noise-event geregistreerd.
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

    /// <summary>Stuurt eenmalig een noise-event naar de AI op deze positie.</summary>
    public void Emit()
    {
        if (env == null) return;
        env.RegisterNoise(transform.position, loudness);
    }

    /// <summary>Variant met override-loudness (bv. voor luidheid afhankelijk van speler-actie).</summary>
    public void Emit(float overrideLoudness)
    {
        if (env == null) return;
        env.RegisterNoise(transform.position, Mathf.Clamp01(overrideLoudness));
    }
}
