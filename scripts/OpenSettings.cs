using UnityEngine;

public class OpenSettings : MonoBehaviour
{
    [SerializeField] private GameObject settingsPanelPrefab;

    private static GameObject currentPanel;   // ← статическая переменная

    public void ShowSettings()
    {
        if (currentPanel != null) return;

        currentPanel = Instantiate(settingsPanelPrefab, GameObject.Find("Canvas").transform);
        currentPanel.transform.SetAsLastSibling();
    }

    public static void HideSettings()        // ← СТАТИЧЕСКИЙ метод!
    {
        if (currentPanel != null)
        {
            Destroy(currentPanel);
            currentPanel = null;
        }
    }
}