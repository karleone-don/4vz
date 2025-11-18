using UnityEngine;

public class GridGenerator : MonoBehaviour
{
    public int width = 8;
    public int height = 8;
    public float cellSize = 1f;
    public Sprite sprite1;
    public Sprite sprite2;
    public float gap = 0.1f;
    public float size = 1f;

    void Awake()
    {
        GenerateGrid();
    }

    void GenerateGrid()
    {
        float offsetX = (width * (cellSize + gap) - gap) / size;
        float offsetY = (height * (cellSize + gap) - gap) / size;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                GameObject cell = new GameObject($"Cell_{x}_{y}");
                float posX = x * (cellSize + gap) - offsetX + cellSize/size;
                float posY = y * (cellSize + gap) - offsetY + cellSize/size;
                cell.transform.position = new Vector3(posX, posY, 0);
                cell.transform.parent = transform;

                SpriteRenderer sr = cell.AddComponent<SpriteRenderer>();
                sr.sprite = ((x + y) % 2 == 0) ? sprite1 : sprite2;

                Cell cellComp = cell.AddComponent<Cell>();
                cellComp.x = x;
                cellComp.y = y;

                cell.transform.localScale = new Vector3(cellSize*5f, cellSize*5f, 1);
            }
        }
    }
}
