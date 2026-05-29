using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Singleton die de heist-state beheert: countdown-timer, score, en win/verlies-events.
/// De speler kan op elk moment extracten via SafeZone + ExtractionButton — alleen
/// afgeleverde loot telt mee in de eindscore. Vuurt <see cref="OnGameEnded"/> bij
/// elk eind-pad (win, timeout, of gevangen door bewaker).
/// </summary>
public class HeistManager : MonoBehaviour
{
    public static HeistManager Instance { get; private set; }

    public enum GameResult { Won, LostTimeout, LostCaught }

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

    public int CurrentScore => currentScore;
    public float TimeRemaining => timeRemaining;
    public bool IsGameActive { get; private set; } = true;
    public bool AllLootSecured => securedLootCount >= totalLootCount;

    /// <summary>Vuurt eenmaal per spel, op elk eind-pad (win of verlies).</summary>
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

    /// <summary>Registreert een afgeleverde loot-item. Aangeroepen door <see cref="DropZone"/>.</summary>
    public void SecureLootItem(int value)
    {
        if (!IsGameActive) return;

        currentScore += value;
        securedLootCount++;
        Debug.Log($"[HEIST UPDATE] Secured {securedLootCount}/{totalLootCount} items. Total loot value: €{currentScore}");
    }

    /// <summary>
    /// Valideert een extractie-poging. De speler moet binnen de SafeZone staan;
    /// anders speelt het error-geluid af en blijft de heist actief.
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

    /// <summary>Eindigt de heist met resultaat TIJD-VOORBIJ.</summary>
    public void LoseGame(string reason)
    {
        if (!IsGameActive) return;
        IsGameActive = false;
        Debug.LogWarning($"[GAME OVER] Heist Failed! Reason: {reason}");
        FireEnded(GameResult.LostTimeout);
    }

    /// <summary>Eindigt de heist met resultaat BETRAPT.</summary>
    public void LoseCaught()
    {
        if (!IsGameActive) return;
        IsGameActive = false;
        Debug.LogWarning("[GAME OVER] Heist Failed! Reason: CAUGHT BY GUARD");
        FireEnded(GameResult.LostCaught);
    }

    /// <summary>Eindigt de heist met resultaat ESCAPE GELUKT.</summary>
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

        DisableGuard();
    }

    private void DisableGuard()
    {
        var guard = UnityEngine.Object.FindAnyObjectByType<Wimme.Test.BankGuardAgent>();
        if (guard == null) return;
        guard.enabled = false;
        if (guard.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    /// <summary>Herlaadt de huidige scene; vereist dat die in Build Settings staat.</summary>
    public void RestartGame()
    {
        Scene s = SceneManager.GetActiveScene();
        SceneManager.LoadScene(s.buildIndex);
    }
}
