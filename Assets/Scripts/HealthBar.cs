using System.Collections;
using UnityEngine;

public class HealthBar : MonoBehaviour
{
    private const string BACK_NAME = "HP_Back";
    private const string FILL_NAME = "HP_Fill";

    private Transform backTransform;
    private Transform fillTransform;
    private SpriteRenderer backRenderer;
    private SpriteRenderer fillRenderer;
    private Transform targetTransform;

    private int maxHp = 1;
    private int currentHp = 1;
    // computed per-object world offset from the object's pivot to the bar position
    private float computedWorldOffsetY = 0f;

    // visual settings
    // make width slightly less than a full cell
    public float width = 0.9f;
    // make bars thinner by default
    public float height = 0.06f;
    public float offsetY = -0.6f;

    private static Sprite whiteSprite;

    void Awake()
    {
        EnsureSprite();
        targetTransform = transform; // the object this component is attached to
        CreateChildren();
        // initialize computed offset to the default offsetY so we have a fallback
        computedWorldOffsetY = offsetY;
        Debug.Log($"HealthBar.Awake created for '{gameObject.name}'");
    }

    void EnsureSprite()
    {
        if (whiteSprite != null) return;
        Texture2D tex = new Texture2D(1, 1, TextureFormat.ARGB32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        // create sprite with pixelsPerUnit=1 so 1 texture pixel == 1 world unit
        whiteSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
    }

    void CreateChildren()
    {
        // create or find a root container for healthbars in the scene so bars are not affected by parent's scale
        GameObject root = GameObject.Find("HealthBars");
        if (root == null) root = new GameObject("HealthBars");

        // background (world-space)
        GameObject back = new GameObject(BACK_NAME + "_" + gameObject.name);
        backTransform = back.transform;
        backTransform.SetParent(root.transform, false);
        backRenderer = back.AddComponent<SpriteRenderer>();
        backRenderer.sprite = whiteSprite;
        backRenderer.sortingOrder = 100000;
        // dark background so red fill stands out
        backRenderer.color = new Color(0.15f, 0f, 0f, 1f);
        backRenderer.material = new Material(Shader.Find("Sprites/Default"));

        // fill
        GameObject fill = new GameObject(FILL_NAME + "_" + gameObject.name);
        fillTransform = fill.transform;
        fillTransform.SetParent(root.transform, false);
        fillRenderer = fill.AddComponent<SpriteRenderer>();
        fillRenderer.sprite = whiteSprite;
        fillRenderer.sortingOrder = 100001;
        fillRenderer.color = Color.red;
        fillRenderer.material = new Material(Shader.Find("Sprites/Default"));

        // set initial scales; actual world scale will be updated in LateUpdate
        backTransform.localScale = new Vector3(width, height, 1f);
        fillTransform.localScale = new Vector3(width, height, 1f);
    }

    void Start()
    {
        AdjustPositionAndSorting();
        Debug.Log($"HealthBar.Start adjusted for '{gameObject.name}' (offsetY={offsetY}, width={width}, height={height})");
        StartCoroutine(DebugDump());
    }

    void LateUpdate()
    {
        // update world position and sizing so bar follows the target without being affected by target scale
        if (backTransform == null || fillTransform == null || targetTransform == null) return;
        // keep bar positioned each frame using the last known hp values
        UpdateVisual(currentHp);
    }

    private System.Collections.IEnumerator DebugDump()
    {
        // wait one frame for transforms to settle
        yield return null;

        string[] childNames = new string[transform.childCount];
        for (int i = 0; i < transform.childCount; i++) childNames[i] = transform.GetChild(i).name;

        Debug.Log($"HealthBar.DebugDump for '{gameObject.name}': children=[{string.Join(",", childNames)}], parentPos={transform.position}, parentScale={transform.lossyScale}");

        if (backRenderer != null)
        {
            Debug.Log($" backRenderer.bounds={backRenderer.bounds} worldPos={backTransform.position} sortingLayer={backRenderer.sortingLayerName} order={backRenderer.sortingOrder} visible={backRenderer.isVisible}");
        }
        if (fillRenderer != null)
        {
            Debug.Log($" fillRenderer.bounds={fillRenderer.bounds} worldPos={fillTransform.position} sortingLayer={fillRenderer.sortingLayerName} order={fillRenderer.sortingOrder} visible={fillRenderer.isVisible}");
        }
    }

    void AdjustPositionAndSorting()
    {
        // Find sprite renderers on this object (and children) to compute bottom and sorting
        SpriteRenderer[] srs = GetComponentsInChildren<SpriteRenderer>();
        float lowestWorldY = float.PositiveInfinity;
        int maxSorting = int.MinValue;
        string sortingLayer = null;

        foreach (var sr in srs)
        {
            // ignore our own generated renderers
            if (sr == backRenderer || sr == fillRenderer) continue;

            Bounds b = sr.bounds;
            if (b.min.y < lowestWorldY) lowestWorldY = b.min.y;

            if (sr.sortingOrder > maxSorting) maxSorting = sr.sortingOrder;
            if (sortingLayer == null) sortingLayer = sr.sortingLayerName;
        }

        if (float.IsPositiveInfinity(lowestWorldY))
        {
            // no sprite renderers found — leave defaults
            Debug.Log($"HealthBar: no sprite renderers found for '{gameObject.name}'");
            return;
        }

        // convert lowest world y to local y
        Vector3 worldPoint = new Vector3(transform.position.x, lowestWorldY, transform.position.z);
        float localBottomY = transform.InverseTransformPoint(worldPoint).y;

        // place the bar slightly below bottom
        float offsetLocalY = localBottomY - (height * 0.5f) - 0.01f; // minimal spacing

        backTransform.localPosition = new Vector3(0f, offsetLocalY, 0f);
        // update fill position using same offset
        float fullWidth = width;
        float leftX = -fullWidth * 0.5f;
        // keep current fill width
        float currentFillWidth = fillTransform.localScale.x;
        fillTransform.localPosition = new Vector3(leftX + currentFillWidth * 0.5f, offsetLocalY, 0.01f);

        // set sorting layer/order to appear above the sprite
        if (sortingLayer != null)
        {
            backRenderer.sortingLayerName = sortingLayer;
            fillRenderer.sortingLayerName = sortingLayer;
        }
        if (maxSorting != int.MinValue)
        {
            backRenderer.sortingOrder = maxSorting + 1;
            fillRenderer.sortingOrder = maxSorting + 2;
        }

        Debug.Log($"HealthBar positioned for '{gameObject.name}': localBottomY={localBottomY:F3}, offsetLocalY={offsetLocalY:F3}, sortingLayer={sortingLayer}, maxSorting={maxSorting}");

        // compute a world-space vertical offset to use during runtime positioning
        Vector3 worldPointForOffset = transform.TransformPoint(new Vector3(0f, offsetLocalY, 0f));
        computedWorldOffsetY = worldPointForOffset.y - transform.position.y;
    }

    public void SetMaxHp(int max)
    {
        maxHp = Mathf.Max(1, max);
        currentHp = Mathf.Min(currentHp, maxHp);
        UpdateVisual(currentHp);
    }

    public void SetHp(int hp)
    {
        hp = Mathf.Clamp(hp, 0, maxHp);
        currentHp = hp;
        UpdateVisual(currentHp);
    }

    private void UpdateVisual(int hp)
    {
        if (fillTransform == null || backTransform == null || targetTransform == null) return;

        float t = (float)hp / (float)Mathf.Max(1, maxHp);
        float fullWidth = width;
        float fillWidth = Mathf.Max(0.001f, fullWidth * t);

        // place background and fill in world space
        // use computed offset (falls back to offsetY if AdjustPositionAndSorting didn't find sprites)
        float useOffset = computedWorldOffsetY;
        Vector3 worldPos = targetTransform.position + new Vector3(0f, useOffset, 0f);
        backTransform.position = worldPos;
        backTransform.localScale = new Vector3(fullWidth, height, 1f);

        // position fill so it's left-anchored
        // compute left corner world position
        Vector3 leftWorld = worldPos + new Vector3(-fullWidth * 0.5f, 0f, 0f);
        fillTransform.position = leftWorld + new Vector3(fillWidth * 0.5f, 0f, 0f);
        fillTransform.localScale = new Vector3(fillWidth, height, 1f);

        // always use red fill per user request
        fillRenderer.color = Color.red;
    }

    void OnDestroy()
    {
        // clean up the world-space GameObjects we created so they don't remain after
        // the owner (this component's GameObject) is destroyed
        if (backTransform != null)
        {
            try { Destroy(backTransform.gameObject); } catch { }
            backTransform = null;
            backRenderer = null;
        }
        if (fillTransform != null)
        {
            try { Destroy(fillTransform.gameObject); } catch { }
            fillTransform = null;
            fillRenderer = null;
        }
    }
}
