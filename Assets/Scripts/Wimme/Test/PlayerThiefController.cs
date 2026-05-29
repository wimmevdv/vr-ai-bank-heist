using UnityEngine;
using UnityEngine.AI;

namespace Wimme.Test
{
    /// <summary>
    /// Desktop-besturing voor de "dief"-rol tijdens training-debugging zonder VR-headset:
    /// WASD-movement plus muis-look op een <see cref="CharacterController"/>. Schakelt
    /// de <see cref="ScriptedThief"/> en <see cref="UnityEngine.AI.NavMeshAgent"/> uit
    /// zodat de menselijke speler en de scripted AI niet om de besturing concurreren.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerThiefController : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 3.5f;
        [SerializeField] private float sprintSpeed = 6f;
        [SerializeField] private float mouseSensitivity = 2f;
        [SerializeField] private Camera playerCamera;

        private CharacterController cc;
        private float yaw;
        private float verticalVelocity;
        private bool hasCursor;

        void Start()
        {
            cc = GetComponent<CharacterController>();
            cc.height = 1.8f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.radius = 0.3f;
            cc.stepOffset = 0.4f;
            cc.slopeLimit = 50f;

            foreach (var cam in FindObjectsByType<Camera>(FindObjectsSortMode.None))
                cam.gameObject.SetActive(false);

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

            var scripted = GetComponent<ScriptedThief>();
            if (scripted != null) scripted.enabled = false;
            var nav = GetComponent<NavMeshAgent>();
            if (nav != null) nav.enabled = false;
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                hasCursor = !hasCursor;
                Cursor.lockState = hasCursor ? CursorLockMode.Locked : CursorLockMode.None;
                Cursor.visible = !hasCursor;
            }

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

            float speed = Input.GetKey(KeyCode.LeftShift) ? sprintSpeed : moveSpeed;
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            Vector3 dir = (transform.forward * v + transform.right * h).normalized;

            if (cc.isGrounded) verticalVelocity = -1f;
            else verticalVelocity += Physics.gravity.y * Time.deltaTime;

            Vector3 move = dir * speed + Vector3.up * verticalVelocity;
            cc.Move(move * Time.deltaTime);
        }
    }
}
