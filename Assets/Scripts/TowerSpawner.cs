// TowerSpawner.cs
using UnityEngine;
using System.Collections;

public class TowerSpawner : MonoBehaviour
{
    public GridGenerator grid;
    public GameObject[] towerPrefabs; // 4 префаба башен

    IEnumerator Start()
    {
        if (grid == null)
        {
            Debug.LogError("Grid не назначен!");
            yield break;
        }

        if (towerPrefabs == null || towerPrefabs.Length < 4)
        {
            Debug.LogError("towerPrefabs не назначен или меньше 4 элементов!");
            yield break;
        }

        // ждём один кадр, чтобы все клетки успели создаться
        yield return null;

        SpawnFixedTowers();
    }

    void SpawnFixedTowers()
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
                Debug.LogWarning($"Клетка {positions[i].x},{positions[i].y} не найдена!");
                continue;
            }

            if (!cell.IsFree)
            {
                Debug.LogWarning($"Клетка {positions[i].x},{positions[i].y} занята!");
                continue;
            }

            GameObject obj = Instantiate(towerPrefabs[i]);
            Tower tower = obj.GetComponent<Tower>();
            if (tower == null)
            {
                Debug.LogError($"Префаб {towerPrefabs[i].name} не содержит компонент Tower!");
                Destroy(obj);
                continue;
            }

            tower.Initialize();
            cell.PlaceBuilding(tower);
            Debug.Log($"Башня {towerPrefabs[i].name} установлена на клетку {positions[i].x},{positions[i].y}");
        }
    }
}
