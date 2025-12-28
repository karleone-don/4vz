using UnityEngine;

public class FastZombie : Enemy
{
    // Быстрый, но хрупкий зомби
    public float moveSpeed = 4f;
    [Header("Visual")]
    [SerializeField] private string moveSpriteResourceFolder = "zombie_soldier";
    [SerializeField] private float moveAnimFrameTime = 0.06f;

    public override void SetupEnemy()
    {
        hp = 30;
        damage = 5;
        name = "FastZombie";
        manaReward = 20;
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
