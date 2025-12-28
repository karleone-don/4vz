using UnityEngine;

public class Cannon : Building
{
    [Header("Настройки пушки")]
    public float scanRange = 100f;
    public float fireCooldown = 0.5f;
    public float rotationSpeed = 180f; // градусов в секунду

    [Header("Снаряд и дуло")]
    public GameObject bulletPrefab; // префаб снаряда
    public Transform turret; // башня, которая крутится
    public Transform muzzle; // точка спавна снаряда


    private float fireTimer = 0f;
    private Transform currentTarget;

    private void Awake()
    {
        hp = 100;
        damage = 20;
        energyCost = 5;
        price = 50;

        // ensure healthbar immediately
        EnsureHealthBar();
        RefreshHealthBar();
        Debug.Log($"Cannon.Awake refreshed healthbar for '{gameObject.name}' hp={hp}");

        if (turret == null)
            turret = transform;
        if (muzzle == null)
            muzzle = turret;
    }

    private void Update()
    {
        fireTimer -= Time.deltaTime;

        Enemy target = FindTargetInLine();

        // Если цель сменилась или пропала
        if (target != null ? target.transform != currentTarget : currentTarget != null)
        {
            currentTarget = target?.transform;
        }

        // Плавно поворачиваем башню
        if (currentTarget != null)
        {
            RotateTurretSmoothly(currentTarget.position);
        }

        // Стрельба ТОЛЬКО когда полностью остановилась и перезарядка прошла
        if (currentTarget != null && 
            fireTimer <= 0f && 
            IsAimedAtTarget())
        {
            if (GameManager.Instance != null)
            {
                Vector2 from = muzzle != null ? (Vector2)muzzle.position : (Vector2)transform.position;
                Vector2 to = currentTarget.position;
                if (GameManager.Instance.IsShotBlockedByMainTower(from, to, transform))
                    return;
            }
            Fire(currentTarget);
            fireTimer = fireCooldown;
        }
    }

    private void RotateTurretSmoothly(Vector3 targetPos)
    {
        Vector2 direction = targetPos - turret.position;
        
        // Только 4 направления
        Vector2 desiredDir;
        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
        {
            desiredDir = new Vector2(Mathf.Sign(direction.x), 0);
        }
        else
        {
            desiredDir = new Vector2(0, Mathf.Sign(direction.y));
        }

        // Целевой угол
        float targetAngle = Mathf.Atan2(desiredDir.y, desiredDir.x) * Mathf.Rad2Deg - 90f;

        // ПЛАВНЫЙ поворот
        float currentAngle = turret.eulerAngles.z;
        float newAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, rotationSpeed * Time.deltaTime);
        turret.rotation = Quaternion.Euler(0, 0, newAngle);
    }

    private bool IsAimedAtTarget()
    {
        if (currentTarget == null) return false;

        Vector2 dirToTarget = (currentTarget.position - turret.position).normalized;
        Vector2 turretDir = turret.up;

        return Vector2.Angle(turretDir, dirToTarget) < 1f;
    }

    private Enemy FindTargetInLine()
    {
        Enemy[] enemies = FindObjectsOfType<Enemy>();
        Enemy best = null;
        float bestDist = Mathf.Infinity;

        foreach (Enemy e in enemies)
        {
            Vector2 dir = e.transform.position - transform.position;
            bool sameX = Mathf.Abs(dir.x) < 0.1f;
            bool sameY = Mathf.Abs(dir.y) < 0.1f;

            if (!sameX && !sameY) continue;

            float dist = dir.magnitude;
            if (dist < bestDist && dist <= scanRange)
            {
                bestDist = dist;
                best = e;
            }
        }

        return best;
    }

    private void Fire(Transform target)
{
    if (bulletPrefab == null)
    {
        Debug.LogWarning("Нет префаба снаряда!");
        return;
    }

    Debug.Log($"Пушка выстрелила в {target.name}!");


    // Безопасный вызов звука
    if (SoundManager.Instance != null)
    {
        SoundManager.Instance.PlayShoot();
    }


    // Создаём снаряд
    GameObject bulletGO = Instantiate(bulletPrefab, muzzle.position, muzzle.rotation);
    if (!bulletGO.activeSelf)
        bulletGO.SetActive(true);
    Bullet bullet = bulletGO.GetComponent<Bullet>();
    if (bullet != null)
    {
        bullet.SetTarget(target);
        bullet.damage = damage;
    }
}


    public override void Initialize()
    {
        Debug.Log("Cannon initialized!");
    }
}
