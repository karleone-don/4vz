using UnityEngine;

public class EnemyGenerator : MonoBehaviour
{
    public GameObject enemyPrefab;
    public float spawnInterval = 1f;
    public GridGenerator grid;
    public int borderOffset = 2; // расстояние n от сетки

    private Vector2 cell00Pos;
    private float cellStep;

    void Start()
    {
        // Защитные проверки
        if (grid == null)
        {
            grid = FindObjectOfType<GridGenerator>();
            if (grid == null)
            {
                Debug.LogError("EnemyGenerator: GridGenerator не найден. Назначьте его в инспекторе.");
                enabled = false;
                return;
            }
        }

        // Попробуем найти реальные объекты клеток. Если их нет — вычислим по параметрам сетки.
        Transform cell00 = grid.transform.Find("Cell_0_0");
        Transform cell10 = grid.transform.Find("Cell_1_0");

        if (cell00 != null && cell10 != null)
        {
            cell00Pos = cell00.position;
            cellStep = Vector2.Distance(cell00.position, cell10.position);
        }
        else
        {
            // fallback: вычислим шаг и позицию на основе параметров сетки (надёжнее, если клетки ещё не созданы)
            float scaleX = grid.transform.localScale.x;
            cellStep = (grid.cellSize + grid.gap) * scaleX;
            // вычислим позицию cell00 как центр сетки смещённый влево/вниз
            float offsetX = (grid.width * (grid.cellSize + grid.gap) - grid.gap) / (2f) * scaleX;
            float offsetY = (grid.height * (grid.cellSize + grid.gap) - grid.gap) / (2f) * scaleX;
            // предполагаем, что GridGenerator размещен так, что его локальная позиция = центр сетки
            Vector3 gridPos = grid.transform.position;
            // cell (0,0) будет внизу слева относительно центра
            cell00Pos = new Vector2(gridPos.x - offsetX + (grid.cellSize * scaleX) / 2f,
                                    gridPos.y - offsetY + (grid.cellSize * scaleX) / 2f);
        }

        if (enemyPrefab == null)
        {
            Debug.LogError("EnemyGenerator: enemyPrefab не назначен в инспекторе.");
            enabled = false;
            return;
        }

        InvokeRepeating(nameof(SpawnEnemy), 0f, spawnInterval);
    }

    void SpawnEnemy()
    {
        Vector2 spawnPos;
        int side; // 0=Left,1=Right,2=Bottom,3=Top
        GetRandomBorderPosition(out spawnPos, out side);

        GameObject enemyObj = Instantiate(enemyPrefab, spawnPos, Quaternion.identity);
        Enemy enemy = enemyObj.GetComponent<Enemy>();
        if (enemy != null)
        {
            enemy.Initialize(grid, side);
            enemy.SetupEnemy();
        }
        else
        {
            Debug.LogWarning("Префаб врага не содержит EnemyMover — движение не будет работать.");
        }
    }

    // возвращает позицию и сторону спавна
    void GetRandomBorderPosition(out Vector2 worldPos, out int sideOut)
    {
        int w = grid.width;
        int h = grid.height;
        int n = borderOffset;

        int side = Random.Range(0, 4);
        sideOut = side;

        int x, y;

        switch (side)
        {
            case 0: x = -n; y = Random.Range(0, h); break; // Left
            case 1: x = w + n - 1;  y = Random.Range(0, h); break; // Right
            case 2: x = Random.Range(0, w); y = -n; break; // Bottom
            default: x = Random.Range(0, w); y = h + n - 1; break; // Top
        }

        float worldX = cell00Pos.x + x * cellStep;
        float worldY = cell00Pos.y + y * cellStep;

        worldPos = new Vector2(worldX, worldY);
    }
}
