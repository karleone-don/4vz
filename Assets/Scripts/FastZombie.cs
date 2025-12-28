using UnityEngine;

public class FastZombie : Enemy
{
    // Быстрый, но хрупкий зомби
    public float moveSpeed = 4f;

    public override void SetupEnemy()
    {
        hp = 30;
        damage = 5;
        name = "FastZombie";
        manaReward = 20;
        Debug.Log($"{name} настроен: HP={hp}, Damage={damage}");
    }

    private void Start()
    {
        SetupEnemy();
    }
}
