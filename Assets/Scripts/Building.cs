using UnityEngine;

public abstract class Building : MonoBehaviour
{
    public static bool SuppressHealthBarCreation = false;
    public int hp = 0;
    public int damage = 0;
    public int energyCost = 0;
    public int price = 0;

    private HealthBar healthBar;
    private bool healthBarDisabled;

    // чтобы OnDestroy не трогал уже “разбирающийся” объект
    private int cachedId;
    private string cachedName;

    // защита от повторной регистрации
    private bool registered;

    // метод для инициализации здания
    public abstract void Initialize();

    private void Awake()
    {
        cachedId = GetInstanceID();
        cachedName = name;

        EnsureHealthBar();
        Debug.Log($"Building.Awake ensured healthbar for '{cachedName}' (hp={hp})");
    }

    private void Start()
    {
        // Ждём один кадр/несколько кадров, чтобы Initialize() успел выставить hp
        StartCoroutine(DeferredInit());
    }

    private System.Collections.IEnumerator DeferredInit()
    {
        yield return null;

        int attempts = 0;
        while (hp <= 0 && attempts < 10)
        {
            attempts++;
            yield return null;
        }

        EnsureHealthBar();
        healthBar?.SetMaxHp(Mathf.Max(1, hp));
        healthBar?.SetHp(hp);

        Debug.Log($"Building.DeferredInit initialized healthbar for '{cachedName}' hp={hp} (waited {attempts} frames)");

        // register with GameManager after initialization
        var gm = GameManager.Instance;
        if (!registered && gm != null)
        {
            gm.RegisterBuilding(this);
            registered = true;
            Debug.Log($"Building registered with GameManager: '{cachedName}' (hp={hp})");
        }
    }

    // Применить урон к зданию
    public void TakeDamage(int dmg)
    {
        hp -= dmg;

        if (healthBar == null)
            EnsureHealthBar();

        healthBar?.SetHp(hp);

        Debug.Log($"{cachedName} получил {dmg} урона. HP={hp}");

        if (hp <= 0)
        {
            Debug.Log($"{cachedName} разрушен!");

            if (GameManager.Instance != null)
                GameManager.Instance.NotifyBuildingDestroyed(this);

            // Если стоим в клетке, очистим ссылку
            if (transform.parent != null)
            {
                Cell parentCell = transform.parent.GetComponent<Cell>();
                if (parentCell != null && parentCell.buildingOnCell == this)
                    parentCell.buildingOnCell = null;
            }

            Destroy(gameObject);
        }
    }

    public void EnsureHealthBar()
    {
        if (healthBarDisabled || SuppressHealthBarCreation) return;
        if (healthBar != null) return;

        healthBar = GetComponent<HealthBar>();
        if (healthBar == null)
            healthBar = gameObject.AddComponent<HealthBar>();
    }

    public void RefreshHealthBar()
    {
        if (healthBarDisabled) return;
        EnsureHealthBar();
        if (healthBar != null)
        {
            healthBar.SetMaxHp(Mathf.Max(1, hp));
            healthBar.SetHp(hp);
        }
    }

    public void DisableHealthBar()
    {
        healthBarDisabled = true;
        if (healthBar != null)
        {
            Destroy(healthBar);
            healthBar = null;
        }
    }

    private void OnDestroy()
    {
        // если GameManager уже не существует/выгружается — просто выходим
        var gm = GameManager.Instance;
        if (gm == null) return;

        // unregister по id (без доступа к name/gameObject в момент уничтожения)
        gm.UnregisterBuildingById(cachedId, cachedName);
    }
}
