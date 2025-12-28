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
        // ensure and refresh healthbar now that hp is set
        EnsureHealthBar();
        RefreshHealthBar();
        Debug.Log($"Tower.Initialize refreshed healthbar for '{gameObject.name}' hp={hp}");
    }
}
