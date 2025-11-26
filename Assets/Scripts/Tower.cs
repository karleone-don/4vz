using UnityEngine;

public class Tower : Building
{
    public Color towerColor = Color.white;

    public override void Initialize()
    {
        GetComponent<SpriteRenderer>().color = towerColor;
        hp = 200;
        damage = 20;
        energyCost = 5;
        price = 50;
    }
}
