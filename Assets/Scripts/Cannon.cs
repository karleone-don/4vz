using UnityEngine;

public class Cannon : Building
{
    private void Awake()
    {
        // можно поставить дефолтные параметры
        hp = 100;
        damage = 20;
        energyCost = 5;
        price = 50;
    }

    public override void Initialize()
    {
        // если хочешь — тут можешь менять параметры динамически
        // например при улучшениях или покупке
        Debug.Log("Cannon initialized!");
    }
}
