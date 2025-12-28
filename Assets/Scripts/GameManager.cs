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
    [SerializeField] private int startMana = 500;
    [SerializeField] private int maxMana = 50000;
    private int currentMana = 0;
    private Text manaText;
    private Image manaFill;
    private float manaFlashTimer = 0f;

    private readonly Building[] mainTowers = new Building[4];
    private readonly int[] mainTowerMaxHp = new int[4];
    private readonly List<TowerStatusRow> towerRows = new List<TowerStatusRow>();

    private class TowerStatusRow
    {
        public Text label;
        public Image fill;
        public Text value;
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
    }

    private void Update()
    {
        if (IsGameOver) return;
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
        if (index < 0 || index >= mainTowers.Length) return;
        if (tower == null) return;

        if (mainTowers[index] == null)
            mainTowersRegisteredCount++;

        mainTowers[index] = tower;
        mainTowerMaxHp[index] = Mathf.Max(1, tower.hp);
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
    }

    private void SetupGameplayUI()
    {
        if (uiCanvas == null) return;
        if (FindObjectOfType<GridGenerator>() == null)
        {
            Transform wp = uiCanvas.transform.Find("WeaponPanel");
            if (wp != null) Destroy(wp.gameObject);
            Transform mt = uiCanvas.transform.Find("MainTowersPanel");
            if (mt != null) Destroy(mt.gameObject);
            Transform mp = uiCanvas.transform.Find("ManaPanel");
            if (mp != null) Destroy(mp.gameObject);
            return;
        }

        EnsureEventSystem();
        EnsureBuildingSelector();
        EnsureWeaponPrototypes();
        SetupWeaponPanel();
        SetupMainTowersPanel();
        SetupManaPanel();
        UpdateMainTowersUI();
    }

    private void UpdateHUD()
    {
        if (hudText != null)
            hudText.text = $"Вышки: {buildings.Count}";
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

    private void SetupManaPanel()
    {
        Transform existing = uiCanvas.transform.Find("ManaPanel");
        if (existing != null)
            Destroy(existing.gameObject);

        GameObject panel = CreateUIObject("ManaPanel", uiCanvas.transform);
        Image bg = panel.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.1f, 0.12f, 0.9f);

        RectTransform prt = panel.GetComponent<RectTransform>();
        prt.anchorMin = new Vector2(0.5f, 0f);
        prt.anchorMax = new Vector2(0.5f, 0f);
        prt.pivot = new Vector2(0.5f, 0f);
        prt.anchoredPosition = new Vector2(0f, 20f);
        prt.sizeDelta = new Vector2(420f, 90f);

        GameObject textGO = CreateUIObject("ManaText", panel.transform);
        manaText = textGO.AddComponent<Text>();
        manaText.text = "Мана: 0";
        manaText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        manaText.fontSize = 26;
        manaText.color = Color.white;
        manaText.alignment = TextAnchor.UpperCenter;
        manaText.raycastTarget = false;

        RectTransform textRT = textGO.GetComponent<RectTransform>();
        textRT.anchorMin = new Vector2(0f, 1f);
        textRT.anchorMax = new Vector2(1f, 1f);
        textRT.pivot = new Vector2(0.5f, 1f);
        textRT.anchoredPosition = new Vector2(0f, -10f);
        textRT.sizeDelta = new Vector2(0f, 30f);

        GameObject barBG = CreateUIObject("ManaBarBG", panel.transform);
        Image bgImg = barBG.AddComponent<Image>();
        bgImg.sprite = GetUiBarSprite();
        bgImg.color = new Color(0.2f, 0.2f, 0.2f, 1f);

        RectTransform barRT = barBG.GetComponent<RectTransform>();
        barRT.anchorMin = new Vector2(0.1f, 0f);
        barRT.anchorMax = new Vector2(0.9f, 0f);
        barRT.pivot = new Vector2(0.5f, 0f);
        barRT.anchoredPosition = new Vector2(0f, 14f);
        barRT.sizeDelta = new Vector2(0f, 14f);

        GameObject barFill = CreateUIObject("ManaBarFill", barBG.transform);
        manaFill = barFill.AddComponent<Image>();
        manaFill.sprite = GetUiBarSprite();
        manaFill.type = Image.Type.Filled;
        manaFill.fillMethod = Image.FillMethod.Horizontal;
        manaFill.fillOrigin = 0;
        manaFill.fillAmount = 1f;
        manaFill.color = new Color(0.25f, 0.8f, 1f, 1f);

        RectTransform fillRT = barFill.GetComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = Vector2.one;
        fillRT.offsetMin = fillRT.offsetMax = Vector2.zero;
    }

    private void SetupMainTowersPanel()
    {
        Transform existing = uiCanvas.transform.Find("MainTowersPanel");
        if (existing != null)
            Destroy(existing.gameObject);

        GameObject panel = CreateUIObject("MainTowersPanel", uiCanvas.transform);
        Image bg = panel.AddComponent<Image>();
        bg.color = new Color(0.07f, 0.08f, 0.1f, 0.9f);

        RectTransform prt = panel.GetComponent<RectTransform>();
        prt.anchorMin = new Vector2(1f, 0.5f);
        prt.anchorMax = new Vector2(1f, 0.5f);
        prt.pivot = new Vector2(1f, 0.5f);
        prt.anchoredPosition = new Vector2(-40f, 0f);
        prt.sizeDelta = new Vector2(520f, 360f);

        GameObject titleGO = CreateUIObject("Title", panel.transform);
        Text titleText = titleGO.AddComponent<Text>();
        titleText.text = "Главные вышки";
        titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleText.fontSize = 26;
        titleText.color = Color.white;
        titleText.alignment = TextAnchor.UpperLeft;
        titleText.raycastTarget = false;

        RectTransform titleRT = titleGO.GetComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0f, 1f);
        titleRT.anchorMax = new Vector2(1f, 1f);
        titleRT.pivot = new Vector2(0f, 1f);
        titleRT.anchoredPosition = new Vector2(16f, -12f);
        titleRT.sizeDelta = new Vector2(-32f, 36f);

        GameObject rowsRoot = CreateUIObject("Rows", panel.transform);
        RectTransform rowsRT = rowsRoot.GetComponent<RectTransform>();
        rowsRT.anchorMin = new Vector2(0f, 0f);
        rowsRT.anchorMax = new Vector2(1f, 1f);
        rowsRT.pivot = new Vector2(0.5f, 0.5f);
        rowsRT.offsetMin = new Vector2(24f, 24f);
        rowsRT.offsetMax = new Vector2(-24f, -70f);

        var vlg = rowsRoot.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.spacing = 14f;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        towerRows.Clear();
        for (int i = 0; i < mainTowers.Length; i++)
        {
            TowerStatusRow row = CreateTowerRow(rowsRoot.transform, $"Вышка {i + 1}");
            towerRows.Add(row);
        }
    }

    private TowerStatusRow CreateTowerRow(Transform parent, string label)
    {
        GameObject rowGO = CreateUIObject(label.Replace(" ", "_"), parent);
        var rowLayout = rowGO.AddComponent<HorizontalLayoutGroup>();
        rowLayout.childAlignment = TextAnchor.MiddleLeft;
        rowLayout.spacing = 10f;
        rowLayout.childControlHeight = false;
        rowLayout.childControlWidth = false;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childForceExpandHeight = false;

        var rowLE = rowGO.AddComponent<LayoutElement>();
        rowLE.preferredHeight = 44f;

        GameObject labelGO = CreateUIObject("Label", rowGO.transform);
        Text labelText = labelGO.AddComponent<Text>();
        labelText.text = label;
        labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        labelText.fontSize = 20;
        labelText.color = Color.white;
        labelText.alignment = TextAnchor.MiddleLeft;
        labelText.raycastTarget = false;

        var labelLE = labelGO.AddComponent<LayoutElement>();
        labelLE.preferredWidth = 120f;

        Sprite uiSprite = GetUiBarSprite();

        GameObject barBG = CreateUIObject("BarBG", rowGO.transform);
        Image barBGImg = barBG.AddComponent<Image>();
        barBGImg.sprite = uiSprite;
        barBGImg.color = new Color(0.2f, 0.2f, 0.2f, 1f);

        var barLE = barBG.AddComponent<LayoutElement>();
        barLE.preferredWidth = 220f;
        barLE.preferredHeight = 14f;

        RectTransform barRT = barBG.GetComponent<RectTransform>();
        barRT.sizeDelta = new Vector2(220f, 14f);

        GameObject barFill = CreateUIObject("BarFill", barBG.transform);
        Image fillImg = barFill.AddComponent<Image>();
        fillImg.sprite = uiSprite;
        fillImg.type = Image.Type.Filled;
        fillImg.fillMethod = Image.FillMethod.Horizontal;
        fillImg.fillOrigin = 0;
        fillImg.fillAmount = 1f;
        fillImg.color = new Color(0.2f, 0.9f, 0.4f, 1f);

        RectTransform fillRT = barFill.GetComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = Vector2.one;
        fillRT.offsetMin = fillRT.offsetMax = Vector2.zero;

        GameObject valueGO = CreateUIObject("Value", rowGO.transform);
        Text valueText = valueGO.AddComponent<Text>();
        valueText.text = "0";
        valueText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        valueText.fontSize = 20;
        valueText.color = Color.white;
        valueText.alignment = TextAnchor.MiddleRight;
        valueText.raycastTarget = false;

        var valueLE = valueGO.AddComponent<LayoutElement>();
        valueLE.preferredWidth = 70f;

        return new TowerStatusRow
        {
            label = labelText,
            fill = fillImg,
            value = valueText
        };
    }

    private void UpdateMainTowersUI()
    {
        if (towerRows.Count == 0) return;

        for (int i = 0; i < towerRows.Count; i++)
        {
            TowerStatusRow row = towerRows[i];
            Building tower = (i < mainTowers.Length) ? mainTowers[i] : null;
            int maxHp = (i < mainTowerMaxHp.Length) ? mainTowerMaxHp[i] : 1;

            if (tower == null)
            {
                row.value.text = "0";
                row.fill.fillAmount = 0f;
                row.label.color = new Color(0.7f, 0.7f, 0.7f, 1f);
                row.value.color = new Color(0.7f, 0.7f, 0.7f, 1f);
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
            row.fill.fillAmount = t;
            row.value.text = currentHp.ToString();
            row.label.color = Color.white;
            row.value.color = Color.white;
        }
    }

    private void ResetMana()
    {
        currentMana = Mathf.Clamp(startMana, 0, maxMana);
        UpdateManaUI();
    }

    public bool TrySpendMana(int amount)
    {
        if (amount <= 0) return true;
        if (currentMana < amount)
        {
            manaFlashTimer = 0.6f;
            UpdateManaUI();
            return false;
        }

        currentMana -= amount;
        UpdateManaUI();
        return true;
    }

    public void AddMana(int amount)
    {
        if (amount <= 0) return;
        currentMana = Mathf.Clamp(currentMana + amount, 0, maxMana);
        UpdateManaUI();
    }

    private void UpdateManaUI()
    {
        if (manaText != null)
            manaText.text = $"Мана: {currentMana}";
        if (manaFill != null)
            manaFill.fillAmount = (float)currentMana / Mathf.Max(1, maxMana);
    }

    private void UpdateManaFlash()
    {
        if (manaText == null) return;
        if (manaFlashTimer > 0f)
        {
            manaFlashTimer -= Time.deltaTime;
            manaText.color = Color.Lerp(Color.white, new Color(1f, 0.4f, 0.4f, 1f), 0.6f);
        }
        else
        {
            manaText.color = Color.white;
        }
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

        cachedCannonSprite = LoadSpriteFromAssetOrDisk("Assets/Sprites/cannon/cannon1.png", "Sprites/cannon/cannon1.png", 100f);
        return cachedCannonSprite;
    }

    private Sprite GetSharedBulletSprite()
    {
        if (triedLoadBulletSprite) return cachedBulletSprite;
        triedLoadBulletSprite = true;

        cachedBulletSprite = LoadSpriteFromAssetOrDisk("Assets/Sprites/bullet.png", "Sprites/bullet.png", 100f);
        return cachedBulletSprite;
    }

    private Sprite LoadSpriteFromAssetOrDisk(string assetPath, string relativePath, float pixelsPerUnit)
    {
#if UNITY_EDITOR
        Sprite assetSprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (assetSprite != null)
            return assetSprite;
#endif
        return LoadSpriteFromDisk(relativePath, pixelsPerUnit);
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
        if (mainTowersRegisteredCount < mainTowers.Length) return;

        for (int i = 0; i < mainTowers.Length; i++)
        {
            Building tower = mainTowers[i];
            if (tower != null && tower.hp > 0)
                return;
        }

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
