using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class AutoQuit : MonoBehaviour
{
    void Awake()
    {
        GetComponent<Button>().onClick.AddListener(() =>
        {
            Debug.Log("Выхожу из игры!");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        });
    }
}