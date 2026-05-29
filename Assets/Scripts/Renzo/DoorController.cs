using UnityEngine;

/// <summary>
/// Eenvoudige draaideur die rond de Y-as opent met <see cref="Quaternion.Slerp"/>.
/// Activeren via <see cref="OpenDoor"/> (bijvoorbeeld vanuit een XR socket-event).
/// </summary>
public class DoorController : MonoBehaviour
{
    [Tooltip("Hoek (graden) tussen open en gesloten positie.")]
    public float openAngle = 90f;

    [Tooltip("Slerp-snelheid waarmee de deur opendraait.")]
    public float speed = 2f;

    private bool isOpen = false;
    private Quaternion closedRotation;
    private Quaternion openRotation;

    void Start()
    {
        closedRotation = transform.rotation;
        openRotation = Quaternion.Euler(transform.eulerAngles + new Vector3(0, openAngle, 0));
    }

    void Update()
    {
        if (isOpen)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, openRotation, Time.deltaTime * speed);
        }
    }

    /// <summary>Triggert het openen van de deur. Idempotent.</summary>
    public void OpenDoor()
    {
        isOpen = true;
    }
}