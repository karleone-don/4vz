using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class BuildingPlacer : MonoBehaviour
{
    public Camera mainCamera;
    public GameObject buildingPrefab;

    private void Update()
    {
        // 1) Полный запрет строительства после GameOver
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver)
            return;

        // 2) ЛКМ
        if (!Mouse.current.leftButton.wasPressedThisFrame)
            return;

        // 3) Если клик по UI — НЕ ставим в мир (важно для меню/гейм-овер)
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        if (mainCamera == null)
        {
            Debug.LogError("BuildingPlacer: mainCamera is not assigned!");
            return;
        }

        Vector3 mousePos = mainCamera.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        Vector2 mousePos2D = new Vector2(mousePos.x, mousePos.y);

        Collider2D hit = Physics2D.OverlapPoint(mousePos2D);
        if (hit == null) return;

        Cell cell = hit.GetComponent<Cell>();
        if (cell == null) return;

        if (!cell.IsFree)
        {
            Debug.Log("Клетка занята!");
            return;
        }

        // 4) Выбираем префаб: выбранный в BuildingSelector или дефолт
        GameObject toSpawn = buildingPrefab;
        if (BuildingSelector.Instance != null)
        {
            var selected = BuildingSelector.Instance.GetSelectedBuilding();
            if (selected != null)
            {
                toSpawn = selected;
            }
            else
            {
                Debug.LogWarning("BuildingPlacer: no weapon selected.");
                return;
            }
        }

        if (toSpawn == null)
        {
            Debug.LogError("BuildingPlacer: toSpawn is null (no prefab selected and buildingPrefab is null).");
            return;
        }

        int cost = 0;
        Building costBuilding = toSpawn.GetComponent<Building>();
        if (costBuilding != null)
            cost = costBuilding.price;

        if (GameManager.Instance != null && cost > 0)
        {
            if (!GameManager.Instance.TrySpendMana(cost))
                return;
        }

        GameObject obj = Instantiate(toSpawn);
        if (!obj.activeSelf)
            obj.SetActive(true);
        Building building = obj.GetComponent<Building>();

        if (building == null)
        {
            Debug.LogWarning("BuildingPlacer: Instantiated object doesn't contain Building component.");
            if (GameManager.Instance != null && cost > 0)
                GameManager.Instance.AddManaToPlayer(GameManager.Instance.ActivePlayerIndex, cost);
            Destroy(obj);
            return;
        }

        building.Initialize();           // характеристики
        cell.PlaceBuilding(building);    // ставим точно в центр клетки
    }
}
