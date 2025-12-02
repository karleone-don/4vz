using UnityEngine;

// Базовый класс всех врагов
public abstract class Enemy : EnemyMover
{
    public int hp;
    public int damage;

    // Этот метод будет вызываться при создании врага
    public abstract void SetupEnemy();
    // В класс Enemy добавь метод:
    public void TakeDamage(int dmg)
    {
        hp -= dmg;
        Debug.Log($"{name} получил {dmg} урона! Осталось HP: {hp}");

        if (hp <= 0)
        {
            Debug.Log($"{name} уничтожен!");
            Destroy(gameObject);
        }
    }
}
