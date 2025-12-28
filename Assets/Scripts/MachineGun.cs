using UnityEngine;

public class MachineGun : Cannon
{
    private void Awake()
    {
        hp = 90;
        damage = 6;
        energyCost = 6;
        price = 80;

        scanRange = 120f;
        fireCooldown = 0.12f;
        rotationSpeed = 720f;

        EnsureHealthBar();
        RefreshHealthBar();

        if (turret == null)
            turret = transform;
        if (muzzle == null)
            muzzle = turret;
    }

    public override void Initialize()
    {
        Debug.Log("MachineGun initialized!");
    }
}
