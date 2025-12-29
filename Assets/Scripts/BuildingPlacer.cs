using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class BuildingPlacer : MonoBehaviour
{
    public Camera mainCamera;
    public GameObject buildingPrefab;
    public float placementRadius = 0.5f; // радиус поиска ближайшей клетки

    // Зоны для каждого игрока: (minX, minY, maxX, maxY)
    private static readonly Vector4[] playerZones = new Vector4[]
    {
        new Vector4(0, 0, 3, 3),   // Игрок 0
        new Vector4(0, 4, 3, 7),   // Игрок 1
        new Vector4(4, 0, 7, 3),   // Игрок 2
        new Vector4(4, 4, 7, 7)    // Игрок 3
    };

    private int lastPlayerIndex = -1; // Отслеживаем, какой игрок в последний раз нажимал X

    private void Update()
    {
        if (!TowerSpawner.Instance.IsStageTwoStarted()) return;

        // Полный запрет строительства после GameOver
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver)
            return;

        // 1) ЛКМ
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            Vector3 mousePos = mainCamera.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            TryPlaceBuilding(mousePos, -1); // -1 = нет конкретного игрока
        }

        // 2) Кнопка X на джойстике для каждого игрока
        for (int i = 0; i < Gamepad.all.Count; i++)
        {
            if (Gamepad.all[i].buttonWest.wasPressedThisFrame)
            {
                Vector3 playerMousePos = TowerSpawner.Instance.GetMousePosition(i);
                lastPlayerIndex = i;
                TryPlaceBuilding(playerMousePos, i);
            }
        }
    }

    private bool IsInPlayerZone(Vector2Int gridPos, int playerIndex)
    {
        if (playerIndex < 0 || playerIndex >= playerZones.Length)
            return true; // Для ЛКМ без конкретного игрока разрешаем везде

        Vector4 zone = playerZones[playerIndex];
        int minX = (int)zone.x, minY = (int)zone.y;
        int maxX = (int)zone.z, maxY = (int)zone.w;

        return gridPos.x >= minX && gridPos.x <= maxX &&
               gridPos.y >= minY && gridPos.y <= maxY;
    }

    private void TryPlaceBuilding(Vector3 worldPos, int playerIndex)
    {
        // Если клик по UI — НЕ ставим в мир
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        // Смещение: левый верхний угол здания должен быть в worldPos
        // Поэтому центр здания смещаем на половину размера клетки вправо и вниз
        GridGenerator grid = TowerSpawner.Instance.grid;
        if (grid == null)
        {
            Debug.LogError("BuildingPlacer: GridGenerator не найден.");
            return;
        }

        float cellSize = grid.cellSize * grid.transform.localScale.x;
        Vector3 adjustedPos = worldPos + new Vector3(-cellSize / 2f, cellSize / 2f, 0);

        // 1) Найти ближайшую клетку
        Cell closestCell = null;
        float minDist = placementRadius;

        foreach (var cell in FindObjectsOfType<Cell>())
        {
            float dist = Vector2.Distance(adjustedPos, cell.transform.position);
            if (dist <= minDist && cell.IsFree)
            {
                // Проверка зоны игрока
                Vector2Int cellGridPos = GetGridPositionFromCell(cell);
                if (IsInPlayerZone(cellGridPos, playerIndex))
                {
                    closestCell = cell;
                    minDist = dist;
                }
            }
        }

        if (closestCell == null)
        {
            Debug.Log($"Нет свободной клетки в зоне игрока {playerIndex} для постройки.");
            return;
        }

        // 2) Выбор префаба здания
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
                Debug.LogWarning("BuildingPlacer: no building selected.");
                return;
            }
        }

        if (toSpawn == null)
        {
            Debug.LogError("BuildingPlacer: toSpawn is null.");
            return;
        }

        // 3) Проверка стоимости
        int cost = 0;
        Building costBuilding = toSpawn.GetComponent<Building>();
        if (costBuilding != null)
            cost = costBuilding.price;

        if (GameManager.Instance != null && cost > 0 && !GameManager.Instance.TrySpendMana(cost))
            return;

        // 4) Создание здания
        GameObject obj = Instantiate(toSpawn);
        if (!obj.activeSelf)
            obj.SetActive(true);

        Building building = obj.GetComponent<Building>();
        if (building == null)
        {
            Debug.LogWarning("BuildingPlacer: объект не содержит компонент Building.");
            if (GameManager.Instance != null && cost > 0)
                GameManager.Instance.AddManaToPlayer(GameManager.Instance.ActivePlayerIndex, cost);
            Destroy(obj);
            return;
        }

        building.Initialize();
        closestCell.PlaceBuilding(building);
    }

    private Vector2Int GetGridPositionFromCell(Cell cell)
    {
        // Попытаемся получить координаты из имени объекта (Cell_X_Y)
        string name = cell.gameObject.name;
        if (name.StartsWith("Cell_"))
        {
            string[] parts = name.Split('_');
            if (parts.Length >= 3 && int.TryParse(parts[1], out int x) && int.TryParse(parts[2], out int y))
            {
                return new Vector2Int(x, y);
            }
        }

        // Fallback: находим клетку в GridGenerator
        GridGenerator grid = TowerSpawner.Instance.grid;
        if (grid != null)
        {
            for (int x = 0; x < grid.width; x++)
            {
                for (int y = 0; y < grid.height; y++)
                {
                    if (grid.GetCell(x, y) == cell)
                        return new Vector2Int(x, y);
                }
            }
        }

        return Vector2Int.zero;
    }
}
