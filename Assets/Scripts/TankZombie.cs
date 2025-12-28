using UnityEngine;

public class TankZombie : Enemy
{
    // Медленный, но живучий зомби
    public float moveSpeed = 1.5f;
    [Header("Visual")]
    [SerializeField] private string moveSpriteResourceFolder = "zombie_tank";
    [SerializeField] private float moveAnimFrameTime = 0.06f;

    public override void SetupEnemy()
    {
        hp = 250;
        damage = 25;
        name = "TankZombie";
        manaReward = 80;
        speed = moveSpeed;
        ApplyCustomMoveSprites();
        UseMoveSpritesForAttack();
        Debug.Log($"{name} настроен: HP={hp}, Damage={damage}");
    }

    private void Start()
    {
        SetupEnemy();
    }

    private void ApplyCustomMoveSprites()
    {
        if (!string.IsNullOrEmpty(moveSpriteResourceFolder))
            LoadMoveSpritesFromResources(moveSpriteResourceFolder, moveAnimFrameTime);
    }
}
