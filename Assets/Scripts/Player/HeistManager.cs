using UnityEngine;

/// <summary>
/// Centrally manages the heist gameplay state, tracking the countdown timer,
/// current monetary score, and progression toward the level's loot objectives.
/// Implements the Singleton pattern.
/// </summary>
public class HeistManager : MonoBehaviour
{
    public static HeistManager Instance { get; private set; }

    [Header("Time Management")]
    [Tooltip("Initial time allowed for the heist in seconds.")]
    [SerializeField] private float timeRemaining = 180f;

    [Header("Economy & Objectives")]
    [SerializeField] private int currentScore = 0;

    // Encapsulated properties for secure read-only access from other scripts
    public int CurrentScore => currentScore;
    public float TimeRemaining => timeRemaining;
    public bool IsGameActive { get; private set; } = true;
    public bool AllLootSecured => securedLootCount >= totalLootCount;

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
        LootItem[] itemsInLevel = Object.FindObjectsByType<LootItem>(FindObjectsSortMode.None);
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
    /// Validates all heist objectives and player positioning upon interaction.
    /// Triggers either the victory sequence or sensory error feedback.
    /// </summary>
    /// <param name="isPlayerInSafeZone">Context provided by the SafeZone trigger status.</param>
    /// <param name="errorAudio">The audio emitter to play feedback from if validation fails.</param>
    public void TryExecuteExtraction(bool isPlayerInSafeZone, AudioSource errorAudio)
    {
        if (!IsGameActive) return;

        // Condition 1: Is the player physically standing inside the escape vehicle/zone?
        if (!isPlayerInSafeZone)
        {
            Debug.LogWarning("[HEIST VALIDATION] Extraction denied: Player is outside the Safe Zone.");
            PlayAudioFeedback(errorAudio);
            return;
        }

        // Condition 2: Has all loot from the scene been securely deposited into a DropZone?
        if (!AllLootSecured)
        {
            Debug.LogWarning("[HEIST VALIDATION] Extraction denied: Missing required loot objectives.");
            PlayAudioFeedback(errorAudio);
            return;
        }

        // Success: All conditions validated successfully
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
        IsGameActive = false;
        Debug.LogWarning($"[GAME OVER] Heist Failed! Reason: {reason}");
    }

    public void WinGame()
    {
        IsGameActive = false;
        Debug.Log($"[VICTORY] Heist Successful! Total take: €{currentScore}. Time left: {Mathf.RoundToInt(timeRemaining)}s");
    }
}