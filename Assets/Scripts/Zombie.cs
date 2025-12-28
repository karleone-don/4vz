using UnityEngine;

public class Zombie : Enemy
{
    public override void SetupEnemy()
    {
        hp = 100;
        damage = 20;
        speed = 0.5f; // наследуемая переменная из EnemyMover
        manaReward = 30;
    }

    void Start()
    {
        SetupEnemy();
    }
}
