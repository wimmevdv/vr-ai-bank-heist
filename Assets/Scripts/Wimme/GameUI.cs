using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// End-screen overlay voor de heist. Toont na het einde van het spel een
/// paneel met de uitslag (gelukt / tijd voorbij), verdiend bedrag, aantal
/// items en resterende tijd, plus een Play Again-knop.
///
/// LIVE HUD (timer + score tijdens spel) zit op de left-controller watch —
/// dit script doet ENKEL het eind-paneel.
///
/// Setup (zie ook de instructies in het Claude-bericht):
///   1) Maak een Canvas in de scene. Render Mode = World Space.
///   2) Vervang op de Canvas de standaard GraphicRaycaster door
///      TrackedDeviceGraphicRaycaster (van XR Interaction Toolkit), zodat
///      VR-rays de knop kunnen klikken.
///   3) Onder de Canvas: een EndPanel (Image als achtergrond), in begin uit.
///      Daaronder TextMeshPro-tekst voor titel en samenvatting, en een
///      uGUI Button "PlayAgain" met een TextMeshPro-label "Play Again".
///   4) Hang dit script op de Canvas, sleep de references in de inspector.
///   5) Zorg dat de huidige scene in File > Build Settings staat (anders
///      werkt Play Again niet).
/// </summary>
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
    [Tooltip("Bij tonen het Canvas één keer voor de speler plaatsen. Daarna blijft het stilstaan — geen meelopen met het hoofd.")]
    [SerializeField] private bool snapToPlayerOnShow = true;
    [Tooltip("Afstand voor de speler in meters.")]
    [SerializeField] private float distanceFromPlayer = 2f;

    private bool subscribed;

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
        // Wacht tot HeistManager.Instance bestaat — script-volgorde-veilig.
        while (HeistManager.Instance == null) yield return null;

        HeistManager.Instance.OnGameEnded += ShowEndScreen;
        subscribed = true;
    }

    private void ShowEndScreen(HeistManager.HeistEndInfo info)
    {
        if (snapToPlayerOnShow) SnapInFrontOfPlayer();

        if (endPanel != null) endPanel.SetActive(true);

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

    /// <summary>
    /// Plaatst het Canvas eenmaal vóór de hoofdcamera (HMD) en richt het naar
    /// de speler. Het paneel volgt daarna NIET — geen masker-effect.
    /// </summary>
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
