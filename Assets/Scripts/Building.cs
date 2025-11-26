using UnityEngine;

public abstract class Building : MonoBehaviour
{
    public int hp = 0;
    public int damage = 0;
    public int energyCost = 0;
    public int price = 0;

    // метод для инициализации здания
    public abstract void Initialize();
}
