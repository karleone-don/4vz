using UnityEngine;
using UnityEngine.InputSystem; // новая Input System

public class Cell : MonoBehaviour
{
    public int x;
    public int y;

    private void Start()
    {
        if (GetComponent<Collider2D>() == null)
        {
            gameObject.AddComponent<BoxCollider2D>();
        }
    }

    private void Update()
    {
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            Vector3 mousePos = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            Collider2D hit = Physics2D.OverlapPoint(mousePos);
            if (hit != null && hit.gameObject == gameObject)
            {
                Debug.Log($"Clicked cell: x = {x}, y = {y}");
            }
        }
    }
}
