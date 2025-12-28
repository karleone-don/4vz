using UnityEngine;
using UnityEngine.UI;

// Simple UI script: populate a panel with buttons for each weapon prefab.
public class WeaponSelectionUI : MonoBehaviour
{
    [Tooltip("Parent panel (RectTransform) to hold weapon buttons")]
    public RectTransform panel;

    [Tooltip("A simple Button prefab (must have a Text child)" )]
    public Button buttonPrefab;

    [Tooltip("List of building/weapon prefabs to choose from")]
    public GameObject[] weaponPrefabs;

    private void Start()
    {
        if (panel == null || buttonPrefab == null)
        {
            Debug.LogWarning("WeaponSelectionUI: assign panel and buttonPrefab in inspector.");
            return;
        }

        // Clear existing
        for (int i = panel.childCount - 1; i >= 0; i--) Destroy(panel.GetChild(i).gameObject);

        foreach (GameObject prefab in weaponPrefabs)
        {
            Button btn = Instantiate(buttonPrefab, panel);
            Text txt = btn.GetComponentInChildren<Text>();
            if (txt != null) txt.text = prefab != null ? prefab.name : "<None>";

            GameObject captured = prefab;
            btn.onClick.AddListener(() => OnWeaponClicked(captured));
        }
    }

    private void OnWeaponClicked(GameObject prefab)
    {
        if (BuildingSelector.Instance != null)
        {
            BuildingSelector.Instance.SelectBuilding(prefab);
        }
        else
        {
            Debug.LogWarning("BuildingSelector instance not found in scene.");
        }
    }
}
