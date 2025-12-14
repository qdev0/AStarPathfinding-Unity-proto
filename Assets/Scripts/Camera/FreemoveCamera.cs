using UnityEngine;

public sealed class FreemoveCamera : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 8f;
    public float fastMultiplier = 3f;
    public float slowMultiplier = 0.3f;

    [Header("Look")]
    public float lookSensitivity = 2f;
    public bool holdRightMouseToLook = true;

    private float _yaw;
    private float _pitch;

    private void Start()
    {
        Vector3 e = transform.eulerAngles;
        _yaw = e.y;
        _pitch = e.x;
    }

    private void Update()
    {
        HandleLook();
        HandleMove();
    }

    private void HandleLook()
    {
        bool looking = holdRightMouseToLook ? Input.GetMouseButton(1) : true;

        if (looking)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            float mx = Input.GetAxis("Mouse X") * lookSensitivity;
            float my = Input.GetAxis("Mouse Y") * lookSensitivity;

            _yaw += mx;
            _pitch -= my;
            _pitch = Mathf.Clamp(_pitch, -89f, 89f);

            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    private void HandleMove()
    {
        float speed = moveSpeed;

        if (Input.GetKey(KeyCode.LeftShift)) speed *= fastMultiplier;
        if (Input.GetKey(KeyCode.LeftControl)) speed *= slowMultiplier;

        float x = Input.GetAxisRaw("Horizontal"); // A/D
        float z = Input.GetAxisRaw("Vertical");   // W/S

        float y = 0f;
        if (Input.GetKey(KeyCode.E)) y += 1f;     // up
        if (Input.GetKey(KeyCode.Q)) y -= 1f;     // down

        Vector3 move = (transform.right * x + transform.forward * z + Vector3.up * y).normalized;
        transform.position += move * speed * Time.deltaTime;
    }
}
