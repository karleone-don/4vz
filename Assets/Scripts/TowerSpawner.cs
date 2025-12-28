using UnityEngine;
using System.Collections;

public class TowerSpawner : MonoBehaviour
{
    public GridGenerator grid;
    public GameObject[] towerPrefabs; // 4 префаба башен

    private IEnumerator Start()
    {
        if (grid == null)
        {
            Debug.LogError("TowerSpawner: Grid не назначен!");
            yield break;
        }

        if (towerPrefabs == null || towerPrefabs.Length < 4)
        {
            Debug.LogError("TowerSpawner: towerPrefabs не назначен или меньше 4 элементов!");
            yield break;
        }

        // ждём один кадр, чтобы все клетки успели создаться
        yield return null;

        SpawnFixedTowers();
    }

    private void SpawnFixedTowers()
    {
        Vector2Int[] positions =
        {
            new Vector2Int(3,3),
            new Vector2Int(3,4),
            new Vector2Int(4,3),
            new Vector2Int(4,4)
        };

        for (int i = 0; i < positions.Length; i++)
        {
            Cell cell = grid.GetCell(positions[i].x, positions[i].y);
            if (cell == null)
            {
                Debug.LogWarning($"TowerSpawner: клетка {positions[i].x},{positions[i].y} не найдена!");
                continue;
            }

            if (!cell.IsFree)
            {
                Debug.LogWarning($"TowerSpawner: клетка {positions[i].x},{positions[i].y} занята!");
                continue;
            }

            GameObject prefab = towerPrefabs[i];
            if (prefab == null)
            {
                Debug.LogError($"TowerSpawner: towerPrefabs[{i}] = null!");
                continue;
            }

            GameObject obj = Instantiate(prefab);
            Tower tower = obj.GetComponent<Tower>();
            if (tower == null)
            {
                Debug.LogError($"TowerSpawner: префаб {prefab.name} не содержит компонент Tower!");
                Destroy(obj);
                continue;
            }

            tower.Initialize();
            cell.PlaceBuilding(tower);
            if (GameManager.Instance != null)
                GameManager.Instance.RegisterMainTower(i, tower);

            Debug.Log($"TowerSpawner: башня {prefab.name} установлена на клетку {positions[i].x},{positions[i].y}");
        }
    }
}
