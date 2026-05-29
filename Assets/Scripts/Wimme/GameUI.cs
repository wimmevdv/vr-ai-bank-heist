using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;


public class GameUI : MonoBehaviour
{
    [Header("End screen referenties")]
    [SerializeField] private GameObject endPanel;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI summaryText;
    [SerializeField] private Button playAgainButton;

    [Header("Teksten")]
    [SerializeField] private string winTitle = "ESCAPE GELUKT";
    [SerializeField] private string loseTitle = "TIJD VOORBIJ";
    [SerializeField] private string caughtTitle = "BETRAPT";
    [SerializeField] private string currencySymbol = "€";

    [Header("Positionering")]
    [Tooltip("Bij tonen het Canvas één keer voor de speler plaatsen. ")]
    [SerializeField] private bool snapToPlayerOnShow = true;
    [Tooltip("Afstand voor de speler in meters.")]
    [SerializeField] private float distanceFromPlayer = 2f;

    [Header("Fail-safe restart")]
    [Tooltip("Auto-restart na X seconden als VR-button niet klikt. 0 = uit. " +
             "Aanbevolen 15-30 sec als backup.")]
    [SerializeField] private float autoRestartSeconds = 0f;

    private bool subscribed;
    private Coroutine autoRestartRoutine;

    private void Awake()
    {
        if (endPanel != null) endPanel.SetActive(false);
        if (playAgainButton != null) playAgainButton.onClick.AddListener(OnPlayAgainClicked);
    }

    private void OnEnable()
    {
        StartCoroutine(SubscribeWhenReady());
    }

    private void OnDisable()
    {
        if (subscribed && HeistManager.Instance != null)
            HeistManager.Instance.OnGameEnded -= ShowEndScreen;
        subscribed = false;
    }

    private IEnumerator SubscribeWhenReady()
    {
        // Wacht tot HeistManager.Instance bestaat 
        while (HeistManager.Instance == null) yield return null;

        HeistManager.Instance.OnGameEnded += ShowEndScreen;
        subscribed = true;
    }

    private void ShowEndScreen(HeistManager.HeistEndInfo info)
    {
        if (snapToPlayerOnShow) SnapInFrontOfPlayer();

        if (endPanel != null) endPanel.SetActive(true);

        if (autoRestartSeconds > 0f)
        {
            if (autoRestartRoutine != null) StopCoroutine(autoRestartRoutine);
            autoRestartRoutine = StartCoroutine(AutoRestartAfter(autoRestartSeconds));
        }

        if (titleText != null)
        {
            string title;
            switch (info.result)
            {
                case HeistManager.GameResult.Won:         title = winTitle; break;
                case HeistManager.GameResult.LostCaught:  title = caughtTitle; break;
                case HeistManager.GameResult.LostTimeout:
                default:                                  title = loseTitle; break;
            }
            titleText.text = title;
        }

        if (summaryText != null)
        {
            string time = Mathf.CeilToInt(Mathf.Max(0f, info.timeRemainingAtEnd)).ToString();
            summaryText.text =
                $"Verdiend: {currencySymbol}{info.totalMoney}\n" +
                $"Buit: {info.itemsSecured} / {info.totalItems}\n" +
                $"Tijd over: {time}s";
        }
    }

    private void OnPlayAgainClicked()
    {
        if (HeistManager.Instance != null)
            HeistManager.Instance.RestartGame();
        else
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }


    private IEnumerator AutoRestartAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (HeistManager.Instance != null)
            HeistManager.Instance.RestartGame();
        else
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
    }

    private void SnapInFrontOfPlayer()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        transform.position = cam.transform.position + cam.transform.forward * distanceFromPlayer;

        Vector3 lookDir = transform.position - cam.transform.position;
        if (lookDir.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(lookDir, Vector3.up);
    }
}
