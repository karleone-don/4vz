using UnityEngine;
using UnityEngine.InputSystem;

public class TowerSpawner : MonoBehaviour
{
    public static TowerSpawner Instance;
    [Header("Grid & Prefabs")]
    public GridGenerator grid;
    public GameObject[] towerPrefabs; // 4 башни
    public GameObject[] mousePrefabs; // 4 мышки

    [Header("Mouse Spawn Positions")]
    public Vector3[] mouseSpawnPositions =
    {
        new Vector3(-1f, -1f, 0f),
        new Vector3(-1f, 1f, 0f),
        new Vector3(1f, -1f, 0f),
        new Vector3(1f, 1f, 0f)
    };

    [Header("Tower Grid Positions")]
    public Vector2Int[] towerGridPositions =
    {
        new Vector2Int(2,2),
        new Vector2Int(2,5),
        new Vector2Int(5,2),
        new Vector2Int(5,5)
    };

    private GameObject[] spawnedMice;
    private const int maxPlayers = 4;
    private bool stageTwoStarted = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        spawnedMice = new GameObject[maxPlayers];
    }

    private void Update()
    {
        if (stageTwoStarted) return;

        for (int i = 0; i < Gamepad.all.Count && i < maxPlayers; i++)
        {
            Gamepad pad = Gamepad.all[i];

            if (pad.buttonSouth.wasPressedThisFrame)
            {
                TrySpawnPlayerSet(i);
            }

            // Если игрок уже есть и нажал треугольник — старт второго этапа
            if (spawnedMice[i] != null && pad.buttonNorth.wasPressedThisFrame)
            {
                StartStageTwo();
            }
        }
    }

    private void TrySpawnPlayerSet(int index)
    {
        if (spawnedMice[index] != null) return;

        SpawnMouse(index);
        SpawnSpecificTower(index);
    }

    private void SpawnMouse(int index)
    {
        if (index >= mousePrefabs.Length || mousePrefabs[index] == null) return;

        Vector3 spawnPos = mouseSpawnPositions[index];
        GameObject mouse = Instantiate(mousePrefabs[index], spawnPos, Quaternion.identity);
        spawnedMice[index] = mouse;

        var controller = mouse.GetComponent<MouseController>();
        if (controller != null)
        {
            controller.Initialize(index);
        }

        var rb2d = mouse.GetComponent<Rigidbody2D>();
        if (rb2d != null)
        {
            rb2d.freezeRotation = true;
            rb2d.gravityScale = 0f;
        }

        Debug.Log($"Игрок {index}: Мышка создана в {spawnPos}");
    }

    private void SpawnSpecificTower(int index)
    {
        if (index >= towerPrefabs.Length || towerPrefabs[index] == null) return;
        if (index >= towerGridPositions.Length) return;

        Vector2Int gridPos = towerGridPositions[index];
        Cell cell = grid.GetCell(gridPos.x, gridPos.y);
        if (cell == null || !cell.IsFree)
        {
            Debug.LogWarning($"Клетка {gridPos} занята или не существует!");
            return;
        }

        GameObject obj = Instantiate(towerPrefabs[index]);
        Tower tower = obj.GetComponent<Tower>();

        if (tower != null)
        {
            tower.Initialize();
            cell.PlaceBuilding(tower);
            if (GameManager.Instance != null)
                GameManager.Instance.RegisterMainTower(index, tower);

            Debug.Log($"Игрок {index}: Башня создана в клетке {gridPos}");
        }
        else
        {
            Destroy(obj);
        }
    }

    private void StartStageTwo()
    {
        stageTwoStarted = true;
        Debug.Log("Этап 2: спавн новых мышек и башен запрещён. Генерация врагов активирована.");

    }
    public bool IsStageTwoStarted()
    {
        return stageTwoStarted;
    }

    public Vector3 GetMousePosition(int playerIndex)
    {
        if (playerIndex >= 0 && playerIndex < spawnedMice.Length && spawnedMice[playerIndex] != null)
            return spawnedMice[playerIndex].transform.position;
        return Vector3.zero;
    }
}
