using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables; // Required for XRI 3.x

/// <summary>
/// Handles input verification on an XRSimpleInteractable button.
/// Bridges physical interaction with the global HeistManager state evaluation.
/// </summary>
[RequireComponent(typeof(XRSimpleInteractable))]
public class ExtractionButton : MonoBehaviour
{
    [Header("Dependencies")]
    [Tooltip("The reference to the SafeZone trigger box to check player presence.")]
    [SerializeField] private SafeZone safeZone;

    [Header("Audio Feedback")]
    [Tooltip("Audio source that plays an error sound when extraction conditions are unmet.")]
    [SerializeField] private AudioSource errorAudio;

    private XRSimpleInteractable simpleInteractable;

    private void Awake()
    {
        simpleInteractable = GetComponent<XRSimpleInteractable>();
    }

    private void OnEnable()
    {
        // Subscribe to XRI interaction events (Observer Pattern)
        simpleInteractable.firstSelectEntered.AddListener(OnButtonInteracted);
    }

    private void OnDisable()
    {
        // Unsubscribe to ensure memory safety
        simpleInteractable.firstSelectEntered.RemoveListener(OnButtonInteracted);
    }

    /// <summary>
    /// Invoked when the player presses the button (Select event triggered).
    /// </summary>
    private void OnButtonInteracted(SelectEnterEventArgs args)
    {
        if (HeistManager.Instance == null) return;

        // Determine current safe state (safely falls back to false if reference is broken)
        bool isReady = safeZone != null && safeZone.IsPlayerInZone;

        // Forward validation request to the master manager
        HeistManager.Instance.TryExecuteExtraction(isReady, errorAudio);
    }
}