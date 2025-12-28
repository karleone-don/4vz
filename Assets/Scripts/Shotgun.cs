using System.Collections.Generic;
using UnityEngine;

public class Shotgun : Building
{
    [Header("Shotgun Settings")]
    public float scanRange = 6f;
    public float fireCooldown = 1.1f;
    public int pellets = 6;
    public int pelletDamage = 8;
    public float spreadRadius = 1.5f;
    public float rotationSpeed = 360f;
    public float alignThreshold = 0.12f;

    [Header("Visuals (Optional)")]
    public GameObject bulletPrefab;
    public Transform turret;
    public Transform muzzle;

    private float fireTimer;
    private Transform currentTarget;

    private void Awake()
    {
        hp = 140;
        damage = pelletDamage;
        energyCost = 8;
        price = 90;

        EnsureHealthBar();
        RefreshHealthBar();

        if (turret == null)
            turret = transform;
        if (muzzle == null)
            muzzle = turret;
    }

    public override void Initialize()
    {
        Debug.Log("Shotgun initialized!");
    }

    private void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver)
            return;

        fireTimer -= Time.deltaTime;
        Enemy target = FindTargetInLine();
        currentTarget = target != null ? target.transform : null;

        if (currentTarget == null)
            return;

        RotateTurretSmoothly(currentTarget.position);

        if (fireTimer > 0f || !IsAimedAtTarget(currentTarget.position))
            return;

        bool isVertical;
        if (!IsAligned(currentTarget.position, out isVertical))
            return;

        if (GameManager.Instance != null)
        {
            Vector2 from = muzzle != null ? (Vector2)muzzle.position : (Vector2)transform.position;
            Vector2 to = currentTarget.position;
            if (GameManager.Instance.IsShotBlockedByMainTower(from, to, transform))
                return;
        }

        List<Enemy> candidates = FindEnemiesInAxisSpread(currentTarget.position, isVertical);
        if (candidates.Count == 0 && target != null)
            candidates.Add(target);

        for (int i = 0; i < pellets; i++)
        {
            Enemy chosen = candidates[Random.Range(0, candidates.Count)];
            if (chosen != null)
            {
                if (bulletPrefab != null)
                {
                    GameObject bulletGO = Instantiate(bulletPrefab, muzzle.position, muzzle.rotation);
                    if (!bulletGO.activeSelf)
                        bulletGO.SetActive(true);
                    Bullet bullet = bulletGO.GetComponent<Bullet>();
                    if (bullet != null)
                    {
                        bullet.SetTarget(chosen.transform);
                        bullet.damage = pelletDamage;
                    }
                }
                else
                {
                    chosen.TakeDamage(pelletDamage);
                }
            }
        }

        if (SoundManager.Instance != null)
            SoundManager.Instance.PlayShoot();

        fireTimer = fireCooldown;
    }

    private Enemy FindTargetInLine()
    {
        Enemy[] enemies = FindObjectsOfType<Enemy>();
        Enemy best = null;
        float bestDist = float.MaxValue;

        foreach (Enemy e in enemies)
        {
            if (e == null) continue;
            Vector2 dir = e.transform.position - turret.position;
            bool aligned = Mathf.Abs(dir.x) <= alignThreshold || Mathf.Abs(dir.y) <= alignThreshold;
            if (!aligned) continue;

            float d = dir.magnitude;
            if (d <= scanRange && d < bestDist)
            {
                bestDist = d;
                best = e;
            }
        }

        return best;
    }

    private List<Enemy> FindEnemiesInAxisSpread(Vector2 center, bool vertical)
    {
        List<Enemy> list = new List<Enemy>();
        Enemy[] enemies = FindObjectsOfType<Enemy>();
        float r2 = spreadRadius * spreadRadius;

        foreach (Enemy e in enemies)
        {
            if (e == null) continue;
            Vector3 pos = e.transform.position;
            if (vertical)
            {
                if (Mathf.Abs(pos.x - center.x) > alignThreshold) continue;
            }
            else
            {
                if (Mathf.Abs(pos.y - center.y) > alignThreshold) continue;
            }

            float d2 = (pos - (Vector3)center).sqrMagnitude;
            if (d2 <= r2)
                list.Add(e);
        }

        return list;
    }

    private bool IsAligned(Vector3 targetPos, out bool vertical)
    {
        Vector2 dir = targetPos - turret.position;
        if (Mathf.Abs(dir.x) <= alignThreshold)
        {
            vertical = true;
            return true;
        }
        if (Mathf.Abs(dir.y) <= alignThreshold)
        {
            vertical = false;
            return true;
        }

        vertical = false;
        return false;
    }

    private void RotateTurretSmoothly(Vector3 targetPos)
    {
        bool vertical;
        if (!IsAligned(targetPos, out vertical))
            return;

        Vector2 desiredDir = vertical
            ? new Vector2(0f, Mathf.Sign(targetPos.y - turret.position.y))
            : new Vector2(Mathf.Sign(targetPos.x - turret.position.x), 0f);

        float targetAngle = Mathf.Atan2(desiredDir.y, desiredDir.x) * Mathf.Rad2Deg - 90f;
        float currentAngle = turret.eulerAngles.z;
        float newAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, rotationSpeed * Time.deltaTime);
        turret.rotation = Quaternion.Euler(0f, 0f, newAngle);
    }

    private bool IsAimedAtTarget(Vector3 targetPos)
    {
        Vector2 dirToTarget = ((Vector2)targetPos - (Vector2)turret.position).normalized;
        Vector2 turretDir = turret.up;
        return Vector2.Angle(turretDir, dirToTarget) < 1f;
    }
}
