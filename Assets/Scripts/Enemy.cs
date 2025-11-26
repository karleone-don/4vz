using UnityEngine;

// Базовый класс всех врагов
public abstract class Enemy : EnemyMover
{
    public int hp;
    public int damage;

    // Этот метод будет вызываться при создании врага
    public abstract void SetupEnemy();
}
