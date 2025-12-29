using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
#if UNITY_EDITOR
using UnityEditor;
#endif
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

public class GameManager : MonoBehaviour
{
    private static GameManager _instance;
    public static GameManager Instance => _instance;

    private HashSet<Building> buildings = new HashSet<Building>();
    public bool IsGameOver { get; private set; } = false;
    private bool hadAnyBuilding = false;
    [SerializeField] private float buildingScanInterval = 0.5f;
    private float nextBuildingScanTime = 0f;
    private int mainTowersRegisteredCount = 0;

    [Header("Scene names (Build Settings)")]
    [SerializeField] private string mainMenuSceneName = "main menu";
    [SerializeField] private string gameSceneName = "GameScene";

    private GameObject gameOverUI;
    private Text hudText;
    private Canvas uiCanvas;
    private int gameOverButtonIndex = 0;
    private Transform gameOverButtonsRoot;
    private Transform weaponPanelRoot;
    private readonly List<Button> weaponButtons = new List<Button>();
    private readonly List<Color> weaponButtonBaseColors = new List<Color>();
    private readonly List<GameObject> weaponPrefabs = new List<GameObject>();
    private int selectedWeaponIndex = -1;
    private GameObject runtimeBulletPrefab;
    private Sprite cachedCannonSprite;
    private bool triedLoadCannonSprite = false;
    private Sprite cachedBulletSprite;
    private bool triedLoadBulletSprite = false;
    private Sprite cachedUiBarSprite;
    [Header("Mana")]
    [SerializeField] private int startMana = 1500;
    [SerializeField] private int maxMana = 5000;
    private readonly int[] playerMana = new int[4];
    private readonly float[] manaFlashTimers = new float[4];
    private int activePlayerIndex = 0;

    private readonly Building[] mainTowers = new Building[4];
    private readonly int[] mainTowerMaxHp = new int[4];
    private readonly PlayerPanelUI[] playerPanels = new PlayerPanelUI[4];
    private bool gameplayUiReady = false;

    private class PlayerPanelUI
    {
        public Image background;
        public Text title;
        public Text manaText;
        public Image manaFill;
        public Text hpText;
        public Image hpFill;
    }

    // -------------------- LIFECYCLE --------------------
    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        if (_instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            DestroyGameOverUI();
            CleanupRuntimeObjects();
        }
    }

    void Start()
    {
        SetupHUD();
        RebuildBuildings();
        UpdateHUD();
        SetupGameplayUI();
        ResetMana();
        UpdateHudVisibility();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Time.timeScale = 1f;
        IsGameOver = false;

        DestroyGameOverUI();
        SetupHUD();

        buildings.Clear();
        hadAnyBuilding = false;
        RebuildBuildings();
        UpdateHUD();
        nextBuildingScanTime = 0f;
        for (int i = 0; i < mainTowers.Length; i++)
        {
            mainTowers[i] = null;
            mainTowerMaxHp[i] = 0;
        }
        mainTowersRegisteredCount = 0;
        SetupGameplayUI();
        ResetMana();
        UpdateHudVisibility();
    }

    private void Update()
    {
        if (IsGameOver) return;

        EnsureGameplayUi();
        UpdateActivePlayerInput();

        if (buildingScanInterval <= 0f) return;

        if (Time.time >= nextBuildingScanTime)
        {
            nextBuildingScanTime = Time.time + buildingScanInterval;
            RebuildBuildings();
            UpdateHUD();
            UpdateMainTowersUI();
            CheckGameOver();
        }

        UpdateManaFlash();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstance()
    {
        if (_instance != null) return;
        var existing = FindObjectOfType<GameManager>();
        if (existing != null)
        {
            _instance = existing;
            return;
        }

        var go = new GameObject("GameManager");
        go.AddComponent<GameManager>();
        Debug.Log("GameManager: auto-created singleton instance.");
    }

    // -------------------- BUILDINGS --------------------
    private void RebuildBuildings()
    {
        buildings.Clear();
        Cell[] cells = FindObjectsOfType<Cell>();
        foreach (var cell in cells)
        {
            if (cell == null) continue;
            Building b = cell.buildingOnCell;
            if (b == null) continue;

            // self-heal if the link exists but parent is incorrect
            if (b.transform.parent != cell.transform)
                b.transform.SetParent(cell.transform, true);

            if (IsTrackableBuilding(b))
                buildings.Add(b);
        }

        if (buildings.Count > 0)
            hadAnyBuilding = true;

        Debug.Log($"GameManager: found {buildings.Count} buildings");
    }

    public void RegisterBuilding(Building b)
    {
        if (b == null || IsGameOver) return;

        if (!IsTrackableBuilding(b))
            return;

        buildings.Add(b);
        hadAnyBuilding = true;
        UpdateHUD();
    }

    public void UnregisterBuildingById(int instanceId, string name = "")
    {
        Building target = null;

        foreach (var b in buildings)
        {
            if (b == null) continue;
            if (b.GetInstanceID() == instanceId)
            {
                target = b;
                break;
            }
        }

        if (target != null)
        {
            buildings.Remove(target);
            ClearMainTowerIfDestroyed(target);
            UpdateMainTowersUI();
        }

        // очистка null
        buildings.RemoveWhere(b => b == null);
        buildings.RemoveWhere(b => !IsTrackableBuilding(b));

        UpdateHUD();

        CheckGameOver();
    }

    public void NotifyBuildingDestroyed(Building b)
    {
        if (b == null) return;
        if (buildings.Contains(b))
            buildings.Remove(b);

        Cell cell = b.GetComponentInParent<Cell>();
        if (cell != null && cell.buildingOnCell == b)
            cell.buildingOnCell = null;

        buildings.RemoveWhere(x => x == null);
        UpdateHUD();
        Debug.Log($"GameManager: building destroyed, remaining={buildings.Count}");
        ClearMainTowerIfDestroyed(b);
        UpdateMainTowersUI();
        CheckGameOver();
    }

    public void RegisterMainTower(int index, Building tower)
    {
        if (tower == null) return;
        if (IsMainTower(tower)) return;

        int slot = GetPanelSlotForTower(tower);
        if (slot < 0 || slot >= mainTowers.Length)
            slot = FindFirstEmptyMainTowerSlot();
        if (slot < 0 || slot >= mainTowers.Length)
            slot = Mathf.Clamp(index, 0, mainTowers.Length - 1);

        if (mainTowers[slot] == null)
            mainTowersRegisteredCount++;

        mainTowers[slot] = tower;
        mainTowerMaxHp[slot] = Mathf.Max(1, tower.hp);
        tower.DisableHealthBar();
        UpdateMainTowersUI();
    }

    private void ClearMainTowerIfDestroyed(Building b)
    {
        if (b == null) return;
        for (int i = 0; i < mainTowers.Length; i++)
        {
            if (mainTowers[i] == b)
            {
                mainTowers[i] = null;
                break;
            }
        }
    }

    // -------------------- GAME OVER --------------------
    private void TriggerGameOver()
    {
        if (IsGameOver) return;

        IsGameOver = true;
        Debug.Log("GAME OVER");

        // Останавливаем врагов
        EnemyGenerator gen = FindObjectOfType<EnemyGenerator>();
        if (gen != null)
            gen.StopAllCoroutines();

        Time.timeScale = 0f;

        EnsureEventSystem();
        CreateGameOverUI();
    }

    // -------------------- UI --------------------
    private void SetupHUD()
    {
        uiCanvas = FindObjectOfType<Canvas>();

        if (uiCanvas == null)
        {
            GameObject canvasGO = new GameObject("UI");
            uiCanvas = canvasGO.AddComponent<Canvas>();
            uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler cs = canvasGO.AddComponent<CanvasScaler>();
            cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            cs.referenceResolution = new Vector2(1920, 1080);

            canvasGO.AddComponent<GraphicRaycaster>();
            DontDestroyOnLoad(canvasGO);
        }

        GameObject hudGO = GameObject.Find("HUD_Towers");
        if (hudGO == null)
        {
            hudGO = new GameObject("HUD_Towers");
            hudGO.transform.SetParent(uiCanvas.transform, false);

            hudText = hudGO.AddComponent<Text>();
            hudText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            hudText.fontSize = 28;
            hudText.color = Color.white;
            hudText.alignment = TextAnchor.UpperRight;

            RectTransform rt = hudGO.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(1, 1);
            rt.anchoredPosition = new Vector2(-20, -20);
            rt.sizeDelta = new Vector2(300, 50);
        }
        else
        {
            hudText = hudGO.GetComponent<Text>();
        }

        UpdateHudVisibility();
    }

    private void SetupGameplayUI()
    {
        if (uiCanvas == null) return;
        if (FindObjectOfType<GridGenerator>() == null)
        {
            CleanupGameplayUI();
            gameplayUiReady = false;
            return;
        }

        EnsureEventSystem();
        EnsureBuildingSelector();
        EnsureWeaponPrototypes();
        SetupWeaponPanel();
        SetupPlayerPanels();
        UpdateMainTowersUI();
        gameplayUiReady = true;
    }

    private void EnsureGameplayUi()
    {
        if (gameplayUiReady) return;
        if (uiCanvas == null) return;
        if (FindObjectOfType<GridGenerator>() == null) return;

        SetupGameplayUI();
        UpdateManaUI();
        UpdateMainTowersUI();
    }

    private void CleanupGameplayUI()
    {
        Transform wp = uiCanvas.transform.Find("WeaponPanel");
        if (wp != null) Destroy(wp.gameObject);
        Transform mt = uiCanvas.transform.Find("MainTowersPanel");
        if (mt != null) Destroy(mt.gameObject);
        Transform mp = uiCanvas.transform.Find("ManaPanel");
        if (mp != null) Destroy(mp.gameObject);
        Transform pp = uiCanvas.transform.Find("PlayerPanels");
        if (pp != null) Destroy(pp.gameObject);
    }

    private void UpdateHUD()
    {
        if (hudText != null)
            hudText.text = $"Осталось зданий: {buildings.Count}";
    }

    private void UpdateHudVisibility()
    {
        if (hudText == null) return;
        bool inGameScene = SceneManager.GetActiveScene().name == gameSceneName;
        hudText.gameObject.SetActive(inGameScene);
    }

    private void SetupWeaponPanel()
    {
        Transform existing = uiCanvas.transform.Find("WeaponPanel");
        if (existing != null)
            Destroy(existing.gameObject);

        GameObject panel = CreateUIObject("WeaponPanel", uiCanvas.transform);
        weaponPanelRoot = panel.transform;

        Image bg = panel.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.1f, 0.12f, 0.9f);

        RectTransform prt = panel.GetComponent<RectTransform>();
        prt.anchorMin = new Vector2(0f, 0.5f);
        prt.anchorMax = new Vector2(0f, 0.5f);
        prt.pivot = new Vector2(0f, 0.5f);
        prt.anchoredPosition = new Vector2(20f, 0f);
        prt.sizeDelta = new Vector2(360f, 420f);

        GameObject titleGO = CreateUIObject("Title", panel.transform);
        Text titleText = titleGO.AddComponent<Text>();
        titleText.text = "Орудия";
        titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleText.fontSize = 30;
        titleText.color = Color.white;
        titleText.alignment = TextAnchor.UpperLeft;
        titleText.raycastTarget = false;

        RectTransform titleRT = titleGO.GetComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0f, 1f);
        titleRT.anchorMax = new Vector2(1f, 1f);
        titleRT.pivot = new Vector2(0f, 1f);
        titleRT.anchoredPosition = new Vector2(16f, -12f);
        titleRT.sizeDelta = new Vector2(-32f, 40f);

        GameObject buttonsRoot = CreateUIObject("Buttons", panel.transform);
        RectTransform buttonsRT = buttonsRoot.GetComponent<RectTransform>();
        buttonsRT.anchorMin = new Vector2(0f, 0f);
        buttonsRT.anchorMax = new Vector2(1f, 1f);
        buttonsRT.pivot = new Vector2(0.5f, 0.5f);
        buttonsRT.offsetMin = new Vector2(20f, 20f);
        buttonsRT.offsetMax = new Vector2(-20f, -80f);

        var vlg = buttonsRoot.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.spacing = 12f;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        weaponButtons.Clear();
        weaponButtonBaseColors.Clear();

        string[] labels = { "Пушка", "Дробовик", "Пулемет" };
        Color[] colors =
        {
            new Color(0.18f, 0.6f, 0.85f, 1f),
            new Color(0.9f, 0.55f, 0.2f, 1f),
            new Color(0.2f, 0.75f, 0.4f, 1f)
        };

        for (int i = 0; i < labels.Length; i++)
        {
            int price = 0;
            if (i < weaponPrefabs.Count)
            {
                Building b = weaponPrefabs[i].GetComponent<Building>();
                if (b != null) price = b.price;
            }

            string label = price > 0 ? $"{labels[i]}\nЦена: {price}" : labels[i];
            Button btn = CreateWeaponButton(buttonsRoot.transform, label, colors[i]);
            int index = i;
            btn.onClick.AddListener(() => SelectWeapon(index));
            weaponButtons.Add(btn);
            weaponButtonBaseColors.Add(colors[i]);
        }

        if (weaponPrefabs.Count > 0)
            SelectWeapon(0);
    }

    private Button CreateWeaponButton(Transform parent, string label, Color baseColor)
    {
        GameObject btnGO = CreateUIObject(label, parent);
        Image img = btnGO.AddComponent<Image>();
        img.color = baseColor;

        Button btn = btnGO.AddComponent<Button>();
        ColorBlock colors = btn.colors;
        colors.normalColor = baseColor;
        colors.highlightedColor = Color.Lerp(baseColor, Color.white, 0.2f);
        colors.pressedColor = Color.Lerp(baseColor, Color.black, 0.2f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.3f, 0.3f, 0.3f, 1f);
        btn.colors = colors;

        var layout = btnGO.AddComponent<LayoutElement>();
        layout.preferredHeight = 70f;

        GameObject txtGO = CreateUIObject("Text", btnGO.transform);
        Text txt = txtGO.AddComponent<Text>();
        txt.text = label;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = 26;
        txt.color = Color.white;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.raycastTarget = false;

        RectTransform trt = txtGO.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;

        var shadow = txtGO.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.4f);
        shadow.effectDistance = new Vector2(1.5f, -1.5f);

        return btn;
    }

    private void SelectWeapon(int index)
    {
        if (index < 0 || index >= weaponPrefabs.Count) return;
        if (BuildingSelector.Instance == null) return;

        selectedWeaponIndex = index;
        BuildingSelector.Instance.SelectBuilding(weaponPrefabs[index]);

        for (int i = 0; i < weaponButtons.Count; i++)
        {
            Button btn = weaponButtons[i];
            Color baseColor = weaponButtonBaseColors[i];
            bool selected = (i == selectedWeaponIndex);
            Color color = selected ? Color.Lerp(baseColor, Color.white, 0.25f) : baseColor;
            btn.image.color = color;
            ColorBlock colors = btn.colors;
            colors.normalColor = color;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.2f);
            colors.selectedColor = colors.highlightedColor;
            btn.colors = colors;
        }
    }

    private void SetupPlayerPanels()
    {
        Transform existing = uiCanvas.transform.Find("PlayerPanels");
        if (existing != null)
            Destroy(existing.gameObject);

        Transform old = uiCanvas.transform.Find("MainTowersPanel");
        if (old != null) Destroy(old.gameObject);
        Transform oldMana = uiCanvas.transform.Find("ManaPanel");
        if (oldMana != null) Destroy(oldMana.gameObject);

        GameObject root = CreateUIObject("PlayerPanels", uiCanvas.transform);

        RectTransform rootRT = root.GetComponent<RectTransform>();
        rootRT.anchorMin = new Vector2(0.5f, 0.5f);
        rootRT.anchorMax = new Vector2(0.5f, 0.5f);
        rootRT.pivot = new Vector2(0.5f, 0.5f);
        rootRT.anchoredPosition = Vector2.zero;
        rootRT.sizeDelta = new Vector2(640f, 640f);

        playerPanels[0] = CreatePlayerPanel(root.transform, "P1", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(-380f, 150f));
        playerPanels[1] = CreatePlayerPanel(root.transform, "P2", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(-380f, -150f));
        playerPanels[2] = CreatePlayerPanel(root.transform, "P3", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(380f, 150f));
        playerPanels[3] = CreatePlayerPanel(root.transform, "P4", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(380f, -150f));

        UpdatePlayerPanelHighlight();
        UpdateManaUI();
    }

    private PlayerPanelUI CreatePlayerPanel(Transform parent, string title, Vector2 anchor, Vector2 pivot, Vector2 anchoredPos)
    {
        GameObject panel = CreateUIObject($"PlayerPanel_{title}", parent);
        Image bg = panel.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.1f, 0.12f, 0.9f);

        RectTransform prt = panel.GetComponent<RectTransform>();
        prt.anchorMin = anchor;
        prt.anchorMax = anchor;
        prt.pivot = pivot;
        prt.anchoredPosition = anchoredPos;
        prt.sizeDelta = new Vector2(260f, 150f);

        GameObject titleGO = CreateUIObject("Title", panel.transform);
        Text titleText = titleGO.AddComponent<Text>();
        titleText.text = title;
        titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleText.fontSize = 22;
        titleText.color = Color.white;
        titleText.alignment = TextAnchor.UpperLeft;
        titleText.raycastTarget = false;

        RectTransform titleRT = titleGO.GetComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0f, 1f);
        titleRT.anchorMax = new Vector2(1f, 1f);
        titleRT.pivot = new Vector2(0f, 1f);
        titleRT.anchoredPosition = new Vector2(12f, -8f);
        titleRT.sizeDelta = new Vector2(-24f, 24f);

        GameObject manaTextGO = CreateUIObject("ManaText", panel.transform);
        Text manaText = manaTextGO.AddComponent<Text>();
        manaText.text = "mana 0";
        manaText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        manaText.fontSize = 18;
        manaText.color = Color.white;
        manaText.alignment = TextAnchor.UpperLeft;
        manaText.raycastTarget = false;

        RectTransform manaTextRT = manaTextGO.GetComponent<RectTransform>();
        manaTextRT.anchorMin = new Vector2(0f, 1f);
        manaTextRT.anchorMax = new Vector2(1f, 1f);
        manaTextRT.pivot = new Vector2(0f, 1f);
        manaTextRT.anchoredPosition = new Vector2(12f, -36f);
        manaTextRT.sizeDelta = new Vector2(-24f, 20f);

        GameObject manaBarBG = CreateUIObject("ManaBarBG", panel.transform);
        Image manaBgImg = manaBarBG.AddComponent<Image>();
        manaBgImg.sprite = GetUiBarSprite();
        manaBgImg.color = new Color(0.2f, 0.2f, 0.2f, 1f);

        RectTransform manaBarRT = manaBarBG.GetComponent<RectTransform>();
        manaBarRT.anchorMin = new Vector2(0f, 1f);
        manaBarRT.anchorMax = new Vector2(1f, 1f);
        manaBarRT.pivot = new Vector2(0.5f, 1f);
        manaBarRT.anchoredPosition = new Vector2(0f, -58f);
        manaBarRT.sizeDelta = new Vector2(-24f, 12f);
        manaBarRT.offsetMin = new Vector2(12f, manaBarRT.offsetMin.y);
        manaBarRT.offsetMax = new Vector2(-12f, manaBarRT.offsetMax.y);

        GameObject manaFillGO = CreateUIObject("ManaFill", manaBarBG.transform);
        Image manaFill = manaFillGO.AddComponent<Image>();
        manaFill.sprite = GetUiBarSprite();
        manaFill.type = Image.Type.Filled;
        manaFill.fillMethod = Image.FillMethod.Horizontal;
        manaFill.fillOrigin = 0;
        manaFill.fillAmount = 1f;
        manaFill.color = new Color(0.25f, 0.8f, 1f, 1f);

        RectTransform manaFillRT = manaFillGO.GetComponent<RectTransform>();
        manaFillRT.anchorMin = Vector2.zero;
        manaFillRT.anchorMax = Vector2.one;
        manaFillRT.offsetMin = manaFillRT.offsetMax = Vector2.zero;

        GameObject hpTextGO = CreateUIObject("HpText", panel.transform);
        Text hpText = hpTextGO.AddComponent<Text>();
        hpText.text = "hp 0";
        hpText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        hpText.fontSize = 18;
        hpText.color = Color.white;
        hpText.alignment = TextAnchor.UpperLeft;
        hpText.raycastTarget = false;

        RectTransform hpTextRT = hpTextGO.GetComponent<RectTransform>();
        hpTextRT.anchorMin = new Vector2(0f, 1f);
        hpTextRT.anchorMax = new Vector2(1f, 1f);
        hpTextRT.pivot = new Vector2(0f, 1f);
        hpTextRT.anchoredPosition = new Vector2(12f, -82f);
        hpTextRT.sizeDelta = new Vector2(-24f, 20f);

        GameObject hpBarBG = CreateUIObject("HpBarBG", panel.transform);
        Image hpBgImg = hpBarBG.AddComponent<Image>();
        hpBgImg.sprite = GetUiBarSprite();
        hpBgImg.color = new Color(0.2f, 0.2f, 0.2f, 1f);

        RectTransform hpBarRT = hpBarBG.GetComponent<RectTransform>();
        hpBarRT.anchorMin = new Vector2(0f, 1f);
        hpBarRT.anchorMax = new Vector2(1f, 1f);
        hpBarRT.pivot = new Vector2(0.5f, 1f);
        hpBarRT.anchoredPosition = new Vector2(0f, -104f);
        hpBarRT.sizeDelta = new Vector2(-24f, 12f);
        hpBarRT.offsetMin = new Vector2(12f, hpBarRT.offsetMin.y);
        hpBarRT.offsetMax = new Vector2(-12f, hpBarRT.offsetMax.y);

        GameObject hpFillGO = CreateUIObject("HpFill", hpBarBG.transform);
        Image hpFill = hpFillGO.AddComponent<Image>();
        hpFill.sprite = GetUiBarSprite();
        hpFill.type = Image.Type.Filled;
        hpFill.fillMethod = Image.FillMethod.Horizontal;
        hpFill.fillOrigin = 0;
        hpFill.fillAmount = 1f;
        hpFill.color = new Color(0.2f, 0.9f, 0.4f, 1f);

        RectTransform hpFillRT = hpFillGO.GetComponent<RectTransform>();
        hpFillRT.anchorMin = Vector2.zero;
        hpFillRT.anchorMax = Vector2.one;
        hpFillRT.offsetMin = hpFillRT.offsetMax = Vector2.zero;

        return new PlayerPanelUI
        {
            background = bg,
            title = titleText,
            manaText = manaText,
            manaFill = manaFill,
            hpText = hpText,
            hpFill = hpFill
        };
    }

    private void UpdateMainTowersUI()
    {
        for (int i = 0; i < mainTowers.Length; i++)
        {
            PlayerPanelUI panel = playerPanels[i];
            if (panel == null) continue;

            Building tower = mainTowers[i];
            int maxHp = (i < mainTowerMaxHp.Length) ? mainTowerMaxHp[i] : 1;

            if (tower == null)
            {
                if (panel.hpText != null) panel.hpText.text = "hp 0";
                if (panel.hpFill != null) panel.hpFill.fillAmount = 0f;
                continue;
            }

            int currentHp = Mathf.Max(0, tower.hp);
            if (currentHp > maxHp)
            {
                maxHp = currentHp;
                if (i < mainTowerMaxHp.Length)
                    mainTowerMaxHp[i] = maxHp;
            }

            float t = (float)currentHp / Mathf.Max(1, maxHp);
            if (panel.hpFill != null) panel.hpFill.fillAmount = t;
            if (panel.hpText != null) panel.hpText.text = $"hp {currentHp}";
        }
    }

    private void ResetMana()
    {
        for (int i = 0; i < playerMana.Length; i++)
        {
            playerMana[i] = Mathf.Clamp(startMana, 0, maxMana);
            manaFlashTimers[i] = 0f;
        }
        UpdateManaUI();
    }

    public bool TrySpendMana(int amount)
    {
        return TrySpendMana(amount, activePlayerIndex);
    }

    public bool TrySpendMana(int amount, int playerIndex)
    {
        if (amount <= 0) return true;
        if (playerIndex < 0 || playerIndex >= playerMana.Length) return false;
        if (playerMana[playerIndex] < amount)
        {
            manaFlashTimers[playerIndex] = 0.6f;
            UpdateManaUI();
            return false;
        }

        playerMana[playerIndex] -= amount;
        UpdateManaUI();
        return true;
    }

    public void AddMana(int amount)
    {
        AddManaToPlayer(activePlayerIndex, amount);
    }

    public void AddManaToPlayer(int index, int amount)
    {
        if (amount <= 0) return;
        if (index < 0 || index >= playerMana.Length) return;
        playerMana[index] = Mathf.Clamp(playerMana[index] + amount, 0, maxMana);
        UpdateManaUI();
    }

    private void UpdateManaUI()
    {
        for (int i = 0; i < playerPanels.Length; i++)
        {
            PlayerPanelUI panel = playerPanels[i];
            if (panel == null) continue;
            int manaValue = (i >= 0 && i < playerMana.Length) ? playerMana[i] : 0;
            if (panel.manaText != null)
                panel.manaText.text = $"mana {manaValue}";
            if (panel.manaFill != null)
                panel.manaFill.fillAmount = (float)manaValue / Mathf.Max(1, maxMana);
        }
    }

    private void UpdateManaFlash()
    {
        for (int i = 0; i < playerPanels.Length; i++)
        {
            PlayerPanelUI panel = playerPanels[i];
            if (panel == null || panel.manaText == null) continue;

            float timer = (i >= 0 && i < manaFlashTimers.Length) ? manaFlashTimers[i] : 0f;
            if (timer > 0f)
            {
                timer -= Time.deltaTime;
                if (i >= 0 && i < manaFlashTimers.Length)
                    manaFlashTimers[i] = timer;
                panel.manaText.color = Color.Lerp(Color.white, new Color(1f, 0.4f, 0.4f, 1f), 0.6f);
            }
            else
            {
                panel.manaText.color = Color.white;
            }
        }
    }

    public bool IsMainTower(Building b)
    {
        if (b == null) return false;
        for (int i = 0; i < mainTowers.Length; i++)
        {
            if (mainTowers[i] == b) return true;
        }
        return false;
    }

    public bool IsShotBlockedByMainTower(Vector2 from, Vector2 to, Transform ignore)
    {
        RaycastHit2D[] hits = Physics2D.LinecastAll(from, to);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D col = hits[i].collider;
            if (col == null) continue;

            Transform hitTransform = col.transform;
            if (ignore != null && (hitTransform == ignore || hitTransform.IsChildOf(ignore)))
                continue;

            Building b = col.GetComponent<Building>() ?? col.GetComponentInParent<Building>();
            if (b != null && IsMainTower(b))
                return true;
        }
        return false;
    }

    public int ActivePlayerIndex => activePlayerIndex;

    private void UpdateActivePlayerInput()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb == null) return;
        if (kb.digit1Key.wasPressedThisFrame) SetActivePlayer(0);
        if (kb.digit2Key.wasPressedThisFrame) SetActivePlayer(1);
        if (kb.digit3Key.wasPressedThisFrame) SetActivePlayer(2);
        if (kb.digit4Key.wasPressedThisFrame) SetActivePlayer(3);
#else
        if (Input.GetKeyDown(KeyCode.Alpha1)) SetActivePlayer(0);
        if (Input.GetKeyDown(KeyCode.Alpha2)) SetActivePlayer(1);
        if (Input.GetKeyDown(KeyCode.Alpha3)) SetActivePlayer(2);
        if (Input.GetKeyDown(KeyCode.Alpha4)) SetActivePlayer(3);
#endif
    }

    private void SetActivePlayer(int index)
    {
        if (index < 0 || index >= playerPanels.Length) return;
        if (activePlayerIndex == index) return;
        activePlayerIndex = index;
        UpdatePlayerPanelHighlight();
    }

    private void UpdatePlayerPanelHighlight()
    {
        Color baseColor = new Color(0.08f, 0.1f, 0.12f, 0.9f);
        Color activeColor = new Color(0.12f, 0.15f, 0.2f, 0.95f);

        for (int i = 0; i < playerPanels.Length; i++)
        {
            PlayerPanelUI panel = playerPanels[i];
            if (panel == null || panel.background == null) continue;
            panel.background.color = (i == activePlayerIndex) ? activeColor : baseColor;
        }
    }

    private int FindFirstEmptyMainTowerSlot()
    {
        for (int i = 0; i < mainTowers.Length; i++)
        {
            if (mainTowers[i] == null) return i;
        }
        return -1;
    }

    private int GetPanelSlotForTower(Building tower)
    {
        Vector2 center = GetGridCenter();
        Vector2 pos = tower.transform.position;
        bool left = pos.x <= center.x;
        bool top = pos.y >= center.y;

        if (top && left) return 0;      // P1
        if (!top && left) return 1;     // P2
        if (top && !left) return 2;     // P3
        return 3;                       // P4
    }

    private Vector2 GetGridCenter()
    {
        GridGenerator grid = FindObjectOfType<GridGenerator>();
        if (grid == null) return Vector2.zero;

        Transform c00 = grid.transform.Find("Cell_0_0");
        Transform cmax = grid.transform.Find($"Cell_{grid.width - 1}_{grid.height - 1}");
        if (c00 != null && cmax != null)
            return (c00.position + cmax.position) * 0.5f;

        return grid.transform.position;
    }

    private void EnsureBuildingSelector()
    {
        if (BuildingSelector.Instance != null) return;
        GameObject go = new GameObject("BuildingSelector");
        go.AddComponent<BuildingSelector>();
        DontDestroyOnLoad(go);
    }

    private void EnsureWeaponPrototypes()
    {
        if (weaponPrefabs.Count > 0) return;

        if (runtimeBulletPrefab == null)
            runtimeBulletPrefab = CreateRuntimeBulletPrefab();
        else
            RefreshRuntimeBulletSprite();

        Sprite sharedSprite = GetSharedWeaponSprite();
        weaponPrefabs.Add(CreateWeaponPrototype("Cannon", typeof(Cannon), new Color(0.2f, 0.6f, 0.85f, 1f), runtimeBulletPrefab, sharedSprite, 150));
        weaponPrefabs.Add(CreateWeaponPrototype("Shotgun", typeof(Shotgun), new Color(0.9f, 0.55f, 0.2f, 1f), runtimeBulletPrefab, sharedSprite, 250));
        weaponPrefabs.Add(CreateWeaponPrototype("MachineGun", typeof(MachineGun), new Color(0.2f, 0.75f, 0.4f, 1f), runtimeBulletPrefab, sharedSprite, 200));
    }

    private void RefreshRuntimeBulletSprite()
    {
        if (runtimeBulletPrefab == null) return;
        SpriteRenderer sr = runtimeBulletPrefab.GetComponent<SpriteRenderer>();
        if (sr == null) return;
        Sprite bulletSprite = GetSharedBulletSprite();
        if (bulletSprite != null)
            sr.sprite = bulletSprite;
    }

    private void CleanupRuntimeObjects()
    {
        if (runtimeBulletPrefab != null)
        {
            Destroy(runtimeBulletPrefab);
            runtimeBulletPrefab = null;
        }

        for (int i = 0; i < weaponPrefabs.Count; i++)
        {
            if (weaponPrefabs[i] != null)
                Destroy(weaponPrefabs[i]);
        }
        weaponPrefabs.Clear();
        weaponButtons.Clear();
        weaponButtonBaseColors.Clear();
        selectedWeaponIndex = -1;
    }

    private GameObject CreateWeaponPrototype(string name, System.Type scriptType, Color color, GameObject bulletPrefab, Sprite sharedSprite, int price)
    {
        GameObject proto = new GameObject(name + "_Prototype");
        proto.SetActive(false);
        DontDestroyOnLoad(proto);

        proto.transform.localScale = new Vector3(1.3f, 1.3f, 1f);

        SpriteRenderer sr = proto.AddComponent<SpriteRenderer>();
        sr.sprite = sharedSprite != null ? sharedSprite : CreateSolidSprite(color);
        sr.color = Color.white;
        sr.sortingOrder = 1;

        proto.AddComponent<BoxCollider2D>();

        Building building;
        Building.SuppressHealthBarCreation = true;
        try
        {
            building = (Building)proto.AddComponent(scriptType);
        }
        finally
        {
            Building.SuppressHealthBarCreation = false;
        }
        building.price = price;

        Cannon cannon = building as Cannon;
        if (cannon != null)
            cannon.bulletPrefab = bulletPrefab;

        Shotgun shotgun = building as Shotgun;
        if (shotgun != null)
            shotgun.bulletPrefab = bulletPrefab;

        return proto;
    }

    private GameObject CreateRuntimeBulletPrefab()
    {
        GameObject bullet = new GameObject("RuntimeBullet");
        bullet.SetActive(false);
        DontDestroyOnLoad(bullet);
        bullet.transform.localScale = new Vector3(0.06f, 0.06f, 1f);

        SpriteRenderer sr = bullet.AddComponent<SpriteRenderer>();
        Sprite bulletSprite = GetSharedBulletSprite();
        sr.sprite = bulletSprite != null ? bulletSprite : CreateSolidSprite(Color.yellow);
        sr.sortingOrder = 20;

        Bullet bulletScript = bullet.AddComponent<Bullet>();
        bulletScript.speed = 8f;
        bulletScript.damage = 10;

        return bullet;
    }

    private Sprite CreateSolidSprite(Color color)
    {
        Texture2D tex = new Texture2D(16, 16, TextureFormat.ARGB32, false);
        tex.filterMode = FilterMode.Point;
        Color[] pixels = new Color[16 * 16];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 16f);
    }

    private Sprite CreateUiSprite(Color color)
    {
        Texture2D tex = new Texture2D(2, 2, TextureFormat.ARGB32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[4];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 100f);
    }

    private Sprite GetUiBarSprite()
    {
        if (cachedUiBarSprite == null)
            cachedUiBarSprite = CreateUiSprite(Color.white);
        return cachedUiBarSprite;
    }

    private Sprite GetSharedWeaponSprite()
    {
        if (triedLoadCannonSprite) return cachedCannonSprite;
        triedLoadCannonSprite = true;

        cachedCannonSprite = LoadSpriteFromAssetOrDisk("Assets/Resources/Sprites/cannon/cannon1.png", "Sprites/cannon/cannon1.png", 100f);
        return cachedCannonSprite;
    }

    private Sprite GetSharedBulletSprite()
    {
        if (triedLoadBulletSprite) return cachedBulletSprite;
        triedLoadBulletSprite = true;

        cachedBulletSprite = LoadSpriteFromAssetOrDisk("Assets/Resources/Sprites/bullet.png", "Sprites/bullet.png", 100f);
        return cachedBulletSprite;
    }

    private Sprite LoadSpriteFromAssetOrDisk(string assetPath, string relativePath, float pixelsPerUnit)
    {
        Sprite resourceSprite = LoadSpriteFromResources(relativePath);
        if (resourceSprite != null)
            return resourceSprite;
#if UNITY_EDITOR
        Sprite assetSprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (assetSprite != null)
            return assetSprite;
#endif
        return LoadSpriteFromDisk(relativePath, pixelsPerUnit);
    }

    private Sprite LoadSpriteFromResources(string relativePath)
    {
        if (string.IsNullOrEmpty(relativePath))
            return null;

        string resourcePath = Path.ChangeExtension(relativePath, null);
        return Resources.Load<Sprite>(resourcePath);
    }

    private Sprite LoadSpriteFromDisk(string relativePath, float pixelsPerUnit)
    {
        string path = Path.Combine(Application.dataPath, relativePath);
        if (!File.Exists(path))
            return null;

        byte[] data = File.ReadAllBytes(path);
        if (data == null || data.Length == 0)
            return null;

        Texture2D tex = new Texture2D(2, 2, TextureFormat.ARGB32, false);
        if (!tex.LoadImage(data))
            return null;

        tex.filterMode = FilterMode.Point;
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), pixelsPerUnit);
    }

    private bool IsTrackableBuilding(Building b)
    {
        if (b == null) return false;
        Cell cell = b.GetComponentInParent<Cell>();
        if (cell == null) return false;

        // Self-heal missing link for pre-placed buildings.
        if (cell.buildingOnCell == null)
            cell.buildingOnCell = b;

        return cell.buildingOnCell == b;
    }

    private void CheckGameOver()
    {
        if (IsGameOver) return;
        if (!hadAnyBuilding) return;

        buildings.RemoveWhere(b => b == null);
        buildings.RemoveWhere(b => !IsTrackableBuilding(b));
        if (buildings.Count == 0)
            TriggerGameOver();
    }

    private void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;

        GameObject es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
        es.AddComponent<InputSystemUIInputModule>();
#else
        es.AddComponent<StandaloneInputModule>();
#endif
        DontDestroyOnLoad(es);
    }

    private void DestroyGameOverUI()
    {
        if (gameOverUI != null)
        {
            Destroy(gameOverUI);
            gameOverUI = null;
        }
        gameOverButtonsRoot = null;
        gameOverButtonIndex = 0;
    }

    private void CreateGameOverUI()
    {
        if (gameOverUI != null)
        {
            if (gameOverUI.transform.Find("Panel/Buttons") != null)
                return;

            Destroy(gameOverUI);
            gameOverUI = null;
        }

        gameOverUI = new GameObject("GameOverUI");
        Canvas canvas = gameOverUI.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999;

        CanvasScaler cs = gameOverUI.AddComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920, 1080);

        gameOverUI.AddComponent<GraphicRaycaster>();

        // Panel
        GameObject panel = CreateUIObject("Panel", gameOverUI.transform);
        Image img = panel.AddComponent<Image>();
        img.color = new Color(0.05f, 0.05f, 0.08f, 0.85f);
        img.raycastTarget = true;

        RectTransform prt = panel.GetComponent<RectTransform>();
        prt.anchorMin = Vector2.zero;
        prt.anchorMax = Vector2.one;
        prt.offsetMin = prt.offsetMax = Vector2.zero;

        // Title
        GameObject titleGO = CreateUIObject("Title", panel.transform);
        Text titleText = titleGO.AddComponent<Text>();
        titleText.text = "Игра завершена";
        titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleText.fontSize = 48;
        titleText.color = Color.white;
        titleText.alignment = TextAnchor.UpperCenter;
        titleText.raycastTarget = false;
        var titleShadow = titleGO.AddComponent<Shadow>();
        titleShadow.effectColor = new Color(0f, 0f, 0f, 0.5f);
        titleShadow.effectDistance = new Vector2(2f, -2f);

        RectTransform titleRT = titleGO.GetComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0.5f, 1f);
        titleRT.anchorMax = new Vector2(0.5f, 1f);
        titleRT.pivot = new Vector2(0.5f, 1f);
        titleRT.anchoredPosition = new Vector2(0f, -80f);
        titleRT.sizeDelta = new Vector2(600f, 80f);

        GameObject buttonsRoot = CreateUIObject("Buttons", panel.transform);
        gameOverButtonsRoot = buttonsRoot.transform;

        RectTransform buttonsRT = buttonsRoot.GetComponent<RectTransform>();
        buttonsRT.anchorMin = new Vector2(0.5f, 0.5f);
        buttonsRT.anchorMax = new Vector2(0.5f, 0.5f);
        buttonsRT.pivot = new Vector2(0.5f, 0.5f);
        buttonsRT.anchoredPosition = new Vector2(0f, -180f);
        buttonsRT.sizeDelta = new Vector2(1200f, 140f);

        var hlg = buttonsRoot.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.spacing = 30f;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        gameOverButtonIndex = 0;
        CreateButton(gameOverButtonsRoot, "Restart", "Начать заново", RestartGame);
        CreateButton(gameOverButtonsRoot, "Menu", "В меню", ReturnToMainMenu);
        CreateButton(gameOverButtonsRoot, "Quit", "Закрыть игру", QuitGame);
    }

    private void CreateButton(Transform parent, string name, string label, UnityEngine.Events.UnityAction action)
    {
        GameObject btnGO = CreateUIObject(name, parent);

        Image img = btnGO.AddComponent<Image>();
        img.color = new Color(0.18f, 0.6f, 0.85f, 1f);

        Button btn = btnGO.AddComponent<Button>();
        btn.onClick.AddListener(action);
        ColorBlock colors = btn.colors;
        colors.normalColor = new Color(0.18f, 0.6f, 0.85f, 1f);
        colors.highlightedColor = new Color(0.22f, 0.7f, 0.95f, 1f);
        colors.pressedColor = new Color(0.12f, 0.45f, 0.7f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.4f, 0.4f, 0.4f, 1f);
        btn.colors = colors;

        RectTransform rt = btnGO.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(330, 110);
        gameOverButtonIndex++;

        GameObject txtGO = CreateUIObject("Text", btnGO.transform);

        Text txt = txtGO.AddComponent<Text>();
        txt.text = label;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = 30;
        txt.color = Color.white;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.raycastTarget = false;
        var txtShadow = txtGO.AddComponent<Shadow>();
        txtShadow.effectColor = new Color(0f, 0f, 0f, 0.4f);
        txtShadow.effectDistance = new Vector2(1.5f, -1.5f);

        RectTransform trt = txtGO.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;
    }

    private GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    // -------------------- BUTTONS --------------------
    private void RestartGame()
    {
        Time.timeScale = 1f;
        DestroyGameOverUI();
        var active = SceneManager.GetActiveScene();
        if (active.IsValid())
        {
            SceneManager.LoadScene(active.buildIndex);
        }
        else if (!string.IsNullOrEmpty(gameSceneName))
        {
            SceneManager.LoadScene(gameSceneName);
        }
    }

    private void ReturnToMainMenu()
    {
        Time.timeScale = 1f;
        DestroyGameOverUI();
        SceneManager.LoadScene(mainMenuSceneName);
    }

    private void QuitGame()
    {
        Time.timeScale = 1f;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
