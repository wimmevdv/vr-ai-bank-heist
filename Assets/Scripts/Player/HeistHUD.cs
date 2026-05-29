using TMPro;
using UnityEngine;

/// <summary>
/// Polsband-HUD die geld en resterende tijd toont. Fadet alleen in wanneer de
/// speler er recht naar kijkt en het canvas dichtbij genoeg is, zodat de tekst
/// niet stoort tijdens normaal spelen.
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
    [Range(15f, 180f)]
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
        canvasGroup.alpha = 0f;

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
    private void LateUpdate()
    {
        if (!billboardToCamera) return;
        if (headCamera == null) return;

        Vector3 fromCamera = transform.position - headCamera.position;
        if (fromCamera.sqrMagnitude < 0.0001f) return;

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
