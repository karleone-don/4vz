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

    // звук попадания по зомби
    if (SoundManager.Instance != null)
        SoundManager.Instance.PlayZombieHit();

    if (hp <= 0)
    {
        Debug.Log($"{name} уничтожен!");

        // звук смерти зомби
        if (SoundManager.Instance != null)
            SoundManager.Instance.PlayZombieDie();

        Destroy(gameObject);
    }
}

}
