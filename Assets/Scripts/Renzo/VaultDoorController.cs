using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(HingeJoint))]
public class VaultDoorController : MonoBehaviour
{
    [Header("Hinge")]
    [Tooltip("Lokale positie van het scharnier op de deur (rand waar de deur draait).")]
    public Vector3 hingeAnchor = new Vector3(-0.5f, 0f, 0f);

    [Tooltip("As waarrond de deur draait. (0,1,0) = Y-as, deur blijft op zelfde hoogte.")]
    public Vector3 hingeAxis = new Vector3(0f, 1f, 0f);

    [Header("Openings limieten (graden)")]
    public float minAngle = 0f;
    public float maxAngle = 100f;

    [Header("Fysica")]
    public float mass = 40f;
    public float angularDrag = 2f;

    [Header("Auto-open (na keycard scan)")]
    [Tooltip("Doel hoek waar de deur automatisch naartoe draait.")]
    public float autoOpenAngle = 95f;

    [Tooltip("Kracht waarmee de motor de deur opent.")]
    public float motorForce = 200f;

    [Tooltip("Snelheid waarmee de motor draait (graden/sec).")]
    public float motorSpeed = 40f;

    [Tooltip("Optionele veer om deur dicht te houden voor scan. 0 = uit.")]
    public float closedSpringForce = 0f;
    public float closedSpringDamper = 1f;

    private Rigidbody rb;
    private HingeJoint hinge;
    private bool isOpening = false;

    void Reset() { Configure(); }
    void Awake() { Configure(); }

    void Configure()
    {
        rb = GetComponent<Rigidbody>();
        hinge = GetComponent<HingeJoint>();

        rb.mass = mass;
        rb.angularDamping = angularDrag;
        rb.useGravity = false;
        rb.isKinematic = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.constraints = RigidbodyConstraints.FreezePositionY;

        hinge.anchor = hingeAnchor;
        hinge.axis = hingeAxis;
        hinge.useLimits = true;

        var limits = hinge.limits;
        limits.min = Mathf.Min(minAngle, maxAngle);
        limits.max = Mathf.Max(minAngle, maxAngle);
        limits.bounciness = 0f;
        hinge.limits = limits;

        if (closedSpringForce > 0f)
        {
            hinge.useSpring = true;
            var spring = hinge.spring;
            spring.spring = closedSpringForce;
            spring.damper = closedSpringDamper;
            spring.targetPosition = 0f;
            hinge.spring = spring;
        }
        else
        {
            hinge.useSpring = false;
        }

        hinge.useMotor = false;

        var grab = GetComponent<XRGrabInteractable>();
        if (grab != null)
        {
            grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
            grab.throwOnDetach = false;
            grab.trackPosition = true;
            grab.trackRotation = true;
        }
    }

    public void OpenAutomatically()
    {
        if (isOpening) return;
        isOpening = true;

        hinge.useSpring = false;

        bool openPositive = autoOpenAngle >= 0f;
        float targetSpeed = openPositive ? motorSpeed : -motorSpeed;

        var motor = hinge.motor;
        motor.force = motorForce;
        motor.targetVelocity = targetSpeed;
        motor.freeSpin = false;
        hinge.motor = motor;
        hinge.useMotor = true;
    }

    void FixedUpdate()
    {
        if (!isOpening) return;

        float current = hinge.angle;
        bool reached = (autoOpenAngle >= 0f && current >= autoOpenAngle - 1f)
                    || (autoOpenAngle < 0f && current <= autoOpenAngle + 1f);

        if (reached)
        {
            var motor = hinge.motor;
            motor.targetVelocity = 0f;
            hinge.motor = motor;
        }
    }
}
