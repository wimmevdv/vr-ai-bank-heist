using UnityEngine;
using UnityEngine.AI;

namespace Wimme.Test
{
    /// <summary>
    /// Drop-in keyboard controller for testing the AI guard from the thief's
    /// perspective. Add this to the ScriptedThief GameObject, then DISABLE the
    /// ScriptedThief + NavMeshAgent components. WASD to move, mouse to look.
    /// The guard AI sees this object the same way (tag "Player", trigger collider).
    /// </summary>
    public class PlayerThiefController : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 3.5f;
        [SerializeField] private float sprintSpeed = 6f;
        [SerializeField] private float mouseSensitivity = 2f;
        [SerializeField] private Camera playerCamera;

        private float yaw;
        private bool hasCursor;

        void Start()
        {
            // Disable ALL other cameras so only the thief POV is visible
            foreach (var cam in FindObjectsByType<Camera>(FindObjectsSortMode.None))
                cam.gameObject.SetActive(false);

            // Create first-person camera at eye height, looking straight ahead
            if (playerCamera == null)
            {
                var camGo = new GameObject("ThiefCamera");
                camGo.transform.SetParent(transform);
                camGo.transform.localPosition = new Vector3(0f, 1.6f, 0f);
                camGo.transform.localRotation = Quaternion.identity;
                playerCamera = camGo.AddComponent<Camera>();
                playerCamera.nearClipPlane = 0.1f;
                playerCamera.fieldOfView = 75f;
            }

            yaw = transform.eulerAngles.y;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            hasCursor = true;

            // Disable the scripted AI + NavMesh on this object
            var scripted = GetComponent<ScriptedThief>();
            if (scripted != null) scripted.enabled = false;
            var nav = GetComponent<NavMeshAgent>();
            if (nav != null) nav.enabled = false;
        }

        void Update()
        {
            // Toggle cursor lock with Escape
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                hasCursor = !hasCursor;
                Cursor.lockState = hasCursor ? CursorLockMode.Locked : CursorLockMode.None;
                Cursor.visible = !hasCursor;
            }

            // Mouse look (yaw only — no pitch needed for top-down-ish testing)
            if (hasCursor)
            {
                yaw += Input.GetAxis("Mouse X") * mouseSensitivity;
                float pitch = playerCamera.transform.localEulerAngles.x;
                pitch -= Input.GetAxis("Mouse Y") * mouseSensitivity;
                pitch = pitch > 180f ? pitch - 360f : pitch;
                pitch = Mathf.Clamp(pitch, -80f, 80f);
                playerCamera.transform.localEulerAngles = new Vector3(pitch, 0f, 0f);
            }
            transform.eulerAngles = new Vector3(0f, yaw, 0f);

            // WASD movement
            float speed = Input.GetKey(KeyCode.LeftShift) ? sprintSpeed : moveSpeed;
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            Vector3 dir = (transform.forward * v + transform.right * h).normalized;
            transform.position += dir * speed * Time.deltaTime;
        }
    }
}
