using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Reads analog input from the XR controllers (Grip and Trigger)
/// and passes these values to the Avatar's Animator to blend hand animations.
/// </summary>
[RequireComponent(typeof(Animator))]
public class VRAvatarHandAnimator : MonoBehaviour
{
    [Header("Input Actions")]
    [Tooltip("Reference to the Left Hand Grip action (e.g., XRI LeftHand/Select Value)")]
    [SerializeField] private InputActionReference leftGripAction;
    [Tooltip("Reference to the Right Hand Grip action")]
    [SerializeField] private InputActionReference rightGripAction;

    private Animator animator;

    private readonly int leftGripHash = Animator.StringToHash("LeftGrip");
    private readonly int rightGripHash = Animator.StringToHash("RightGrip");

    private void Awake()
    {
        animator = GetComponent<Animator>();
    }

    private void Update()
    {
        UpdateHandAnimations();
    }

    /// <summary>
    /// Leest de float-waardes (0.0 tot 1.0) van de controller-knoppen
    /// en stuurt deze direct door naar de parameters in de Unity Animator.
    /// </summary>
    private void UpdateHandAnimations()
    {
        float leftGripValue = leftGripAction.action.ReadValue<float>();
        animator.SetFloat(leftGripHash, leftGripValue);

        float rightGripValue = rightGripAction.action.ReadValue<float>();
        animator.SetFloat(rightGripHash, rightGripValue);
    }
}