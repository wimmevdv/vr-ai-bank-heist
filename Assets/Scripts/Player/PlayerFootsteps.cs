using UnityEngine;

/// <summary>
/// Speelt voetstap-audio op basis van beweging van de XR Origin.
/// Geeft per voetstap een noise-event aan de AI-bewaker (via NoiseEmitter).
///
/// Drop dit script op XR Origin. Vul de footstepClips array met 2-4 wav/mp3-bestanden.
/// Sluip-tempo (langzaam) maakt zachte voetstappen die de AI minder snel hoort,
/// hard rennen pusht een luider noise-event.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class PlayerFootsteps : MonoBehaviour
{
    [Header("Audio")]
    [Tooltip("2-4 footstep-clips voor variatie (Mp3/Wav). Random gekozen per stap.")]
    [SerializeField] private AudioClip[] footstepClips;
    [Tooltip("Basis-volume van de audio. Wordt vermenigvuldigd met huidige snelheid-factor.")]
    [Range(0f, 1f)]
    [SerializeField] private float baseVolume = 0.6f;

    [Header("Step timing")]
    [Tooltip("Afstand (m) tussen voetstappen bij normaal lopen.")]
    [SerializeField] private float stepDistance = 0.6f;
    [Tooltip("Minimum snelheid waarboven voetstappen geactiveerd worden (m/s). Anders te veel triggers door HMD-wobble.")]
    [SerializeField] private float minSpeedForStep = 0.3f;

    [Header("Noise emission (naar AI)")]
    [Tooltip("Optioneel — leeg laten = geen noise gepusht. Vul met NoiseEmitter op dit object voor AI-detectie.")]
    [SerializeField] private NoiseEmitter noiseEmitter;
    [Tooltip("Loudness bij stilstaan / sluipen (snelheid <1 m/s).")]
    [Range(0f, 1f)]
    [SerializeField] private float quietLoudness = 0.15f;
    [Tooltip("Loudness bij volle snelheid (snelheid >3 m/s).")]
    [Range(0f, 1f)]
    [SerializeField] private float loudLoudness = 0.7f;

    private AudioSource audioSource;
    private Vector3 lastPosition;
    private float distanceAccumulator;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f; // 3D — anders is positioning weird
        lastPosition = transform.position;
    }

    private void Update()
    {
        // Horizontal-only delta: hoogteveranderingen (springen/duiken) tellen niet als stap.
        Vector3 delta = transform.position - lastPosition;
        delta.y = 0f;
        float distMoved = delta.magnitude;
        lastPosition = transform.position;

        float speed = distMoved / Mathf.Max(Time.deltaTime, 0.0001f);
        if (speed < minSpeedForStep) return;

        distanceAccumulator += distMoved;
        if (distanceAccumulator < stepDistance) return;

        distanceAccumulator = 0f;
        PlayFootstep(speed);
    }

    private void PlayFootstep(float speed)
    {
        // Audio
        if (footstepClips != null && footstepClips.Length > 0 && audioSource != null)
        {
            var clip = footstepClips[Random.Range(0, footstepClips.Length)];
            float volumeFactor = Mathf.Clamp01(speed / 3.5f);
            audioSource.PlayOneShot(clip, baseVolume * (0.5f + 0.5f * volumeFactor));
        }

        // AI noise
        if (noiseEmitter != null)
        {
            float loudness = Mathf.Lerp(quietLoudness, loudLoudness, Mathf.Clamp01((speed - 1f) / 2.5f));
            noiseEmitter.Emit(loudness);
        }
    }
}
