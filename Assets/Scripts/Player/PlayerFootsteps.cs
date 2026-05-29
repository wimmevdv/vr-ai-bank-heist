using System.Collections;
using UnityEngine;

/// <summary>
/// Speelt voetstap-audio op basis van XR Origin-beweging en pusht per stap een
/// noise-event naar de AI-bewaker (luidheid schaalt met snelheid). Bij gebruik
/// van een lange multi-step clip kan <c>clipPlayDuration</c> + random offset
/// per stap een korte slice afspelen in plaats van de hele clip.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class PlayerFootsteps : MonoBehaviour
{
    [Header("Audio")]
    [Tooltip("1-4 footstep-clips voor variatie (Mp3/Wav). Random gekozen per stap.")]
    [SerializeField] private AudioClip[] footstepClips;
    [Tooltip("Basis-volume van de audio. Wordt vermenigvuldigd met huidige snelheid-factor.")]
    [Range(0f, 1f)]
    [SerializeField] private float baseVolume = 0.6f;

    [Header("Clip slicing (voor lange multi-step clips)")]
    [Tooltip("Hoe lang per stap afspelen. 0 = volledige clip (gebruik voor single-step clips). " +
             "Voor multi-step clips zet op 0.4-0.5 sec.")]
    [SerializeField] private float clipPlayDuration = 0f;
    [Tooltip("Pak een willekeurig startmoment in de clip i.p.v. altijd vanaf begin. " +
             "Geeft variatie als je 1 lange clip gebruikt.")]
    [SerializeField] private bool useRandomClipOffset = true;
    [Tooltip("Korte fade-out (sec) bij stop om klik-geluid te voorkomen. 0 = harde stop.")]
    [Range(0f, 0.1f)]
    [SerializeField] private float clipFadeOut = 0.03f;

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
    private Coroutine stopRoutine;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 1f;
        lastPosition = transform.position;
    }

    private void Update()
    {
        // Y-as wordt genegeerd zodat bukken/springen niet als een voetstap telt.
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
        if (footstepClips != null && footstepClips.Length > 0 && audioSource != null)
        {
            var clip = footstepClips[Random.Range(0, footstepClips.Length)];
            float volumeFactor = Mathf.Clamp01(speed / 3.5f);
            float volume = baseVolume * (0.5f + 0.5f * volumeFactor);

            if (clipPlayDuration <= 0f || clipPlayDuration >= clip.length)
            {
                audioSource.PlayOneShot(clip, volume);
            }
            else
            {
                // Slice-mode: gebruik Play() i.p.v. PlayOneShot zodat StopAfter de
                // clip op tijd kan afkappen voor de volgende stap.
                if (stopRoutine != null) StopCoroutine(stopRoutine);
                audioSource.Stop();
                audioSource.clip = clip;
                audioSource.volume = volume;

                float maxStart = Mathf.Max(0f, clip.length - clipPlayDuration - 0.01f);
                audioSource.time = useRandomClipOffset && maxStart > 0f
                    ? Random.Range(0f, maxStart)
                    : 0f;

                audioSource.Play();
                stopRoutine = StartCoroutine(StopAfter(clipPlayDuration, volume));
            }
        }

        if (noiseEmitter != null)
        {
            float loudness = Mathf.Lerp(quietLoudness, loudLoudness, Mathf.Clamp01((speed - 1f) / 2.5f));
            noiseEmitter.Emit(loudness);
        }
    }

    private IEnumerator StopAfter(float seconds, float originalVolume)
    {
        if (clipFadeOut <= 0f)
        {
            yield return new WaitForSeconds(seconds);
            audioSource.Stop();
        }
        else
        {
            float fadeStart = Mathf.Max(0f, seconds - clipFadeOut);
            yield return new WaitForSeconds(fadeStart);
            float t = 0f;
            while (t < clipFadeOut)
            {
                t += Time.deltaTime;
                if (audioSource != null) audioSource.volume = Mathf.Lerp(originalVolume, 0f, t / clipFadeOut);
                yield return null;
            }
            if (audioSource != null)
            {
                audioSource.Stop();
                audioSource.volume = originalVolume;
            }
        }
        stopRoutine = null;
    }
}
