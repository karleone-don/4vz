using UnityEngine;
using UnityEngine.InputSystem;

public class BuildingPlacer : MonoBehaviour
{
    public Camera mainCamera;
    public GameObject buildingPrefab;

    void Update()
    {
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            Vector3 mousePos = mainCamera.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            Vector2 mousePos2D = new Vector2(mousePos.x, mousePos.y);

            Collider2D hit = Physics2D.OverlapPoint(mousePos2D);
            if (hit != null)
            {
                Cell cell = hit.GetComponent<Cell>();
                if (cell != null)
                {
                    if (cell.IsFree)
                    {
                        GameObject obj = Instantiate(buildingPrefab);
                        Building building = obj.GetComponent<Building>();

                        building.Initialize();   // характеристики
                        cell.PlaceBuilding(building); // ставим точно в центр клетки
                    }
                    else
                    {
                        Debug.Log("Клетка занята!");
                    }
                }
            }
        }
    }
}
