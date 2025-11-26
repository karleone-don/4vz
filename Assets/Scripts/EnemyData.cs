using UnityEngine;

[CreateAssetMenu(fileName = "NewEnemyData", menuName = "Enemy/Enemy Data", order = 1)]
public class EnemyData : ScriptableObject
{
    [Header("Характеристики Врага")]
    public string enemyName = "Базовый Враг";
    public float maxHealth = 100f;
    public float moveSpeed = 1f;
    public float attackDamage = 10f;
}