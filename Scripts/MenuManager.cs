using UnityEngine;

public class MenuManager : MonoBehaviour
{
    // СТАТИЧЕСКИЙ метод — Unity видит его даже при глюках
    public static void ExitGame()
    {
        Debug.Log("ВЫХОД — РАБОТАЕТ!");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}