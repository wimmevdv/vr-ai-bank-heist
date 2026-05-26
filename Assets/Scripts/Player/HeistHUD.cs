using TMPro;
using UnityEngine;

/// <summary>
/// Polst HeistManager elke frame en toont score + resterende tijd op
/// twee TextMeshPro velden. Bedoeld voor een World-space Canvas vast op
/// een controller (polshorloge-stijl).
///
/// Horloge-gedrag: het paneel is alleen zichtbaar als de speler met de
/// pols naar zijn gezicht draait. De zichtbaarheidscheck gebruikt de
/// oorspronkelijke (in de Inspector ingestelde) localRotation als
/// referentie voor "waar zou de wijzerplaat-normal staan zonder
/// billboarding" — zo blijft de check correct ongeacht hoe de hand
/// kantelt.
///
/// Billboarding: de wereldrotatie wordt in LateUpdate overschreven zodat
/// het Canvas altijd recht naar de hoofd-camera kijkt met Vector3.up als
/// up-vector. Position erft nog steeds van de parent (de controller),
/// dus het paneeltje volgt de hand zoals een horloge. Maar de tekst
/// staat altijd horizontaal leesbaar.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class HeistHUD : MonoBehaviour
{
    [Header("Text targets")]
    [Tooltip("TMP-veld waarin '€ 12000' komt te staan.")]
    [SerializeField] private TMP_Text scoreText;

    [Tooltip("TMP-veld waarin 'mm:ss' komt te staan.")]
    [SerializeField] private TMP_Text timerText;

    [Header("Format")]
    [Tooltip("Wordt voor de score gezet. Standaard '€ '.")]
    [SerializeField] private string currencyPrefix = "€ ";

    [Tooltip("Wat te tonen als HeistManager nog niet in scène zit.")]
    [SerializeField] private string fallbackText = "--";

    [Header("Horloge-gedrag")]
    [Tooltip("Hoofd-camera waarnaar gekeken wordt. Leeg laten = Camera.main.")]
    [SerializeField] private Transform headCamera;

    [Tooltip("Hoek (graden) tussen pols-canvas en kijkrichting waaronder hij " +
             "nog zichtbaar is. Kleiner = strenger (moet recht ervoor kijken). " +
             "60° is een prettige sweet-spot voor een horloge.")]
    [Range(15f, 90f)]
    [SerializeField] private float visibleAngleDegrees = 60f;

    [Tooltip("Hoe snel hij fade-in/out doet. Hoger = snapt sneller.")]
    [SerializeField] private float fadeSpeed = 8f;

    [Tooltip("Onder deze afstand (m) tot de camera blijft hij altijd onzichtbaar. " +
             "Voorkomt dat hij flicker als de hand toevallig naar je gezicht wijst " +
             "vanaf de overkant van de kamer.")]
    [SerializeField] private float maxDistance = 1.0f;

    [Tooltip("Zet uit als je het Canvas NIET wil laten billboarden naar de " +
             "camera (bv. om de oude pols-vaste oriëntatie te debuggen).")]
    [SerializeField] private bool billboardToCamera = true;

    private CanvasGroup canvasGroup;
    private Quaternion originalLocalRotation;
    private bool hasOriginalRotation;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f; // start onzichtbaar

        // Onthoud de oriëntatie die in de Inspector is gezet — dit is onze
        // referentie voor "naar welke kant kijkt de horloge-wijzerplaat
        // logischerwijs". We gebruiken dit later voor de zichtbaarheidscheck,
        // omdat na billboarding transform.forward niet meer hand-gerelateerd is.
        originalLocalRotation = transform.localRotation;
        hasOriginalRotation = true;

        if (headCamera == null && Camera.main != null)
            headCamera = Camera.main.transform;
    }

    private void Update()
    {
        UpdateText();
        UpdateVisibility();
    }

    /// <summary>
    /// Billboarding in LateUpdate i.p.v. Update: in XR wordt de controller-
    /// transform door het XR-systeem geüpdatet ergens tussen Update en
    /// LateUpdate. Als we hier eerder zouden schrijven, zou de XR-pose-update
    /// onze rotatie overschrijven en zou het Canvas opnieuw met de hand
    /// meekantelen — precies wat we proberen te voorkomen.
    /// </summary>
    private void LateUpdate()
    {
        if (!billboardToCamera) return;
        if (headCamera == null) return;

        Vector3 fromCamera = transform.position - headCamera.position;
        if (fromCamera.sqrMagnitude < 0.0001f) return; // camera staat op exact dezelfde plek

        // forward = van camera naar canvas → canvas-voorkant kijkt naar camera
        // up = Vector3.up → tekst staat altijd horizontaal in de wereld
        transform.rotation = Quaternion.LookRotation(fromCamera, Vector3.up);
    }

    private void UpdateText()
    {
        var hm = HeistManager.Instance;

        if (hm == null)
        {
            if (scoreText != null) scoreText.text = fallbackText;
            if (timerText != null) timerText.text = fallbackText;
            return;
        }

        if (scoreText != null)
            scoreText.text = currencyPrefix + hm.CurrentScore;

        if (timerText != null)
        {
            float t = Mathf.Max(0f, hm.TimeRemaining);
            int minutes = Mathf.FloorToInt(t / 60f);
            int seconds = Mathf.FloorToInt(t % 60f);
            timerText.text = $"{minutes:00}:{seconds:00}";
        }
    }

    private void UpdateVisibility()
    {
        if (headCamera == null)
        {
            if (Camera.main != null) headCamera = Camera.main.transform;
            else { canvasGroup.alpha = 1f; return; }
        }

        Vector3 toCamera = headCamera.position - transform.position;
        float distance = toCamera.magnitude;

        bool shouldShow;
        if (distance > maxDistance || distance < 0.0001f)
        {
            shouldShow = false;
        }
        else
        {
            // Bereken wat de canvas-forward ZOU zijn als we niet billboardden.
            // Dat is de richting waarin de "horloge-wijzerplaat" volgens de
            // Inspector-instelling kijkt, meegedraaid met de parent (controller).
            // Op die manier blijft de zichtbaarheidscheck pols-gebaseerd, ook al
            // is de zichtbare rotatie billboard-gebaseerd.
            Quaternion handBasedRotation = hasOriginalRotation
                ? (transform.parent != null
                    ? transform.parent.rotation * originalLocalRotation
                    : originalLocalRotation)
                : transform.rotation;
            Vector3 handBasedForward = handBasedRotation * Vector3.forward;

            float dot = Vector3.Dot(handBasedForward, toCamera.normalized);
            float minDot = Mathf.Cos(visibleAngleDegrees * Mathf.Deg2Rad);
            shouldShow = dot >= minDot;
        }

        float targetAlpha = shouldShow ? 1f : 0f;
        canvasGroup.alpha = Mathf.MoveTowards(
            canvasGroup.alpha, targetAlpha, fadeSpeed * Time.deltaTime);
    }
}
