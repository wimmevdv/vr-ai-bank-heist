using UnityEngine;

public class DoorController : MonoBehaviour
{
    public float openAngle = 90f; // The angle the door should open
    public float speed = 2f;      // The speed at which the door opens
    
    private bool isOpen = false;
    private Quaternion closedRotation;
    private Quaternion openRotation;

    void Start()
    {
        // Store the initial rotation of the door hinge
        closedRotation = transform.rotation;
        // Calculate the target rotation (rotate around the Y-axis)
        openRotation = Quaternion.Euler(transform.eulerAngles + new Vector3(0, openAngle, 0));
    }

    void Update()
    {
        // Smoothly rotate the door to the open position if isOpen is true
        if (isOpen)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, openRotation, Time.deltaTime * speed);
        }
    }

    // Call this function from the XR socket event
    public void OpenDoor()
    {
        isOpen = true;
    }
}