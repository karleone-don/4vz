using UnityEngine;

public class Bullet : MonoBehaviour
{
    public float speed = 5f; // скорость полёта
    public int damage = 20;

    private Transform target;

    public void SetTarget(Transform t)
    {
        target = t;
    }

    private void Update()
    {
        if (target == null)
        {
            Destroy(gameObject);
            return;
        }

        // Летим ПРЯМО К ЦЕЛИ (даже если она двигается)
        Vector2 dir = (target.position - transform.position).normalized;
        transform.position += (Vector3)dir * speed * Time.deltaTime;

        // ✅ ПОПАДАНИЕ: если близко — урон и уничтожение
        if (Vector2.Distance(transform.position, target.position) < 0.5f)
        {
            Enemy enemy = target.GetComponent<Enemy>();
            if (enemy != null)
            {
                enemy.TakeDamage(damage); // НЕ мгновенно, а через метод!
            }
            Destroy(gameObject);
        }

        // Самоуничтожение, если далеко улетел
        if (Vector2.Distance(transform.position, target.position) > 50f)
        {
            Destroy(gameObject);
        }
    }
}