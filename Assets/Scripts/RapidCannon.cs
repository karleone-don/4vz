using UnityEngine;

public class RapidCannon : Cannon
{
    private void Awake()
    {
        // Настройки для быстрой пушки
        hp = 80;
        damage = 10;
        energyCost = 7;
        price = 75;

        scanRange = 120f;
        fireCooldown = 0.2f;
        rotationSpeed = 720f;

        if (turret == null)
            turret = transform;
        if (muzzle == null)
            muzzle = turret;
    }

    public override void Initialize()
    {
        Debug.Log("RapidCannon initialized!");
    }
}
