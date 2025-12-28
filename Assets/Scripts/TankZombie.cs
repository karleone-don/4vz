using UnityEngine;

public class TankZombie : Enemy
{
    // Медленный, но живучий зомби
    public float moveSpeed = 1.5f;

    public override void SetupEnemy()
    {
        hp = 250;
        damage = 25;
        name = "TankZombie";
        manaReward = 80;
        Debug.Log($"{name} настроен: HP={hp}, Damage={damage}");
    }

    private void Start()
    {
        SetupEnemy();
    }
}
