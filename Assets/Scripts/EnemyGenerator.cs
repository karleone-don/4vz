using UnityEngine;

public class EnemyGenerator : MonoBehaviour
{
    public GameObject enemyPrefab;
    public GameObject fastZombiePrefab;
    public GameObject tankZombiePrefab;
    [Tooltip("Шанс появления FastZombie (0..1)")]
    public float fastChance = 0.15f;
    [Tooltip("Шанс появления TankZombie (0..1)")]
    public float tankChance = 0.05f;
    [Tooltip("Минимум TankZombie на волну (0 = без гарантии)")]
    public int minTankPerWave = 1;
    [Header("Waves")]
    public int startWaveSize = 6;
    public int waveSizeIncrease = 2;
    public float spawnInterval = 0.8f;
    public float timeBetweenWaves = 6f;
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

        if (enemyPrefab == null && fastZombiePrefab == null && tankZombiePrefab == null)
        {
            Debug.LogError("EnemyGenerator: ни один префаб врага не назначен в инспекторе.");
            enabled = false;
            return;
        }

        StartCoroutine(WaveLoop());
    }

    private System.Collections.IEnumerator WaveLoop()
    {
        int wave = 0;
        while (true)
        {
            wave++;
            int count = startWaveSize + (wave - 1) * waveSizeIncrease;
            int tanksLeft = Mathf.Max(0, minTankPerWave);

            for (int i = 0; i < count; i++)
            {
                int forcedType = 0;
                if (tanksLeft > 0 && (count - i) <= tanksLeft)
                {
                    forcedType = 2;
                    tanksLeft--;
                }

                SpawnEnemy(forcedType);
                yield return new WaitForSeconds(spawnInterval);
            }

            yield return new WaitForSeconds(timeBetweenWaves);
        }
    }

    void SpawnEnemy(int forcedType = 0)
    {
        Vector2 spawnPos;
        int side; // 0=Left,1=Right,2=Bottom,3=Top
        GetRandomBorderPosition(out spawnPos, out side);

        // Выбираем тип врага (0=default,1=fast,2=tank) по шансам
        int chosenType = 0;
        if (forcedType == 2)
        {
            chosenType = 2;
        }
        else
        {
            float r = Random.value;
            if (r < fastChance) chosenType = 1;
            else if (r < fastChance + tankChance) chosenType = 2;
        }

        // Если для выбранного типа есть отдельный префаб — используем его, иначе будем инстанцировать базовый префаб и заменять компонент на нужный тип
        GameObject prefabToSpawn = enemyPrefab;
        if (chosenType == 1 && fastZombiePrefab != null) prefabToSpawn = fastZombiePrefab;
        else if (chosenType == 2 && tankZombiePrefab != null) prefabToSpawn = tankZombiePrefab;

        GameObject enemyObj = Instantiate(prefabToSpawn, spawnPos, Quaternion.identity);

        Debug.Log($"EnemyGenerator: spawned type={chosenType} prefab={(prefabToSpawn!=null?prefabToSpawn.name:"<null>")} at {spawnPos}");

        // Если инстанцировали конкретный префаб, то просто инициализируем
        Enemy enemy = enemyObj.GetComponent<Enemy>();
        if (enemy != null && ((chosenType == 0) ||
            (chosenType == 1 && prefabToSpawn == fastZombiePrefab) ||
            (chosenType == 2 && prefabToSpawn == tankZombiePrefab)))
        {
            enemy.Initialize(grid, side);
            enemy.SetupEnemy();
            return;
        }

        // Иначе — мы инстанцировали базовый префаб, но хотим, чтобы модель и визуал был как у базового, а логика — у Fast/Tank
        if (chosenType == 1)
        {
            // Заменяем компонент Enemy на FastZombie
            Enemy existing = enemyObj.GetComponent<Enemy>();
            if (existing != null) Destroy(existing);
            FastZombie f = enemyObj.AddComponent<FastZombie>();
            f.Initialize(grid, side);
            f.SetupEnemy();
            return;
        }

        if (chosenType == 2)
        {
            Enemy existing = enemyObj.GetComponent<Enemy>();
            if (existing != null) Destroy(existing);
            TankZombie t = enemyObj.AddComponent<TankZombie>();
            t.Initialize(grid, side);
            t.SetupEnemy();
            return;
        }

        // fallback: если ни один компонент не был найден/добавлен — предупреждение
        if (enemy != null)
        {
            enemy.Initialize(grid, side);
            enemy.SetupEnemy();
        }
        else
        {
            Debug.LogWarning("Префаб врага не содержит Enemy — движение не будет работать.");
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
