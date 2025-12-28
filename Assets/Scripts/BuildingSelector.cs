using UnityEngine;

// Singleton to hold currently selected building prefab
public class BuildingSelector : MonoBehaviour
{
    public static BuildingSelector Instance { get; private set; }

    private GameObject selectedPrefab;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void SelectBuilding(GameObject prefab)
    {
        selectedPrefab = prefab;
        Debug.Log($"Selected building: {(prefab!=null?prefab.name:"<null>")}");
    }

    public GameObject GetSelectedBuilding()
    {
        return selectedPrefab;
    }
}
