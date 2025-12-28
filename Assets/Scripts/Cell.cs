using UnityEngine;
using UnityEngine.InputSystem;

public class Cell : MonoBehaviour
{
    public int x;
    public int y;

    // текущее строение на клетке
    public Building buildingOnCell = null;

    public bool IsFree => buildingOnCell == null;

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
                Debug.Log($"Clicked cell: x = {x}, y = {y}, free = {IsFree}");
            }
        }
    }
    public Vector3 GetCenter()
    {
        return transform.position;
    }

    // установка здания на клетку
    public void PlaceBuilding(Building building)
    {
        
        if (IsFree)
        {
            buildingOnCell = building;
            building.transform.position = transform.position;
            building.transform.SetParent(transform);
            Debug.Log($"Placed building '{building.name}' on cell {x},{y}");
        }
        else
        {
            Debug.LogWarning("Клетка занята!");
        }
    }

    // удаление здания
    public void RemoveBuilding()
    {
        if (!IsFree)
        {
            Destroy(buildingOnCell.gameObject);
            buildingOnCell = null;
        }
    }
}
