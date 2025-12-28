using UnityEngine;

public class EnemyMover : MonoBehaviour
{
    public float speed = 2f;
    public float arriveThreshold = 0.05f;
    private GridGenerator grid;
    private Vector2 targetWorldPos;
    private bool initialized = false;

    // priorityAxis: 0 = X first, 1 = Y first
    private int priorityAxis = 0;

    public void Initialize(GridGenerator gridGen, int spawnSide)
    {
        grid = gridGen;
        initialized = true;

        // вычислим целевую позицию как центр областии 3,3 - 4,4 (если они есть)
        Transform c33 = grid.transform.Find("Cell_3_3");
        Transform c44 = grid.transform.Find("Cell_4_4");

        Vector3 pos33 = c33.position;
        Vector3 pos44 = c44.position;

        // стартовая позиция врага
        Vector3 startPos = transform.position;

        // выбираем ближайшую по X
        float targetX = Mathf.Abs(startPos.x - pos33.x) < Mathf.Abs(startPos.x - pos44.x) ? pos33.x : pos44.x;
        // выбираем ближайшую по Y
        float targetY = Mathf.Abs(startPos.y - pos33.y) < Mathf.Abs(startPos.y - pos44.y) ? pos33.y : pos44.y;

        targetWorldPos = new Vector2(targetX, targetY);

        // приоритет оси зависит от стороны спавна:
        // если спавн слева/справа (0 или 1) — сначала по X; если сверху/снизу (2 или 3) — сначала по Y.
        if (spawnSide == 0 || spawnSide == 1) priorityAxis = 0;
        else priorityAxis = 1;

        // сразу ориентируем спрайт
        FaceTargetImmediate();
    }

    protected virtual void Update()
    {
        if (!initialized) return;

        Vector2 pos = transform.position;
        Vector2 toTarget = targetWorldPos - pos;

        if (toTarget.magnitude <= arriveThreshold)
        {
            // достигли центра
            return;
        }

        Vector2 move = Vector2.zero;

        // движение строго по осям: сначала приоритетная ось до выравнивания (целая сетка),
        // затем вторая ось.
        if (priorityAxis == 0) // X first
        {
            if (Mathf.Abs(toTarget.x) > 0.01f)
            {
                move.x = Mathf.Sign(toTarget.x);
            }
            else if (Mathf.Abs(toTarget.y) > 0.01f)
            {
                move.y = Mathf.Sign(toTarget.y);
            }
        }
        else // Y first
        {
            if (Mathf.Abs(toTarget.y) > 0.01f)
            {
                move.y = Mathf.Sign(toTarget.y);
            }
            else if (Mathf.Abs(toTarget.x) > 0.01f)
            {
                move.x = Mathf.Sign(toTarget.x);
            }
        }

        // Перемещение с заданной скоростью (делаем независимым от размера шага)
        Vector2 desired = (move.normalized) * speed * Time.deltaTime;
        // Не перескакиваем через цель — если остаётся меньше, двигаем до цели
        Vector2 newPos = pos + desired;
        // Если движение по X и расстояние до цели по X меньше шага — задвигаем ровно на цель по X
        if (move.x != 0 && Mathf.Abs(toTarget.x) < Mathf.Abs(desired.x))
            newPos.x = targetWorldPos.x;
        if (move.y != 0 && Mathf.Abs(toTarget.y) < Mathf.Abs(desired.y))
            newPos.y = targetWorldPos.y;

        transform.position = newPos;

        // Поворачиваемся к направлению движения
        if (move != Vector2.zero)
            RotateToDirection(move);
    }

    void RotateToDirection(Vector2 dir)
    {
        float angle = 0f;
        if (Mathf.Abs(dir.x) > 0f)
            angle = dir.x > 0 ? 0f : 180f;
        else if (Mathf.Abs(dir.y) > 0f)
            angle = dir.y > 0 ? 90f : -90f;

        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    void FaceTargetImmediate()
    {
        Vector2 toTarget = (targetWorldPos - (Vector2)transform.position);
        // выбрать ось приоритетно для первоначального лица к цели
        Vector2 dir = Vector2.zero;
        if (priorityAxis == 0)
        {
            dir.x = Mathf.Sign(toTarget.x);
            if (dir.x == 0) dir.y = Mathf.Sign(toTarget.y);
        }
        else
        {
            dir.y = Mathf.Sign(toTarget.y);
            if (dir.y == 0) dir.x = Mathf.Sign(toTarget.x);
        }

        if (dir != Vector2.zero) RotateToDirection(dir);
    }
}
