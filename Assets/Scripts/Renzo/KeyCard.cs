using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Attachment;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class KeyCard : MonoBehaviour
{
    [Tooltip("ID van de keycard. Moet overeenkomen met de scanner.")]
    public string keyId = "vault";

    [Header("Fysica")]
    public float mass = 0.05f;
    public bool enforcePhysicsSettings = true;
    public bool enforceGrabSettings = true;

    [Header("Hold-restricties")]
    [Tooltip("Maximale afstand (m) tussen de interactor (hand) en de kaart terwijl je hem vasthoudt.")]
    public float maxHoldDistance = 0.25f;

    [Tooltip("Schakel manipulation input (joystick-push/pull) uit terwijl je de kaart vasthoudt.")]
    public bool disableManipulationWhileHeld = true;

    private XRGrabInteractable grab;
    private Rigidbody rb;
    private IXRSelectInteractor currentInteractor;
    private InteractionAttachController cachedAttachController;
    private bool savedUseManipulation;

    void Reset() { Configure(); }

    void Awake()
    {
        Configure();
        grab = GetComponent<XRGrabInteractable>();
        if (grab != null)
        {
            grab.selectEntered.AddListener(OnGrab);
            grab.selectExited.AddListener(OnRelease);
        }
    }

    void OnDestroy()
    {
        if (grab != null)
        {
            grab.selectEntered.RemoveListener(OnGrab);
            grab.selectExited.RemoveListener(OnRelease);
        }
    }

    void Configure()
    {
        rb = GetComponent<Rigidbody>();
        if (enforcePhysicsSettings)
        {
            rb.mass = mass;
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            foreach (var col in GetComponentsInChildren<Collider>())
            {
                col.isTrigger = false;
            }
        }

        if (enforceGrabSettings)
        {
            var g = GetComponent<XRGrabInteractable>();
            if (g != null)
            {
                g.movementType = XRBaseInteractable.MovementType.VelocityTracking;
                g.throwOnDetach = false;
                g.trackPosition = true;
                g.trackRotation = true;
                g.smoothPosition = true;
                g.smoothRotation = true;
            }
        }
    }

    void OnGrab(SelectEnterEventArgs args)
    {
        currentInteractor = args.interactorObject;

        if (disableManipulationWhileHeld && currentInteractor is Component c)
        {
            cachedAttachController = c.GetComponentInParent<InteractionAttachController>();
            if (cachedAttachController != null)
            {
                savedUseManipulation = cachedAttachController.useManipulationInput;
                cachedAttachController.useManipulationInput = false;
            }
        }
    }

    void OnRelease(SelectExitEventArgs args)
    {
        if (cachedAttachController != null)
        {
            cachedAttachController.useManipulationInput = savedUseManipulation;
            cachedAttachController = null;
        }
        currentInteractor = null;
    }

    void FixedUpdate()
    {
        if (currentInteractor == null || rb == null) return;

        Transform handAttach = currentInteractor.GetAttachTransform(grab);
        if (handAttach == null) return;

        Vector3 toCard = rb.position - handAttach.position;
        float dist = toCard.magnitude;
        if (dist > maxHoldDistance)
        {
            Vector3 clamped = handAttach.position + toCard.normalized * maxHoldDistance;
            rb.position = clamped;
            rb.linearVelocity = Vector3.zero;
        }
    }
}
