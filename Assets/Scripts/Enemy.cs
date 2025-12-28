using System.Collections;
using UnityEngine;

// Базовый класс всех врагов
public abstract class Enemy : EnemyMover
{
    public int hp;
    public int damage;
    public int manaReward = 25;
    private HealthBar healthBar;
    private bool rewardGiven = false;

    // интервал между ударами по зданию (сек)
    public float attackInterval = 1f;

    // Этот метод будет вызываться при создании врага
    public abstract void SetupEnemy();

    // метод получения урона
    public void TakeDamage(int dmg)
    {
        hp -= dmg;
        if (healthBar == null)
            EnsureHealthBar();
        // Ensure the healthbar has a sensible max before setting hp.
        // If the bar was added before `hp` was initialized, attempt to set a correct max.
        try
        {
            int presumedPrevHp = hp + dmg;
            healthBar?.SetMaxHp(Mathf.Max(1, presumedPrevHp));
        }
        catch { }
        healthBar?.SetHp(hp);
        Debug.Log($"{name} получил {dmg} урона! Осталось HP: {hp}");

        if (SoundManager.Instance != null)
            SoundManager.Instance.PlayZombieHit();

        if (hp <= 0)
        {
            Debug.Log($"{name} уничтожен!");
            if (!rewardGiven && GameManager.Instance != null)
            {
                rewardGiven = true;
                GameManager.Instance.AddMana(manaReward);
            }
            if (SoundManager.Instance != null)
                SoundManager.Instance.PlayZombieDie();
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        StartCoroutine(DeferredInit());
    }

    private void Awake()
    {
        // ensure healthbar exists early
        EnsureHealthBar();
        Debug.Log($"Enemy.Awake ensured healthbar for '{gameObject.name}' (hp={hp})");
    }

    private System.Collections.IEnumerator DeferredInit()
    {
        // wait a few frames so derived SetupEnemy/Start can set hp
        int attempts = 0;
        while (hp <= 0 && attempts < 10)
        {
            attempts++;
            yield return null;
        }
        EnsureHealthBar();
        healthBar?.SetMaxHp(Mathf.Max(1, hp));
        healthBar?.SetHp(hp);
        Debug.Log($"Enemy.DeferredInit for '{gameObject.name}' hp={hp} (waited {attempts} frames)");
    }

    private void EnsureHealthBar()
    {
        if (healthBar != null) return;
        healthBar = GetComponent<HealthBar>();
        if (healthBar == null)
            healthBar = gameObject.AddComponent<HealthBar>();
    }

    // Атака зданий: если стоим на клетке с Building, останавливаемся и бьём
    private Building attackTarget = null;
    private Coroutine attackCoroutine = null;

    private IEnumerator AttackRoutine()
    {
        while (attackTarget != null)
        {
            if (attackTarget == null) break; // safety
            attackTarget.TakeDamage(damage);
            yield return new WaitForSeconds(attackInterval);
        }
        attackCoroutine = null;
    }

    // Обновление: вызываем движение из базового класса, если не атакуем.
    private void Update()
    {
        // Если мы не атакуем — выполняем поведение перемещения базового класса
        if (attackCoroutine == null)
        {
            // вызываем реализацию Update из EnemyMover, чтобы продолжить движение
            base.Update();
        }

        // Проверяем область вокруг позиции, чтобы надёжно находить здания и пушки
        float checkRadius = 0.6f; // increased for more reliable detection
        Collider2D[] hits = Physics2D.OverlapCircleAll((Vector2)transform.position, checkRadius);
        Building foundBuilding = null;

        Debug.Log($"{name} overlap check found {hits.Length} colliders (radius={checkRadius})");

        foreach (var h in hits)
        {
            if (h == null) continue;
            // ignore our own collider(s)
            if (h.gameObject == gameObject) continue;
            if (h.transform.IsChildOf(transform)) continue;

            // сначала смотрим на Cell
            Cell cell = h.GetComponent<Cell>();
            if (cell != null && cell.buildingOnCell != null)
            {
                foundBuilding = cell.buildingOnCell;
                break;
            }

            // иначе ищем Building на коллайдере или в родителях
            Building b = h.GetComponent<Building>() ?? h.GetComponentInParent<Building>();
            if (b != null)
            {
                foundBuilding = b;
                break;
            }
        }

        if (foundBuilding != null)
        {
            if (attackTarget != foundBuilding)
            {
                Debug.Log($"{name} started attacking {foundBuilding.name} (hp={foundBuilding.hp})");
                attackTarget = foundBuilding;
                if (attackCoroutine != null) StopCoroutine(attackCoroutine);
                attackCoroutine = StartCoroutine(AttackRoutine());
            }
            return; // если атакуем — не продолжаем проверку
        }
        else
        {
            // useful for debugging when nothing is found
            if (hits.Length > 0)
            {
                string names = "";
                foreach (var h2 in hits) if (h2 != null) names += h2.gameObject.name + ",";
                Debug.Log($"{name} found colliders but no buildings: {names}");
            }
            // fallback: search all Building instances and check distance
            Building[] all = FindObjectsOfType<Building>();
            float bestDist = float.MaxValue;
            Building bestB = null;
            foreach (var b in all)
            {
                if (b == null) continue;
                float d = Vector2.Distance(transform.position, b.transform.position);
                if (d < checkRadius && d < bestDist)
                {
                    bestDist = d; bestB = b;
                }
            }
            if (bestB != null)
            {
                Debug.Log($"{name} fallback found building {bestB.name} at dist={bestDist}");
                attackTarget = bestB;
                if (attackCoroutine != null) StopCoroutine(attackCoroutine);
                attackCoroutine = StartCoroutine(AttackRoutine());
                return;
            }
        }

        // Если здесь нет здания и была цель — прекращаем атаку
        if (attackTarget != null)
        {
            attackTarget = null;
            if (attackCoroutine != null)
            {
                StopCoroutine(attackCoroutine);
                attackCoroutine = null;
            }
        }
    }
}
