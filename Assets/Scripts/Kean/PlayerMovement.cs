using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    public float walkSpeed = 5f;
    public float runSpeed = 10f;
    public float jumpHeight = 1.5f;
    public float gravity = -9.81f;

    [Header("Look")]
    public float mouseSensitivity = 100f;
    public Transform cameraTransform;

    private CharacterController _controller;
    private Vector3 _velocity;
    private float _xRotation = 0f;

    void Start()
    {
        _controller = GetComponent<CharacterController>();

        // Lock and hide cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        HandleLook();
        HandleMovement();
    }

    void HandleLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        // Vertical rotation (camera only, clamped)
        _xRotation -= mouseY;
        _xRotation = Mathf.Clamp(_xRotation, -90f, 90f);
        cameraTransform.localRotation = Quaternion.Euler(_xRotation, 0f, 0f);

        // Horizontal rotation (entire player)
        transform.Rotate(Vector3.up * mouseX);
    }

    void HandleMovement()
    {
        bool isGrounded = _controller.isGrounded;

        // Reset downward velocity when grounded
        if (isGrounded && _velocity.y < 0f)
            _velocity.y = -2f;

        // WASD input
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        Vector3 move = transform.right * horizontal + transform.forward * vertical;

        // Sprint with Left Shift
        float speed = Input.GetKey(KeyCode.LeftShift) ? runSpeed : walkSpeed;
        _controller.Move(move * speed * Time.deltaTime);

        // Jump
        if (Input.GetButtonDown("Jump") && isGrounded)
            _velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);

        // Apply gravity
        _velocity.y += gravity * Time.deltaTime;
        _controller.Move(_velocity * Time.deltaTime);
    }
}