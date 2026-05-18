using UnityEngine;

/// <summary>
/// Synchronizes the VR Avatar's body with the headset's position, 
/// keeping the feet on the floor with a customizable offset for different 3D models.
/// </summary>
public class VRAvatarController : MonoBehaviour
{
    [Header("Tracking Targets")]
    [Tooltip("The Main Camera representing the player's VR Headset")]
    [SerializeField] private Transform headset;

    [Tooltip("The root of the XR Origin to determine the actual floor level")]
    [SerializeField] private Transform xrOrigin;

    [Header("Calibration")]
    [Tooltip("Offset to correct the avatar's height if its pivot point is not at the soles of the feet. Adjust the Y value.")]
    [SerializeField] private Vector3 positionOffset = Vector3.zero;

    [Tooltip("How smoothly the torso rotates to match the direction the player is looking.")]
    [SerializeField] private float bodyTurnSpeed = 5f;

    private void LateUpdate()
    {
        if (headset == null || xrOrigin == null) return;

        SyncBodyPosition();
        SyncBodyRotation();
    }

    /// <summary>
    /// Berekent de positie op de vloer en telt daar de handmatige kalibratie (offset) bij op.
    /// </summary>
    private void SyncBodyPosition()
    {
        // Add the custom offset to the calculated floor position
        Vector3 targetPosition = new Vector3(headset.position.x, xrOrigin.position.y, headset.position.z) + positionOffset;
        transform.position = targetPosition;
    }

    /// <summary>
    /// Roteert het lichaam op de Y-as, maar behoudt de X en Z as om te voorkomen dat het model plat valt.
    /// </summary>
    private void SyncBodyRotation()
    {
        float targetYRotation = headset.eulerAngles.y;
        float currentXRotation = transform.eulerAngles.x;
        float currentZRotation = transform.eulerAngles.z;

        Quaternion targetRotation = Quaternion.Euler(currentXRotation, targetYRotation, currentZRotation);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * bodyTurnSpeed);
    }
}