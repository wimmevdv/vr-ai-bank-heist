using TMPro;
using UnityEngine;

/// <summary>
/// Polst HeistManager elke frame en toont score + resterende tijd op
/// twee TextMeshPro velden. Bedoeld voor een World-space Canvas vast op
/// een controller (polshorloge-stijl).
///
/// Horloge-gedrag: het paneel is alleen zichtbaar als de speler met de
/// pols naar zijn gezicht draait. Dat gebeurt door de hoek te meten
/// tussen de canvas-normal (transform.forward — de richting waarin de
/// tekst leesbaar is) en de richting naar de hoofd-camera. Boven een
/// drempel fadet hij in, eronder fadet hij uit.
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

    private CanvasGroup canvasGroup;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f; // start onzichtbaar

        if (headCamera == null && Camera.main != null)
            headCamera = Camera.main.transform;
    }

    private void Update()
    {
        UpdateText();
        UpdateVisibility();
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
        // Geen camera-referentie (gebeurt soms 1 frame na scene-load in VR) →
        // probeer hem opnieuw te pakken, anders gewoon vol zichtbaar tonen.
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
            // transform.forward = de canvas-normal (de richting waar de tekst
            // naartoe "wijst"). Dot ≈ 1 → canvas wijst recht naar camera.
            float dot = Vector3.Dot(transform.forward, toCamera.normalized);
            float minDot = Mathf.Cos(visibleAngleDegrees * Mathf.Deg2Rad);
            shouldShow = dot >= minDot;
        }

        float targetAlpha = shouldShow ? 1f : 0f;
        canvasGroup.alpha = Mathf.MoveTowards(
            canvasGroup.alpha, targetAlpha, fadeSpeed * Time.deltaTime);
    }
}
