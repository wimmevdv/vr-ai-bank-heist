using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Knop op de muur in de safe-zone. Bij een druk wordt het signaal samen met de
/// huidige <see cref="SafeZone.IsPlayerInZone"/>-status doorgegeven aan
/// <see cref="HeistManager.TryExecuteExtraction"/> voor validatie.
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
        simpleInteractable.firstSelectEntered.AddListener(OnButtonInteracted);
    }

    private void OnDisable()
    {
        simpleInteractable.firstSelectEntered.RemoveListener(OnButtonInteracted);
    }

    private void OnButtonInteracted(SelectEnterEventArgs args)
    {
        if (HeistManager.Instance == null) return;

        bool isReady = safeZone != null && safeZone.IsPlayerInZone;
        HeistManager.Instance.TryExecuteExtraction(isReady, errorAudio);
    }
}