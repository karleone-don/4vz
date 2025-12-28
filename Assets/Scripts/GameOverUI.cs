using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class GameOverUI : MonoBehaviour
{
    [Header("Scene names (must match Build Settings exactly)")]
    [SerializeField] private string mainMenuSceneName = "main menu";
    [SerializeField] private string gameSceneName = "GameScene";

    [Header("Optional: fade/visuals")]
    [SerializeField] private bool showCursorOnGameOver = true;

    private Canvas _canvas;
    private GraphicRaycaster _raycaster;
    private Image _panelImage;

    private void Awake()
    {
        // Ensure this UI is always above everything and blocks clicks
        _canvas = GetComponent<Canvas>();
        if (_canvas == null) _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 10000;

        _raycaster = GetComponent<GraphicRaycaster>();
        if (_raycaster == null) _raycaster = gameObject.AddComponent<GraphicRaycaster>();

        // Try to find "Panel" image and make sure it blocks raycasts
        var panel = transform.Find("Panel");
        if (panel != null)
        {
            _panelImage = panel.GetComponent<Image>();
            if (_panelImage != null)
                _panelImage.raycastTarget = true;
        }
    }

    /// <summary>Show GameOver UI and make sure input can't pass through.</summary>
    public void Show()
    {
        gameObject.SetActive(true);

        // Pause game if not already paused
        if (Time.timeScale != 0f)
            Time.timeScale = 0f;

        // Mark game over in GameManager (if present)
        if (GameManager.Instance != null)
            SetGameOverFlag(true);

        if (showCursorOnGameOver)
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        Canvas.ForceUpdateCanvases();
    }

    /// <summary>Hide UI and resume game (usually before loading scenes).</summary>
    public void Hide()
    {
        gameObject.SetActive(false);

        if (showCursorOnGameOver)
        {
            // don't force lock here if you don't use locked cursor
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
    }

    // ---- Button handlers (can be assigned from code or inspector if needed) ----

    public void OnRestart()
    {
        Time.timeScale = 1f;
        SetGameOverFlag(false);

        // If you want: clean up runtime UI object before reload
        Destroy(gameObject);

        var active = SceneManager.GetActiveScene();
        SceneManager.LoadScene(active.name);
    }

    public void OnMainMenu()
    {
        Time.timeScale = 1f;
        SetGameOverFlag(false);

        Destroy(gameObject);
        SceneManager.LoadScene(mainMenuSceneName);
    }

    public void OnQuit()
    {
        Time.timeScale = 1f;
        SetGameOverFlag(false);

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ---- Helpers ----
    private void SetGameOverFlag(bool value)
    {
        // GameManager.IsGameOver has private set in your file, so we can't set it directly.
        // But we can safely call a public method if you add one.
        // For now, we just rely on GameManager already setting IsGameOver=true in TriggerGameOver().
        // If you want full control, add in GameManager:
        // public void SetGameOver(bool v) { IsGameOver = v; }
        // And call it here.

        var gm = GameManager.Instance;
        if (gm == null) return;

        // reflection fallback (no need to edit GameManager), optional:
        var prop = gm.GetType().GetProperty("IsGameOver");
        // can't set because private set → skip.
    }
}
