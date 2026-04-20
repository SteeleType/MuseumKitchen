using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Client-side narrative cooking flow:
///   Stage 1 — Pick Region          → map zooms to that region
///   Stage 2 — Pick Spice           → map zooms out + caravan flies origin → destination
///   Stage 3 — Pick Cooking Method  → cooking icon punches in
///   Stage 4 — Cook reveal          → big "Cook!" button reveals the dish
///   Stage 5 — Slide-to-send        → drag the dish card up off-screen to commit
///
/// Builds the entire UI at runtime so the scene only needs one GameObject hosting this component.
/// 5 步叙事化点菜流程；运行时构建全部 UI。
/// </summary>
[RequireComponent(typeof(FirebaseSender))]
public class DishComposer : MonoBehaviour
{
    [Header("Map / 地图")]
    [Tooltip("Map background sprite (e.g. Assets/Art/MapOfTheWorld.png). Loaded at runtime if not set.\n地图背景 sprite，留空则按名查找。")]
    [SerializeField] private Sprite mapSprite;

    [Tooltip("Optional: a tiny sprite used for the caravan that travels along the spice route. Falls back to a generated dot.\n商队 sprite，留空则用程式化小圆点。")]
    [SerializeField] private Sprite caravanSprite;

    [Tooltip("Coordinates SO mapping each Region/Origin to normalized map positions.\n地图坐标配置 SO。")]
    [SerializeField] private MapCoordinatesConfig mapCoords;

    [Header("Cooking Method Sprites / 烹饪方式图片")]
    [SerializeField] private Sprite panSprite;
    [SerializeField] private Sprite potSprite;
    [SerializeField] private Sprite ovenSprite;

    [Header("Submit / 提交")]
    [Tooltip("Seconds to wait after the card flies off before resetting back to the Region stage.\n卡片飞出后等多少秒重置回 Region 选择阶段。")]
    [SerializeField] private float resetDelayAfterSubmit = 1.0f;
    [SerializeField] private float swipeDistanceThreshold = 300f;

    private FirebaseSender sender;
    private Canvas hostCanvas;

    // Selection state
    private Region? _region;
    private Spice? _spice;
    private CookingMethod? _cooking;
    private Dish _resolvedDish;

    // UI references
    private RectTransform _mapRT;        // the map sprite that we zoom/pan
    private RectTransform _mapHolder;    // parent that clips the map
    private RectTransform _overlayLayer; // for caravan, route line, markers (child of map holder so it pans with map)
    private Transform _stagePanelHolder; // current step's panel (under canvas, not under map)
    private GameObject _currentStagePanel;
    private TMP_InputField _chefInput;
    private float _lastSubmitTime = -10f;
    private const float SubmitDebounceSeconds = 1.0f;

    private static readonly Color UnselectedColor = new Color(0.18f, 0.18f, 0.24f, 0.92f);
    private static readonly Color SelectedColor   = new Color(0.95f, 0.7f, 0.25f, 1f);
    private static readonly Color DisabledColor   = new Color(0.12f, 0.12f, 0.15f, 0.4f);

    private void Awake()
    {
        sender = GetComponent<FirebaseSender>();
        hostCanvas = FindObjectOfType<Canvas>();
        if (hostCanvas == null) { Debug.LogError("[DishComposer] No Canvas in scene."); return; }

        if (mapSprite == null)
        {
            // Try to auto-resolve from project
            var loaded = Resources.Load<Sprite>("MapOfTheWorld");
            if (loaded != null) mapSprite = loaded;
        }
        if (mapCoords == null)
        {
            mapCoords = Resources.Load<MapCoordinatesConfig>("MapCoordinates");
            if (mapCoords == null)
                Debug.LogWarning("[DishComposer] MapCoordinatesConfig not assigned; map zoom positions will use fallback (0.5, 0.5).");
        }

        BuildScaffold();
        EnterRegionStage();
    }

    // ───────────────────── Scaffold ─────────────────────

    private void BuildScaffold()
    {
        // Background fill
        var bg = NewRect("Background", hostCanvas.transform);
        Stretch(bg);
        var bgImg = bg.AddComponent<Image>();
        // Match the dark teal-navy of the world map's ocean so contain-mode side bars are invisible.
        bgImg.color = new Color(0.06f, 0.13f, 0.18f, 1f);

        // Map holder (clips overflow so zoomed map doesn't bleed)
        var holder = NewRect("MapHolder", hostCanvas.transform);
        var hrt = holder.GetComponent<RectTransform>();
        hrt.anchorMin = Vector2.zero; hrt.anchorMax = Vector2.one;
        hrt.offsetMin = Vector2.zero; hrt.offsetMax = Vector2.zero;
        holder.AddComponent<RectMask2D>();
        _mapHolder = hrt;

        // Map image (fills the holder; we'll scale/translate this)
        var map = NewRect("Map", holder.transform);
        _mapRT = map.GetComponent<RectTransform>();
        _mapRT.anchorMin = new Vector2(0.5f, 0.5f);
        _mapRT.anchorMax = new Vector2(0.5f, 0.5f);
        _mapRT.pivot = new Vector2(0.5f, 0.5f);
        _mapRT.anchoredPosition = Vector2.zero;

        var mapImg = map.AddComponent<Image>();
        mapImg.sprite = mapSprite;
        mapImg.preserveAspect = false; // we size the RT in cover-mode below, no need for built-in fit
        mapImg.color = new Color(1, 1, 1, 0.85f);
        FitMapToHolder();

        // Overlay layer (route line, markers, caravan) — child of map so it follows zoom/pan
        var overlay = NewRect("Overlay", map.transform);
        _overlayLayer = overlay.GetComponent<RectTransform>();
        _overlayLayer.anchorMin = Vector2.zero; _overlayLayer.anchorMax = Vector2.one;
        _overlayLayer.offsetMin = Vector2.zero; _overlayLayer.offsetMax = Vector2.zero;

        // Stage panel holder (above map; each stage's UI lives here)
        var stage = NewRect("StagePanels", hostCanvas.transform);
        var srt = stage.GetComponent<RectTransform>();
        srt.anchorMin = Vector2.zero; srt.anchorMax = Vector2.one;
        srt.offsetMin = Vector2.zero; srt.offsetMax = Vector2.zero;
        _stagePanelHolder = stage.transform;
    }

    private void FitMapToHolder()
    {
        // Width-fit, vertical overflow: image width matches screen width exactly,
        // top/bottom may overflow and get masked by the holder's RectMask2D. No side bars.
        // 宽度对齐屏幕，垂直方向溢出被裁，左右无黑边。
        Vector2 holderSize = _mapHolder.rect.size;
        if (mapSprite == null || holderSize.x <= 0)
        {
            _mapRT.sizeDelta = holderSize;
            return;
        }
        Vector2 spriteSize = mapSprite.rect.size;
        float scale = holderSize.x / spriteSize.x;
        _mapRT.sizeDelta = spriteSize * scale;
    }

    // ───────────────────── Stage 1: Region ─────────────────────

    private void EnterRegionStage()
    {
        ResetMap();
        var panel = NewStagePanel("RegionPanel");

        AddTitleBar(panel, "Where are you cooking from?", "Pick a region.");

        var row = AddBottomBar(panel, 180f);
        foreach (Region r in Enum.GetValues(typeof(Region)))
        {
            bool available = DishDatabase.AvailableRegions().Contains(r);
            var btn = MakePill(row, FormatEnum(r), 240, !available);
            if (available)
            {
                var captured = r;
                btn.onClick.AddListener(() => OnPickRegion(captured));
            }
        }
    }

    private void OnPickRegion(Region r)
    {
        _region = r;
        FadeOutCurrentStage();
        ZoomMapToRegion(r, 0.9f, () => EnterSpiceStage());
    }

    // ───────────────────── Stage 2: Spice ─────────────────────

    private void EnterSpiceStage()
    {
        var panel = NewStagePanel("SpicePanel");
        AddTitleBar(panel, $"What spice arrives in {FormatEnum(_region.Value)}?", "Pick a spice.");

        var row = AddBottomBar(panel, 180f);
        foreach (Spice s in Enum.GetValues(typeof(Spice)))
        {
            bool available = DishDatabase.AvailableSpicesForRegion(_region.Value).Contains(s);
            var btn = MakePill(row, FormatEnum(s), 200, !available);
            if (available)
            {
                var captured = s;
                btn.onClick.AddListener(() => OnPickSpice(captured));
            }
        }
    }

    private void OnPickSpice(Spice s)
    {
        _spice = s;
        var origin = SpiceManager.AddSpiceOrigin(s);
        FadeOutCurrentStage();
        ZoomMapToWorld(0.9f, () =>
        {
            PlayCaravanFromOriginToRegion(origin, _region.Value, 2.0f, () => EnterCookingStage());
        });
    }

    // ───────────────────── Stage 3: Cooking ─────────────────────

    private GameObject _cookingTilesRow;

    private void EnterCookingStage()
    {
        var panel = NewStagePanel("CookingPanel");
        AddTitleBar(panel, "How do you cook it?", "Pick a method.");

        // Container for the 3 tiles (no layout group — we position by index for reliable hit areas).
        // 3 张 tile 容器；不用 layout group，按 index 手控位置避免触摸热区错位。
        var row = NewRect("CookingTiles", panel.transform);
        var rRT = row.GetComponent<RectTransform>();
        rRT.anchorMin = new Vector2(0.5f, 0.5f); rRT.anchorMax = new Vector2(0.5f, 0.5f);
        rRT.pivot = new Vector2(0.5f, 0.5f); rRT.anchoredPosition = new Vector2(0, 0);
        rRT.sizeDelta = new Vector2(1400, 460);
        _cookingTilesRow = row;

        var values = Enum.GetValues(typeof(CookingMethod));
        const float Spacing = 460f; // distance between tile centers
        int total = values.Length;
        float startX = -Spacing * (total - 1) / 2f;

        int i = 0;
        foreach (CookingMethod c in values)
        {
            bool available = DishDatabase
                .AvailableCookingForRegionSpice(_region.Value, _spice.Value)
                .Contains(c);
            float x = startX + i * Spacing;
            BuildCookingTile(row, c, available, x);
            i++;
        }
    }

    private void BuildCookingTile(GameObject parent, CookingMethod c, bool available, float xOffset)
    {
        var tile = NewRect(c + "Tile", parent.transform);
        var tRT = tile.GetComponent<RectTransform>();
        tRT.anchorMin = new Vector2(0.5f, 0.5f); tRT.anchorMax = new Vector2(0.5f, 0.5f);
        tRT.pivot = new Vector2(0.5f, 0.5f);
        tRT.anchoredPosition = new Vector2(xOffset, 0);
        tRT.sizeDelta = new Vector2(360, 440);

        // Invisible raycast catcher + Button on the tile root
        var raycastImg = tile.AddComponent<Image>();
        raycastImg.color = new Color(0, 0, 0, 0);
        var btn = tile.AddComponent<Button>();
        btn.targetGraphic = raycastImg;
        btn.interactable = available;

        // Dish image
        var imgGO = NewRect("Image", tile.transform);
        var img = imgGO.AddComponent<Image>();
        img.sprite = GetCookingSprite(c);
        img.preserveAspect = true;
        img.raycastTarget = false;
        img.color = available ? Color.white : new Color(1, 1, 1, 0.3f);
        var iRT = imgGO.GetComponent<RectTransform>();
        iRT.anchorMin = new Vector2(0.5f, 1f); iRT.anchorMax = new Vector2(0.5f, 1f);
        iRT.pivot = new Vector2(0.5f, 1f);
        iRT.anchoredPosition = new Vector2(0, -10);
        iRT.sizeDelta = new Vector2(300, 300);

        // Label below
        var label = NewText("Label", tile, FormatEnum(c), 32, FontStyles.Bold,
            available ? Color.white : new Color(1, 1, 1, 0.4f), TextAlignmentOptions.Center);
        var lRT = label.GetComponent<RectTransform>();
        lRT.anchorMin = new Vector2(0.5f, 0f); lRT.anchorMax = new Vector2(0.5f, 0f);
        lRT.pivot = new Vector2(0.5f, 0f); lRT.anchoredPosition = new Vector2(0, 10);
        lRT.sizeDelta = new Vector2(300, 60);
        label.GetComponent<TextMeshProUGUI>().raycastTarget = false;

        if (available)
        {
            var captured = c;
            btn.onClick.AddListener(() => OnPickCooking(captured));
        }
    }

    private Sprite GetCookingSprite(CookingMethod c) => c switch
    {
        CookingMethod.Pan  => panSprite,
        CookingMethod.Pot  => potSprite,
        CookingMethod.Oven => ovenSprite,
        _ => null
    };

    private void OnPickCooking(CookingMethod c)
    {
        _cooking = c;
        _resolvedDish = DishDatabase.Find(_region.Value, _spice.Value, _cooking.Value);

        // Fade out the row of tiles
        if (_cookingTilesRow != null)
        {
            var cg = _cookingTilesRow.GetComponent<CanvasGroup>() ?? _cookingTilesRow.AddComponent<CanvasGroup>();
            cg.DOFade(0f, 0.3f);
        }

        // Build a new centerpiece on the same panel: big sprite + Cook button below.
        // 在同一个 panel 上构建新中心组：选中图（大）+ Cook 按钮在图下面。
        var center = NewRect("CookingCenterpiece", _currentStagePanel.transform);
        var crt = center.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(0.5f, 0.5f); crt.anchorMax = new Vector2(0.5f, 0.5f);
        crt.pivot = new Vector2(0.5f, 0.5f); crt.anchoredPosition = new Vector2(0, 30);
        crt.sizeDelta = new Vector2(540, 700);

        // Big cooking image
        var imgGO = NewRect("Image", center.transform);
        var img = imgGO.AddComponent<Image>();
        img.sprite = GetCookingSprite(c);
        img.preserveAspect = true;
        img.raycastTarget = false;
        var iRT = imgGO.GetComponent<RectTransform>();
        iRT.anchorMin = new Vector2(0.5f, 1f); iRT.anchorMax = new Vector2(0.5f, 1f);
        iRT.pivot = new Vector2(0.5f, 1f); iRT.anchoredPosition = new Vector2(0, 0);
        iRT.sizeDelta = new Vector2(480, 480);

        // Method label
        var lab = NewText("Label", center, FormatEnum(c), 36, FontStyles.Bold,
            Color.white, TextAlignmentOptions.Center);
        var lRT = lab.GetComponent<RectTransform>();
        lRT.anchorMin = new Vector2(0.5f, 1f); lRT.anchorMax = new Vector2(0.5f, 1f);
        lRT.pivot = new Vector2(0.5f, 1f); lRT.anchoredPosition = new Vector2(0, -490);
        lRT.sizeDelta = new Vector2(540, 50);

        // Cook button below the image+label
        var btnGO = NewRect("CookBtn", center.transform);
        var bRT = btnGO.GetComponent<RectTransform>();
        bRT.anchorMin = new Vector2(0.5f, 1f); bRT.anchorMax = new Vector2(0.5f, 1f);
        bRT.pivot = new Vector2(0.5f, 1f); bRT.anchoredPosition = new Vector2(0, -560);
        bRT.sizeDelta = new Vector2(420, 110);
        var bg = btnGO.AddComponent<Image>();
        bg.color = new Color(0.85f, 0.35f, 0.2f, 1f);
        var btn = btnGO.AddComponent<Button>();
        btn.targetGraphic = bg;
        var blab = NewText("Label", btnGO, "Cook!", 50, FontStyles.Bold, Color.white, TextAlignmentOptions.Center);
        Stretch(blab);
        blab.GetComponent<TextMeshProUGUI>().raycastTarget = false;

        // Pop-in animation
        center.transform.localScale = Vector3.one * 0.6f;
        var ccg = center.AddComponent<CanvasGroup>();
        ccg.alpha = 0;
        ccg.DOFade(1f, 0.4f).SetDelay(0.15f);
        center.transform.DOScale(1f, 0.4f).SetEase(Ease.OutBack).SetDelay(0.15f);

        btn.onClick.AddListener(() =>
        {
            FadeOutCurrentStage();
            EnterDishRevealStage();
        });
    }

    // ───────────────────── Stage 4: Dish Reveal ─────────────────────

    // Renamed from EnterCookRevealStage — Cook button now lives in OnPickCooking, so this stage
    // just shows the dimmed dish card straight away.
    // 之前的 Cook 大按钮已合并到 OnPickCooking，本阶段直接显示菜品卡片。
    private void EnterDishRevealStage()
    {
        var panel = NewStagePanel("DishRevealPanel");

        var dim = NewRect("Dim", panel.transform);
        Stretch(dim);
        var dimImg = dim.AddComponent<Image>();
        dimImg.color = new Color(0, 0, 0, 0.85f);

        ShowDishCard(panel);
    }

    private void ShowDishCard(GameObject panel)
    {
        // Chef name input (top)
        var inputRow = NewRect("ChefInputRow", panel.transform);
        var irt = inputRow.GetComponent<RectTransform>();
        irt.anchorMin = new Vector2(0.5f, 1); irt.anchorMax = new Vector2(0.5f, 1);
        irt.pivot = new Vector2(0.5f, 1); irt.anchoredPosition = new Vector2(0, -40);
        irt.sizeDelta = new Vector2(700, 80);
        BuildChefInput(inputRow);

        // Reveal container — no background frame; the dim panel is already behind everything.
        // Holds the dish image (large), name + country (under image), hint (bottom), and is the drag target.
        // 揭示容器：没有自己的背景（dim 在更后面已经盖了），大图 + 文字 + 提示，整块作为拖拽接收。
        var card = NewRect("DishPlate", panel.transform);
        var crt = card.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(0.5f, 0.5f); crt.anchorMax = new Vector2(0.5f, 0.5f);
        crt.pivot = new Vector2(0.5f, 0.5f); crt.anchoredPosition = Vector2.zero;
        crt.sizeDelta = new Vector2(800, 900);

        // Invisible raycast catcher so the entire card area accepts drags (incl. empty space around the image).
        // 透明捕获 Image，让整块区域都能接收拖拽。
        var rayCatcher = card.AddComponent<Image>();
        rayCatcher.color = new Color(0, 0, 0, 0);
        rayCatcher.raycastTarget = true;

        // Big dish image, centered upper portion
        if (_resolvedDish != null && _resolvedDish.DishSprite != null)
        {
            var imgGO = NewRect("DishImage", card.transform);
            var img = imgGO.AddComponent<Image>();
            img.sprite = _resolvedDish.DishSprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
            var iRT = imgGO.GetComponent<RectTransform>();
            iRT.anchorMin = new Vector2(0.5f, 1f); iRT.anchorMax = new Vector2(0.5f, 1f);
            iRT.pivot = new Vector2(0.5f, 1f);
            iRT.anchoredPosition = new Vector2(0, -20);
            iRT.sizeDelta = new Vector2(640, 640);
        }

        // Dish name (under image)
        string dishName = _resolvedDish != null ? _resolvedDish.DishName : "Mystery Dish";
        var nameGO = NewText("Name", card, dishName, 44, FontStyles.Bold,
            new Color(1f, 0.85f, 0.35f), TextAlignmentOptions.Center);
        var nRT = nameGO.GetComponent<RectTransform>();
        nRT.anchorMin = new Vector2(0.5f, 1f); nRT.anchorMax = new Vector2(0.5f, 1f);
        nRT.pivot = new Vector2(0.5f, 1f);
        nRT.anchoredPosition = new Vector2(0, -680);
        nRT.sizeDelta = new Vector2(800, 60);
        nameGO.GetComponent<TextMeshProUGUI>().raycastTarget = false;

        // Country (under name)
        string country = _resolvedDish != null ? _resolvedDish.CountryOfOrigin : "Unknown";
        var originGO = NewText("Origin", card, $"<i>from {country}</i>", 24, FontStyles.Italic,
            new Color(0.85f, 0.85f, 0.95f), TextAlignmentOptions.Center);
        var oRT = originGO.GetComponent<RectTransform>();
        oRT.anchorMin = new Vector2(0.5f, 1f); oRT.anchorMax = new Vector2(0.5f, 1f);
        oRT.pivot = new Vector2(0.5f, 1f);
        oRT.anchoredPosition = new Vector2(0, -748);
        oRT.sizeDelta = new Vector2(800, 36);
        originGO.GetComponent<TextMeshProUGUI>().raycastTarget = false;

        // Hint (bottom, pulsing)
        var hintGO = NewText("Hint", card, "↑  Slide up to send", 26, FontStyles.Bold,
            new Color(1f, 0.6f, 0.4f), TextAlignmentOptions.Center);
        var hRT = hintGO.GetComponent<RectTransform>();
        hRT.anchorMin = new Vector2(0.5f, 0f); hRT.anchorMax = new Vector2(0.5f, 0f);
        hRT.pivot = new Vector2(0.5f, 0f);
        hRT.anchoredPosition = new Vector2(0, 20);
        hRT.sizeDelta = new Vector2(800, 40);
        var hintTMP = hintGO.GetComponent<TextMeshProUGUI>();
        hintTMP.raycastTarget = false;
        hintTMP.DOFade(0.3f, 0.8f).SetLoops(-1, LoopType.Yoyo);

        // Drag handler on the card itself
        var drag = card.AddComponent<DishCardDragger>();
        drag.Init(this, swipeDistanceThreshold);

        card.transform.localScale = Vector3.zero;
        card.transform.DOScale(1f, 0.5f).SetEase(Ease.OutBack);
    }

    private void BuildChefInput(GameObject row)
    {
        row.AddComponent<Image>().color = new Color(0.10f, 0.12f, 0.18f, 0.85f);

        var hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 12; hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.padding = new RectOffset(20, 20, 8, 8);
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;

        var label = NewText("Label", row, "Chef:", 22, FontStyles.Normal, Color.white, TextAlignmentOptions.MidlineRight);
        label.AddComponent<LayoutElement>().preferredWidth = 80;

        var inputGO = NewRect("Input", row);
        inputGO.AddComponent<Image>().color = new Color(0.18f, 0.18f, 0.24f, 1f);
        inputGO.AddComponent<LayoutElement>().preferredWidth = 460;

        var ta = NewRect("TextArea", inputGO);
        var taRT = ta.GetComponent<RectTransform>();
        taRT.anchorMin = Vector2.zero; taRT.anchorMax = Vector2.one;
        taRT.offsetMin = new Vector2(12, 6); taRT.offsetMax = new Vector2(-12, -6);
        ta.AddComponent<RectMask2D>();

        var ph = NewText("Placeholder", ta, ChefNameGenerator.Generate(), 20, FontStyles.Italic,
            new Color(0.6f, 0.6f, 0.7f, 0.8f), TextAlignmentOptions.MidlineLeft);
        Stretch(ph);

        var txt = NewText("Text", ta, "", 20, FontStyles.Normal, Color.white, TextAlignmentOptions.MidlineLeft);
        Stretch(txt);

        _chefInput = inputGO.AddComponent<TMP_InputField>();
        _chefInput.textViewport = taRT;
        _chefInput.textComponent = txt.GetComponent<TMP_Text>();
        _chefInput.placeholder = ph.GetComponent<TMP_Text>();
        _chefInput.characterLimit = ChefNameGenerator.MaxLength;
        _chefInput.text = "";
    }

    // ───────────────────── Stage 5: Slide submit (called by DishCardDragger) ─────────────────────

    public void OnDishSwipedUp()
    {
        if (Time.unscaledTime - _lastSubmitTime < SubmitDebounceSeconds) return;
        _lastSubmitTime = Time.unscaledTime;
        if (_resolvedDish == null) return;

        var chef = ChefNameGenerator.Sanitize(_chefInput != null ? _chefInput.text : "");
        var data = new PotluckData
        {
            clientId = chef,
            dishName = _resolvedDish.DishName,
            countryOfOrigin = _resolvedDish.CountryOfOrigin,
            region = _resolvedDish.Region.ToString(),
            spice = _resolvedDish.Spice.ToString(),
            cookingMethod = _resolvedDish.CookingMethod.ToString(),
            spiceOrigin = _resolvedDish.SpiceOrigin.ToString(),
            distanceMiles = _resolvedDish.DistanceTraveledMiles,
            dishAssetName = _resolvedDish.name
        };

        sender.SendDumplingData(data);
        Debug.Log($"[DishComposer] Sent: {chef} → {_resolvedDish.DishName}");

        DOVirtual.DelayedCall(resetDelayAfterSubmit, ResetForNextRound).SetLink(gameObject);
    }

    private void ResetForNextRound()
    {
        _region = null;
        _spice = null;
        _cooking = null;
        _resolvedDish = null;
        FadeOutCurrentStage();
        EnterRegionStage();
    }

    // ───────────────────── Map animations ─────────────────────

    private void ResetMap()
    {
        _mapRT.localScale = Vector3.one;
        _mapRT.anchoredPosition = Vector2.zero;
        ClearOverlay();
    }

    private void ZoomMapToWorld(float duration, Action onDone)
    {
        ClearOverlay();
        var seq = DOTween.Sequence();
        seq.Append(_mapRT.DOScale(1f, duration).SetEase(Ease.InOutCubic));
        seq.Join(_mapRT.DOAnchorPos(Vector2.zero, duration).SetEase(Ease.InOutCubic));
        seq.OnComplete(() => onDone?.Invoke());
    }

    private void ZoomMapToRegion(Region r, float duration, Action onDone)
    {
        Vector2 norm = new Vector2(0.5f, 0.5f);
        float zoom = 2.5f;
        if (mapCoords != null && mapCoords.TryGetRegion(r, out var pt))
        {
            norm = pt.normalizedPos;
            zoom = pt.zoom > 0 ? pt.zoom : mapCoords.defaultRegionZoom;
        }

        // Translate so that the normalized point is centered, accounting for current zoom (=1)
        Vector2 size = _mapRT.rect.size;
        Vector2 offset = new Vector2(
            (0.5f - norm.x) * size.x * zoom,
            (0.5f - norm.y) * size.y * zoom);

        var seq = DOTween.Sequence();
        seq.Append(_mapRT.DOScale(zoom, duration).SetEase(Ease.InOutCubic));
        seq.Join(_mapRT.DOAnchorPos(offset, duration).SetEase(Ease.InOutCubic));
        seq.OnComplete(() => onDone?.Invoke());
    }

    private void PlayCaravanFromOriginToRegion(SpiceOrigin origin, Region dest, float duration, Action onDone)
    {
        Vector2 normOrigin = new Vector2(0.5f, 0.5f);
        Vector2 normDest = new Vector2(0.5f, 0.5f);
        if (mapCoords != null)
        {
            mapCoords.TryGetSpiceOrigin(origin, out normOrigin);
            if (mapCoords.TryGetRegion(dest, out var pt)) normDest = pt.normalizedPos;
        }

        Vector2 size = _mapRT.rect.size;
        Vector2 originPx = NormToPx(normOrigin, size);
        Vector2 destPx   = NormToPx(normDest, size);

        // Build the full normalized control polyline: origin → user waypoints (if any) → dest.
        // Then sample as Catmull-Rom for smooth curves.
        // 完整路径 = 起点 + 用户配的中间点 + 终点；按 Catmull-Rom 平滑。
        var ctlPx = new List<Vector2> { originPx };
        if (mapCoords != null && mapCoords.TryGetRoute(origin, dest, out var wps))
        {
            foreach (var w in wps) ctlPx.Add(NormToPx(w, size));
        }
        else
        {
            // No user route configured → automatic Bezier-style arc (single mid waypoint).
            // 没配 → 用 quadratic-bezier 风格,加一个北向中点作为 waypoint。
            Vector2 mid = (originPx + destPx) * 0.5f;
            Vector2 dir = destPx - originPx;
            float len = dir.magnitude;
            Vector2 perp = len > 0.001f ? new Vector2(-dir.y, dir.x).normalized : Vector2.up;
            if (perp.y < 0) perp = -perp;
            float arcHeight = Mathf.Clamp(len * 0.28f, 60f, 600f);
            ctlPx.Add(mid + perp * arcHeight);
        }
        ctlPx.Add(destPx);

        // Sample curve into dense path points for both the line drawing and the caravan motion.
        const int SamplesPerSegment = 16;
        var samples = SampleCatmullRom(ctlPx, SamplesPerSegment);

        // Markers
        AddMarker(originPx, new Color(1f, 0.85f, 0.4f), origin.ToString());
        AddMarker(destPx,   new Color(1f, 0.5f,  0.3f), dest.ToString());

        // Draw the route progressively: each segment between samples fades in staggered.
        // 路径逐段淡入，形成"被画出来"的感觉。
        float drawWindow = Mathf.Max(0.2f, duration * 0.6f);
        float perSegmentDelay = samples.Count > 1 ? drawWindow / (samples.Count - 1) : 0f;
        for (int i = 1; i < samples.Count; i++)
        {
            var img = AddRouteSegment(samples[i - 1], samples[i]);
            img.color = new Color(1f, 0.85f, 0.4f, 0f);
            img.DOFade(0.85f, 0.18f).SetDelay((i - 1) * perSegmentDelay);
        }

        // Caravan
        var car = NewRect("Caravan", _overlayLayer);
        var cImg = car.AddComponent<Image>();
        cImg.sprite = caravanSprite;
        cImg.color = caravanSprite != null ? Color.white : new Color(0.95f, 0.7f, 0.25f);
        cImg.preserveAspect = true;
        var crt = car.GetComponent<RectTransform>();
        crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(0.5f, 0.5f);
        crt.sizeDelta = new Vector2(40, 40);
        crt.anchoredPosition = samples[0];
        Vector2 firstTangent = samples.Count > 1 ? (samples[1] - samples[0]) : Vector2.right;
        crt.localEulerAngles = new Vector3(0, 0, Mathf.Atan2(firstTangent.y, firstTangent.x) * Mathf.Rad2Deg);

        // Animate caravan along sampled path. t∈[0,1] → index in samples.
        // 沿采样点移动，t 0→1 直接映射到 samples 索引区间。
        DOVirtual.DelayedCall(0.4f, () =>
        {
            float travelTime = Mathf.Max(0.5f, duration - 0.4f);
            float prog = 0f;
            DOTween.To(() => prog, x =>
            {
                prog = x;
                float fIdx = prog * (samples.Count - 1);
                int i0 = Mathf.Clamp(Mathf.FloorToInt(fIdx), 0, samples.Count - 2);
                int i1 = i0 + 1;
                float ft = fIdx - i0;
                Vector2 p = Vector2.Lerp(samples[i0], samples[i1], ft);
                crt.anchoredPosition = p;
                Vector2 tan = samples[i1] - samples[i0];
                if (tan.sqrMagnitude > 0.0001f)
                    crt.localEulerAngles = new Vector3(0, 0, Mathf.Atan2(tan.y, tan.x) * Mathf.Rad2Deg);
            }, 1f, travelTime).SetEase(Ease.InOutSine).OnComplete(() => onDone?.Invoke());
        });
    }

    private static Vector2 NormToPx(Vector2 norm, Vector2 mapSize) =>
        new Vector2((norm.x - 0.5f) * mapSize.x, (norm.y - 0.5f) * mapSize.y);

    /// <summary>Sample a uniform Catmull-Rom spline through the given control points (must have ≥ 2).</summary>
    public static List<Vector2> SampleCatmullRom(List<Vector2> ctl, int samplesPerSegment)
    {
        var result = new List<Vector2>();
        if (ctl == null || ctl.Count == 0) return result;
        if (ctl.Count == 1) { result.Add(ctl[0]); return result; }

        // Pad endpoints so first/last segments work with 4-point Catmull-Rom.
        var p = new List<Vector2>(ctl.Count + 2);
        p.Add(ctl[0] + (ctl[0] - ctl[1]));        // ghost before
        p.AddRange(ctl);
        p.Add(ctl[ctl.Count - 1] + (ctl[ctl.Count - 1] - ctl[ctl.Count - 2])); // ghost after

        result.Add(ctl[0]);
        for (int i = 0; i < ctl.Count - 1; i++)
        {
            Vector2 p0 = p[i], p1 = p[i + 1], p2 = p[i + 2], p3 = p[i + 3];
            for (int s = 1; s <= samplesPerSegment; s++)
            {
                float t = s / (float)samplesPerSegment;
                result.Add(CatmullRom(p0, p1, p2, p3, t));
            }
        }
        return result;
    }

    private static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        float t2 = t * t, t3 = t2 * t;
        return 0.5f * (
            2f * p1 +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3
        );
    }

    private Image AddRouteSegment(Vector2 a, Vector2 b)
    {
        var seg = NewRect("RouteSeg", _overlayLayer);
        var img = seg.AddComponent<Image>();
        var rt = seg.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        Vector2 dir = b - a;
        float len = dir.magnitude;
        rt.sizeDelta = new Vector2(len, 4f);
        rt.anchoredPosition = (a + b) * 0.5f;
        rt.localEulerAngles = new Vector3(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        return img;
    }

    private static Vector2 QuadraticBezier(Vector2 p0, Vector2 p1, Vector2 p2, float t)
    {
        float u = 1f - t;
        return u * u * p0 + 2f * u * t * p1 + t * t * p2;
    }

    private static Vector2 QuadraticTangent(Vector2 p0, Vector2 p1, Vector2 p2, float t)
    {
        return 2f * (1f - t) * (p1 - p0) + 2f * t * (p2 - p1);
    }

    private void AddMarker(Vector2 anchoredPos, Color color, string label)
    {
        var dot = NewRect("Marker", _overlayLayer);
        var img = dot.AddComponent<Image>();
        img.color = color;
        var rt = dot.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(20, 20);
        rt.anchoredPosition = anchoredPos;
        dot.transform.localScale = Vector3.zero;
        dot.transform.DOScale(1f, 0.3f).SetEase(Ease.OutBack);
    }

    private void ClearOverlay()
    {
        for (int i = _overlayLayer.childCount - 1; i >= 0; i--)
            Destroy(_overlayLayer.GetChild(i).gameObject);
    }

    // ───────────────────── Helpers ─────────────────────

    private GameObject NewStagePanel(string name)
    {
        if (_currentStagePanel != null) Destroy(_currentStagePanel);
        var p = NewRect(name, _stagePanelHolder);
        Stretch(p);
        var cg = p.AddComponent<CanvasGroup>();
        cg.alpha = 0;
        cg.DOFade(1f, 0.4f);
        _currentStagePanel = p;
        return p;
    }

    private void FadeOutCurrentStage()
    {
        if (_currentStagePanel == null) return;
        var go = _currentStagePanel;
        var cg = go.GetComponent<CanvasGroup>();
        if (cg != null) cg.DOFade(0f, 0.3f).OnComplete(() => Destroy(go));
        else Destroy(go);
        _currentStagePanel = null;
    }

    private void AddTitleBar(GameObject panel, string title, string subtitle)
    {
        var bar = NewRect("TitleBar", panel.transform);
        var rt = bar.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(0.5f, 1); rt.anchoredPosition = new Vector2(0, -20);
        rt.sizeDelta = new Vector2(-80, 130);
        bar.AddComponent<Image>().color = new Color(0, 0, 0, 0.55f);

        var vlg = bar.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(20, 20, 12, 12); vlg.spacing = 4;
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;

        var t = NewText("Title", bar, title, 38, FontStyles.Bold,
            new Color(1f, 0.85f, 0.35f), TextAlignmentOptions.Center);
        t.AddComponent<LayoutElement>().preferredHeight = 50;

        var s = NewText("Sub", bar, subtitle, 22, FontStyles.Italic,
            new Color(0.85f, 0.85f, 0.95f), TextAlignmentOptions.Center);
        s.AddComponent<LayoutElement>().preferredHeight = 32;
    }

    private GameObject AddBottomBar(GameObject panel, float height)
    {
        var bar = NewRect("BottomBar", panel.transform);
        var rt = bar.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(1, 0);
        rt.pivot = new Vector2(0.5f, 0); rt.anchoredPosition = new Vector2(0, 30);
        rt.sizeDelta = new Vector2(-80, height);
        bar.AddComponent<Image>().color = new Color(0, 0, 0, 0.55f);

        var hlg = bar.AddComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(20, 20, 20, 20); hlg.spacing = 14;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = false; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
        return bar;
    }

    private Button MakePill(GameObject parent, string label, float width, bool disabled)
    {
        var go = NewRect(label + "Btn", parent.transform);
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(width, 110);

        var img = go.AddComponent<Image>();
        img.color = disabled ? DisabledColor : UnselectedColor;

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.interactable = !disabled;

        var le = go.AddComponent<LayoutElement>();
        le.preferredWidth = width; le.preferredHeight = 110;

        var text = NewText("Label", go, label, 24, FontStyles.Bold, Color.white, TextAlignmentOptions.Center);
        Stretch(text);

        return btn;
    }

    private static string FormatEnum(System.Enum e)
    {
        // "MiddleEast" → "Middle East", "BlackPepper" → "Black Pepper"
        var s = e.ToString();
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < s.Length; i++)
        {
            if (i > 0 && char.IsUpper(s[i])) sb.Append(' ');
            sb.Append(s[i]);
        }
        return sb.ToString();
    }

    private static string CookingEmoji(CookingMethod c) => c switch
    {
        CookingMethod.Pan => "🍳",
        CookingMethod.Pot => "🍲",
        CookingMethod.Oven => "🔥",
        _ => "🍴"
    };

    // ───────────────────── UI primitives ─────────────────────

    private static GameObject NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.layer = LayerMask.NameToLayer("UI");
        return go;
    }

    private static GameObject NewRect(string name, GameObject parent) => NewRect(name, parent.transform);

    private static GameObject NewText(string name, Transform parent, string text, float size,
        FontStyles style, Color color, TextAlignmentOptions align)
    {
        var go = NewRect(name, parent);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = size; tmp.fontStyle = style; tmp.color = color; tmp.alignment = align;
        tmp.richText = true;
        UIFont.Apply(tmp);
        return go;
    }

    private static GameObject NewText(string name, GameObject parent, string text, float size,
        FontStyles style, Color color, TextAlignmentOptions align)
        => NewText(name, parent.transform, text, size, style, color, align);

    private static void Stretch(GameObject go)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }
}

/// <summary>
/// Drag handler that lets the user swipe the dish card upward off-screen to commit.
/// 让玩家向上拖拽 dish 卡片以提交。
/// </summary>
public class DishCardDragger : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private DishComposer owner;
    private float threshold;
    private RectTransform rt;
    private Vector2 startPos;
    private bool committed;

    public void Init(DishComposer owner, float swipeThreshold)
    {
        this.owner = owner;
        this.threshold = swipeThreshold;
        rt = (RectTransform)transform;
    }

    public void OnBeginDrag(PointerEventData e)
    {
        if (committed) return;
        startPos = rt.anchoredPosition;
    }

    public void OnDrag(PointerEventData e)
    {
        if (committed) return;
        // Restrict to vertical drag (cleaner)
        rt.anchoredPosition = startPos + new Vector2(0, Mathf.Max(0, e.position.y - e.pressPosition.y));
    }

    public void OnEndDrag(PointerEventData e)
    {
        if (committed) return;
        float distance = rt.anchoredPosition.y - startPos.y;
        if (distance >= threshold)
        {
            committed = true;
            // Fly off
            rt.DOAnchorPos(rt.anchoredPosition + new Vector2(0, 1500), 0.5f).SetEase(Ease.InCubic);
            transform.DOScale(0.5f, 0.5f);
            owner.OnDishSwipedUp();
        }
        else
        {
            // Snap back
            rt.DOAnchorPos(startPos, 0.3f).SetEase(Ease.OutQuad);
        }
    }
}
