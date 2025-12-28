using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

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

    [Header("Attack Animation")]
    [SerializeField] private Sprite[] attackSprites;
    [SerializeField] private float attackFrameTime = 0.06f;
    [SerializeField] private string attackSpriteFolder = "Assets/Sprites/zombie_attack";
    [SerializeField] private string attackResourcePrefix = "zombie_attack/attack_";
    [SerializeField] private int attackResourceFrameCount = 8;

    [Header("Move Animation")]
    [SerializeField] private Sprite[] moveSprites;
    [SerializeField] private float moveFrameTime = 0.08f;

    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private Coroutine attackAnimCoroutine;
    private Coroutine punchPulseCoroutine;
    private Vector3 baseScale;
    private float moveFrameTimer;
    private int moveFrameIndex;
    private bool usingMoveSprites;

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

        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        baseScale = transform.localScale;
        moveFrameTimer = 0f;
        moveFrameIndex = 0;
        usingMoveSprites = false;

        TryLoadAttackSprites();
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
            PlayAttackAnimation();
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
            Vector3 prevPos = transform.position;
            base.Update();
            UpdateMoveAnimation(prevPos);
        }
        else
        {
            UpdateMoveAnimation(transform.position);
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

    private void PlayAttackAnimation()
    {
        if (attackSprites != null && attackSprites.Length > 0 && spriteRenderer != null)
        {
            if (attackAnimCoroutine != null) StopCoroutine(attackAnimCoroutine);
            attackAnimCoroutine = StartCoroutine(AttackAnimRoutine());
        }
        else
        {
            if (punchPulseCoroutine != null) StopCoroutine(punchPulseCoroutine);
            punchPulseCoroutine = StartCoroutine(PunchPulseRoutine());
        }
    }

    private IEnumerator AttackAnimRoutine()
    {
        if (animator != null)
            animator.enabled = false;

        for (int i = 0; i < attackSprites.Length; i++)
        {
            spriteRenderer.sprite = attackSprites[i];
            yield return new WaitForSeconds(attackFrameTime);
        }

        if (animator != null && !usingMoveSprites)
            animator.enabled = true;

        attackAnimCoroutine = null;
    }

    private IEnumerator PunchPulseRoutine()
    {
        float duration = Mathf.Max(0.08f, Mathf.Min(0.2f, attackInterval * 0.4f));
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float scale = 1f + 0.08f * Mathf.Sin(t * Mathf.PI);
            transform.localScale = baseScale * scale;
            yield return null;
        }

        transform.localScale = baseScale;
        punchPulseCoroutine = null;
    }

    private void TryLoadAttackSprites()
    {
        if (attackSprites != null && attackSprites.Length > 0) return;

#if UNITY_EDITOR
        if (!string.IsNullOrEmpty(attackSpriteFolder))
        {
            string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { attackSpriteFolder });
            if (guids != null && guids.Length > 0)
            {
                List<Sprite> sprites = new List<Sprite>();
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    if (s != null) sprites.Add(s);
                }
                sprites.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
                attackSprites = sprites.ToArray();
            }
        }
#endif

        if (attackSprites == null || attackSprites.Length == 0)
            attackSprites = LoadAttackSpritesFromResources();
    }

    private Sprite[] LoadAttackSpritesFromResources()
    {
        List<Sprite> sprites = new List<Sprite>();

        if (!string.IsNullOrEmpty(attackResourcePrefix) && attackResourceFrameCount > 0)
        {
            for (int i = 0; i < attackResourceFrameCount; i++)
            {
                Sprite s = Resources.Load<Sprite>($"{attackResourcePrefix}{i}");
                if (s != null) sprites.Add(s);
            }
        }

        if (sprites.Count == 0)
        {
            Sprite[] fromSheet = Resources.LoadAll<Sprite>("zombie_attack");
            if (fromSheet != null && fromSheet.Length > 0)
            {
                sprites.AddRange(fromSheet);
                sprites.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            }
        }

        return sprites.Count > 0 ? sprites.ToArray() : null;
    }

    protected void LoadMoveSpritesFromResources(string resourceFolder, float frameTime)
    {
        if (moveSprites != null && moveSprites.Length > 0) return;
        if (string.IsNullOrEmpty(resourceFolder)) return;

        Sprite[] loaded = Resources.LoadAll<Sprite>(resourceFolder);
        if (loaded == null || loaded.Length == 0) return;

        System.Array.Sort(loaded, CompareSpritesByName);
        ApplyMoveSprites(loaded, frameTime);
    }

    protected void ApplyMoveSprites(Sprite[] sprites, float frameTime)
    {
        if (sprites == null || sprites.Length == 0) return;
        moveSprites = sprites;
        moveFrameTime = Mathf.Max(0.01f, frameTime);
        moveFrameIndex = 0;
        moveFrameTimer = 0f;
        usingMoveSprites = true;

        if (spriteRenderer != null)
            spriteRenderer.sprite = moveSprites[0];
        if (animator != null)
            animator.enabled = false;
    }

    protected void UseMoveSpritesForAttack()
    {
        if (moveSprites == null || moveSprites.Length == 0) return;
        attackSprites = moveSprites;
        attackFrameTime = moveFrameTime;
    }

    private void UpdateMoveAnimation(Vector3 previousPos)
    {
        if (moveSprites == null || moveSprites.Length == 0 || spriteRenderer == null) return;
        if (attackCoroutine != null || attackAnimCoroutine != null) return;

        Vector3 currentPos = transform.position;
        bool isMoving = (currentPos - previousPos).sqrMagnitude > 0.000001f;

        if (!isMoving)
        {
            if (moveFrameIndex >= moveSprites.Length) moveFrameIndex = 0;
            spriteRenderer.sprite = moveSprites[moveFrameIndex];
            return;
        }

        moveFrameTimer += Time.deltaTime;
        if (moveFrameTimer >= moveFrameTime)
        {
            moveFrameTimer = 0f;
            moveFrameIndex = (moveFrameIndex + 1) % moveSprites.Length;
            spriteRenderer.sprite = moveSprites[moveFrameIndex];
        }
    }

    private static int CompareSpritesByName(Sprite a, Sprite b)
    {
        int na = ExtractTrailingNumber(a != null ? a.name : null);
        int nb = ExtractTrailingNumber(b != null ? b.name : null);
        if (na >= 0 && nb >= 0 && na != nb) return na.CompareTo(nb);
        return string.CompareOrdinal(a != null ? a.name : "", b != null ? b.name : "");
    }

    private static int ExtractTrailingNumber(string name)
    {
        if (string.IsNullOrEmpty(name)) return -1;
        int i = name.Length - 1;
        while (i >= 0 && char.IsDigit(name[i])) i--;
        if (i == name.Length - 1) return -1;
        if (int.TryParse(name.Substring(i + 1), out int num)) return num;
        return -1;
    }
}
