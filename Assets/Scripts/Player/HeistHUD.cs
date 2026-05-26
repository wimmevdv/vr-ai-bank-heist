using TMPro;
using UnityEngine;

/// <summary>
/// Polst HeistManager elke frame en toont score + resterende tijd op
/// twee TextMeshPro velden. Bedoeld voor een World-space Canvas vast op
/// een controller (polshorloge-stijl), maar werkt op elke Canvas.
/// </summary>
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

    private void Update()
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
}
