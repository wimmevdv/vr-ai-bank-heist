using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Centrally manages the heist gameplay state, tracking the countdown timer,
/// current monetary score, and progression toward the level's loot objectives.
/// Implements the Singleton pattern.
///
/// FASE 3-flow: 5 minuten timer; speler mag op elk moment escapen via de
/// SafeZone + ExtractionButton. Vroeger escapen = vanzelf minder geld
/// (alleen afgeleverde loot telt). Bij game-einde vuurt OnGameEnded met de
/// eindstats; de end-screen UI (GameUI) abonneert daarop.
/// </summary>
public class HeistManager : MonoBehaviour
{
    public static HeistManager Instance { get; private set; }

    public enum GameResult { Won, LostTimeout }

    public struct HeistEndInfo
    {
        public GameResult result;
        public int totalMoney;
        public int itemsSecured;
        public int totalItems;
        public float timeRemainingAtEnd;
    }

    [Header("Time Management")]
    [Tooltip("Initial time allowed for the heist in seconds. 300 = 5 minuten.")]
    [SerializeField] private float timeRemaining = 300f;

    [Header("Economy & Objectives")]
    [SerializeField] private int currentScore = 0;

    // Public read-only API — bestaande consumers (watch-HUD) blijven werken.
    public int CurrentScore => currentScore;
    public float TimeRemaining => timeRemaining;
    public bool IsGameActive { get; private set; } = true;
    public bool AllLootSecured => securedLootCount >= totalLootCount;

    /// <summary>Vuurt eenmaal wanneer de heist eindigt (win of verlies).</summary>
    public event Action<HeistEndInfo> OnGameEnded;

    private int totalLootCount = 0;
    private int securedLootCount = 0;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        InitializeObjects();
    }

    private void Update()
    {
        if (!IsGameActive) return;
        ProcessCountdown();
    }

    private void InitializeObjects()
    {
        LootItem[] itemsInLevel = UnityEngine.Object.FindObjectsByType<LootItem>(FindObjectsSortMode.None);
        totalLootCount = itemsInLevel.Length;
        Debug.Log($"[HEIST MANAGER] Level initialized. Total loot objectives found: {totalLootCount}");
    }

    private void ProcessCountdown()
    {
        if (timeRemaining > 0)
        {
            timeRemaining -= Time.deltaTime;
        }
        else
        {
            timeRemaining = 0;
            LoseGame("TIME EXPIRED");
        }
    }

    public void SecureLootItem(int value)
    {
        if (!IsGameActive) return;

        currentScore += value;
        securedLootCount++;
        Debug.Log($"[HEIST UPDATE] Secured {securedLootCount}/{totalLootCount} items. Total loot value: €{currentScore}");
    }

    /// <summary>
    /// Validates extraction. Sinds fase 3: GEEN AllLootSecured-eis meer.
    /// Speler mag op elk moment escapen met wat hij heeft (vroeger = minder geld).
    /// </summary>
    public void TryExecuteExtraction(bool isPlayerInSafeZone, AudioSource errorAudio)
    {
        if (!IsGameActive) return;

        if (!isPlayerInSafeZone)
        {
            Debug.LogWarning("[HEIST VALIDATION] Extraction denied: Player is outside the Safe Zone.");
            PlayAudioFeedback(errorAudio);
            return;
        }

        WinGame();
    }

    private void PlayAudioFeedback(AudioSource audioSource)
    {
        if (audioSource != null && !audioSource.isPlaying)
        {
            audioSource.Play();
        }
    }

    public void LoseGame(string reason)
    {
        if (!IsGameActive) return;
        IsGameActive = false;
        Debug.LogWarning($"[GAME OVER] Heist Failed! Reason: {reason}");
        FireEnded(GameResult.LostTimeout);
    }

    public void WinGame()
    {
        if (!IsGameActive) return;
        IsGameActive = false;
        Debug.Log($"[VICTORY] Heist Successful! Total take: €{currentScore}. " +
                  $"Time left: {Mathf.RoundToInt(timeRemaining)}s");
        FireEnded(GameResult.Won);
    }

    private void FireEnded(GameResult result)
    {
        var info = new HeistEndInfo
        {
            result = result,
            totalMoney = currentScore,
            itemsSecured = securedLootCount,
            totalItems = totalLootCount,
            timeRemainingAtEnd = timeRemaining
        };
        OnGameEnded?.Invoke(info);
    }

    /// <summary>
    /// Herlaadt de huidige scene en reset alles. Wordt aangeroepen door de
    /// Play Again-knop. De scene moet in File > Build Settings staan.
    /// </summary>
    public void RestartGame()
    {
        Scene s = SceneManager.GetActiveScene();
        SceneManager.LoadScene(s.buildIndex);
    }
}
