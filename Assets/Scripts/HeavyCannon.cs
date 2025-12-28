using UnityEngine;

public class HeavyCannon : Cannon
{
    private void Awake()
    {
        // Настройки для тяжёлой пушки
        hp = 220;
        damage = 55;
        energyCost = 12;
        price = 150;

        scanRange = 140f;
        fireCooldown = 1.2f;
        rotationSpeed = 90f;

        if (turret == null)
            turret = transform;
        if (muzzle == null)
            muzzle = turret;
    }

    public override void Initialize()
    {
        Debug.Log("HeavyCannon initialized!");
    }
}
