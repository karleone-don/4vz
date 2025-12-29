using UnityEngine;
using UnityEngine.InputSystem;

public class MouseController : MonoBehaviour
{
    private Vector2 moveInput;

    [Header("Movement")]
    public float moveSpeed = 8f;
    public float deadzone = 0.15f;

    [Header("Limits")]
    public float limitX = 6f;
    public float limitY = 5f;

    private Gamepad gamepad;

    public void Initialize(int playerIndex)
    {
        if (playerIndex < Gamepad.all.Count)
            gamepad = Gamepad.all[playerIndex];
    }

    private void Update()
    {
        if (gamepad == null)
            return;

        Vector2 input = gamepad.leftStick.ReadValue();

        if (input.magnitude < deadzone)
            input = Vector2.zero;

        moveInput = input;

        Move();
    }

    private void Move()
    {
        Vector3 pos = transform.position;
        pos += (Vector3)(moveInput * moveSpeed * Time.deltaTime);

        pos.x = Mathf.Clamp(pos.x, -limitX, limitX);
        pos.y = Mathf.Clamp(pos.y, -limitY, limitY);

        transform.position = pos;
    }
}
