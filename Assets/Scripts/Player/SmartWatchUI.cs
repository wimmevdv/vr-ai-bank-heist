using UnityEngine;
using TMPro;

/// <summary>
/// Controls the diegetic UI on the player's wrist.
/// Pulls state data from the Singleton HeistManager.
/// </summary>
public class SmartWatchUI : MonoBehaviour
{
    [Header("UI Elements")]
    [Tooltip("The TextMeshPro element displaying the countdown timer.")]
    [SerializeField] private TextMeshProUGUI timeText;

    [Tooltip("The TextMeshPro element displaying the loot progress.")]
    [SerializeField] private TextMeshProUGUI lootProgressText;

    private void Update()
    {
        if (HeistManager.Instance == null) return;

        UpdateTimerDisplay();
        UpdateLootDisplay();
    }

    /// <summary>
    /// Vertaalt de ruwe seconden (float) naar een leesbare digitale MM:SS klok.
    /// </summary>
    private void UpdateTimerDisplay()
    {
        float time = HeistManager.Instance.TimeRemaining;

        int minutes = Mathf.FloorToInt(time / 60f);
        int seconds = Mathf.FloorToInt(time % 60f);

        timeText.text = $"{minutes:00}:{seconds:00}";

        if (time <= 30f)
        {
            timeText.color = Color.red;
        }
    }

    /// <summary>
    /// Toont de voortgang van de gestolen objecten
    /// </summary>
    private void UpdateLootDisplay()
    {
        int current = HeistManager.Instance.SecuredLootCount;
        int total = HeistManager.Instance.TotalLootCount;
        int score = HeistManager.Instance.CurrentScore;

        lootProgressText.text = $"Loot: {current} / {total}\nVal: €{score}";
    }
}