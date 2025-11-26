// GridGenerator.cs
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
    public Cell[,] gridArray;

    void Awake()
    {
        gridArray = new Cell[width, height];
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
                GameObject cellObj = new GameObject($"Cell_{x}_{y}");
                float posX = x * (cellSize + gap) - offsetX + cellSize / size;
                float posY = y * (cellSize + gap) - offsetY + cellSize / size;
                cellObj.transform.position = new Vector3(posX, posY, 0);
                cellObj.transform.parent = transform;

                SpriteRenderer sr = cellObj.AddComponent<SpriteRenderer>();
                sr.sprite = ((x + y) % 2 == 0) ? sprite1 : sprite2;

                Cell cellComp = cellObj.AddComponent<Cell>();
                cellComp.x = x;
                cellComp.y = y;

                cellObj.transform.localScale = new Vector3(cellSize * 5f, cellSize * 5f, 1);

                gridArray[x, y] = cellComp; // важно!
            }
        }
    }

    public Cell GetCell(int x, int y)
    {
        return gridArray[x, y];
    }
}
