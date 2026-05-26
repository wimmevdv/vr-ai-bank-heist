using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class VaultDoorController : MonoBehaviour
{
    [Header("Scharnier (lokale as)")]
    [Tooltip("Lokaal punt waarrond de deur draait (op de scharnierrand).")]
    public Vector3 hingeAnchor = new Vector3(-0.5f, 0f, 0f);

    [Tooltip("Lokale as waarrond de deur draait. (0,1,0) = Y-as.")]
    public Vector3 hingeAxis = new Vector3(0f, 1f, 0f);

    [Header("Auto-open (na keycard scan)")]
    [Tooltip("Hoek (graden) waar de deur naartoe draait t.o.v. de start-rotatie.")]
    public float autoOpenAngle = 95f;

    [Tooltip("Snelheid waarmee de deur opendraait (graden/sec).")]
    public float openSpeed = 30f;

    [Tooltip("Optionele vertraging voor het openen begint (sec).")]
    public float openDelay = 0f;

    private Rigidbody rb;
    private HingeJoint hinge;
    private bool isOpening = false;
    private Quaternion closedLocalRotation;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        hinge = GetComponent<HingeJoint>();
        closedLocalRotation = transform.localRotation;

        if (rb != null)
        {
            rb.useGravity = false;
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }
    }

    public void OpenAutomatically()
    {
        if (isOpening) return;
        isOpening = true;
        StartCoroutine(OpenRoutine());
    }

    private IEnumerator OpenRoutine()
    {
        if (openDelay > 0f) yield return new WaitForSeconds(openDelay);

        if (hinge != null) hinge.useMotor = false;
        if (rb != null) rb.isKinematic = true;

        Vector3 axisLocal = hingeAxis.sqrMagnitude > 0f ? hingeAxis.normalized : Vector3.up;
        Vector3 pivotWorld = transform.TransformPoint(hingeAnchor);
        Vector3 axisWorld = transform.TransformDirection(axisLocal);

        float rotated = 0f;
        float target = autoOpenAngle;
        float dir = Mathf.Sign(target);
        float absTarget = Mathf.Abs(target);

        while (rotated < absTarget)
        {
            float step = openSpeed * Time.deltaTime;
            if (rotated + step > absTarget) step = absTarget - rotated;

            transform.RotateAround(pivotWorld, axisWorld, step * dir);
            rotated += step;
            yield return null;
        }
    }

    [ContextMenu("Reset to Closed")]
    public void ResetToClosed()
    {
        StopAllCoroutines();
        transform.localRotation = closedLocalRotation;
        isOpening = false;
    }
}
