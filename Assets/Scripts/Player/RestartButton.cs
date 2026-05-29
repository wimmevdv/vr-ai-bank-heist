using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Fysieke 3D-knop (XRSimpleInteractable) die HeistManager.RestartGame() aanroept.
/// Werk-around voor wanneer Unity UI buttons niet pakken in VR.
/// Drop op een fysiek 3D-knop object naast de end-screen Canvas.
/// </summary>
[RequireComponent(typeof(XRSimpleInteractable))]
public class RestartButton : MonoBehaviour
{
    [SerializeField] private AudioSource clickAudio;

    private XRSimpleInteractable simpleInteractable;

    private void Awake()
    {
        simpleInteractable = GetComponent<XRSimpleInteractable>();
    }

    private void OnEnable()
    {
        simpleInteractable.firstSelectEntered.AddListener(OnPressed);
    }

    private void OnDisable()
    {
        simpleInteractable.firstSelectEntered.RemoveListener(OnPressed);
    }

    private void OnPressed(SelectEnterEventArgs args)
    {
        if (clickAudio != null && !clickAudio.isPlaying) clickAudio.Play();
        if (HeistManager.Instance != null)
            HeistManager.Instance.RestartGame();
    }
}
