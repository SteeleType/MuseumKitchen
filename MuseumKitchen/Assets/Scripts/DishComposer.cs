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
///   Stage 1 - Pick Spice           -> map zooms to that spice origin
///   Stage 2 - Pick Travel Distance -> system chooses a destination country
///   Stage 3 - Spice Travel         -> map zooms out + caravan flies origin to destination
///   Stage 4 - Pick Cooking Method  -> cooking icon punches in
///   Stage 5 - Cook reveal          -> big "Cook!" button reveals the dish
///   Stage 6 - Slide-to-send        -> drag the dish card up off-screen to commit
///
/// Uses an optional artist-authored Canvas prefab, with the runtime-built UI as a fallback.
/// Runtime UI for the client-side cooking journey.
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

    [Header("Artist Config")]
    [Tooltip("Optional Resources/DishComposerConfig asset. Artists can use it to swap sprites, copy, colors, timing, and layout without touching code.")]
    [SerializeField] private DishComposerConfig uiConfig;

    [Header("Cooking Method Sprites / 烹饪方式图片")]
    [SerializeField] private Sprite panSprite;
    [SerializeField] private Sprite potSprite;
    [SerializeField] private Sprite ovenSprite;

    [Header("Submit / 提交")]
    [Tooltip("Seconds to wait after the card flies off before resetting back to the Spice stage.")]
    [SerializeField] private float resetDelayAfterSubmit = 1.0f;
    [SerializeField] private float swipeDistanceThreshold = 300f;

    private FirebaseSender sender;
    private Canvas hostCanvas;
    private DishComposerConfig _config;
    private DishComposerCanvasSkin _canvasSkin;

    // Selection state
    private Region? _region;
    private Spice? _spice;
    private TravelDistanceBand? _distanceBand;
    private Dish _destinationDish;
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

    private void Awake()
    {
        sender = GetComponent<FirebaseSender>();
        ResolveConfig();
        hostCanvas = FindObjectOfType<Canvas>();
        if (hostCanvas == null && _config.canvasPrefab == null) { Debug.LogError("[DishComposer] No Canvas in scene and no Canvas prefab assigned."); return; }

        if (ActiveMapSprite == null)
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
        if (_mapRT == null || _stagePanelHolder == null) return;
        EnterSpiceStage();
    }

    private void ResolveConfig()
    {
        _config = uiConfig != null ? uiConfig : Resources.Load<DishComposerConfig>("DishComposerConfig");
        if (_config != null) return;

        _config = ScriptableObject.CreateInstance<DishComposerConfig>();
        _config.hideFlags = HideFlags.HideAndDontSave;
        _config.resetDelayAfterSubmit = resetDelayAfterSubmit;
        _config.swipeDistanceThreshold = swipeDistanceThreshold;
    }

    private Sprite ActiveMapSprite => _config != null && _config.mapSprite != null ? _config.mapSprite : mapSprite;
    private Sprite ActiveCaravanSprite => _config != null && _config.caravanSprite != null ? _config.caravanSprite : caravanSprite;
    private int NearMaxMiles => _config != null ? _config.nearMaxMiles : 1500;
    private int MediumMaxMiles => _config != null ? _config.mediumMaxMiles : 3500;

    // ───────────────────── Scaffold ─────────────────────

    private void BuildScaffold()
    {
        if (TryBuildScaffoldFromPrefab())
            return;
        if (hostCanvas == null)
        {
            Debug.LogError("[DishComposer] No Canvas available for runtime UI.");
            return;
        }

        // Background fill
        var bg = NewRect("Background", hostCanvas.transform);
        Stretch(bg);
        var bgImg = bg.AddComponent<Image>();
        // Match the dark teal-navy of the world map's ocean so contain-mode side bars are invisible.
        bgImg.color = _config.backgroundColor;

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
        mapImg.sprite = ActiveMapSprite;
        mapImg.preserveAspect = false; // we size the RT in cover-mode below, no need for built-in fit
        mapImg.color = _config.mapTint;
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

    private bool TryBuildScaffoldFromPrefab()
    {
        if (_config.canvasPrefab == null)
            return false;

        bool prefabHasCanvas = _config.canvasPrefab.GetComponentInChildren<Canvas>(true) != null;
        if (!prefabHasCanvas && hostCanvas == null)
        {
            Debug.LogWarning("[DishComposer] Canvas prefab has no Canvas and there is no scene Canvas; falling back is not possible.");
            return false;
        }

        Transform parent = prefabHasCanvas ? (hostCanvas != null ? hostCanvas.transform.parent : null) : hostCanvas.transform;
        GameObject instance = Instantiate(_config.canvasPrefab, parent, false);
        instance.name = _config.canvasPrefab.name;

        _canvasSkin = instance.GetComponentInChildren<DishComposerCanvasSkin>(true);
        if (_canvasSkin == null)
        {
            Debug.LogWarning("[DishComposer] Canvas prefab is assigned but has no DishComposerCanvasSkin component; falling back to runtime UI.");
            Destroy(instance);
            return false;
        }

        _canvasSkin.ResolveMissingReferences();
        if (_canvasSkin.canvas != null)
            hostCanvas = _canvasSkin.canvas;

        Transform uiRoot = hostCanvas != null ? hostCanvas.transform : instance.transform;

        if (_canvasSkin.backgroundImage != null && _canvasSkin.useConfigScaffoldColors)
            _canvasSkin.backgroundImage.color = _config.backgroundColor;

        _mapHolder = _canvasSkin.mapHolder;
        if (_mapHolder == null)
        {
            var holder = NewRect("MapHolder", uiRoot);
            _mapHolder = holder.GetComponent<RectTransform>();
            Stretch(holder);
        }
        if (_mapHolder.GetComponent<RectMask2D>() == null)
            _mapHolder.gameObject.AddComponent<RectMask2D>();

        _mapRT = _canvasSkin.mapRect;
        if (_mapRT == null)
        {
            var map = NewRect("Map", _mapHolder);
            _mapRT = map.GetComponent<RectTransform>();
            _mapRT.anchorMin = new Vector2(0.5f, 0.5f);
            _mapRT.anchorMax = new Vector2(0.5f, 0.5f);
            _mapRT.pivot = new Vector2(0.5f, 0.5f);
            _mapRT.anchoredPosition = Vector2.zero;
        }

        Image mapImg = _canvasSkin.mapImage != null ? _canvasSkin.mapImage : _mapRT.GetComponent<Image>();
        if (mapImg == null)
            mapImg = _mapRT.gameObject.AddComponent<Image>();
        if (ActiveMapSprite != null)
            mapImg.sprite = ActiveMapSprite;
        mapImg.preserveAspect = false;
        if (_canvasSkin.useConfigScaffoldColors)
            mapImg.color = _config.mapTint;
        FitMapToHolder();

        _overlayLayer = _canvasSkin.overlayLayer;
        if (_overlayLayer == null)
        {
            var overlay = NewRect("Overlay", _mapRT);
            _overlayLayer = overlay.GetComponent<RectTransform>();
            Stretch(overlay);
        }

        _stagePanelHolder = _canvasSkin.stagePanelHolder;
        if (_stagePanelHolder == null)
        {
            var stage = NewRect("StagePanels", uiRoot);
            Stretch(stage);
            _stagePanelHolder = stage.transform;
        }

        _canvasSkin.HideTemplates();
        return true;
    }

    private void FitMapToHolder()
    {
        // Width-fit, vertical overflow: image width matches screen width exactly,
        // top/bottom may overflow and get masked by the holder's RectMask2D. No side bars.
        // 宽度对齐屏幕，垂直方向溢出被裁，左右无黑边。
        Vector2 holderSize = _mapHolder.rect.size;
        Sprite sprite = ActiveMapSprite;
        if (sprite == null)
        {
            var mapImg = _mapRT != null ? _mapRT.GetComponent<Image>() : null;
            sprite = mapImg != null ? mapImg.sprite : null;
        }
        if (sprite == null || holderSize.x <= 0)
        {
            _mapRT.sizeDelta = holderSize;
            return;
        }
        Vector2 spriteSize = sprite.rect.size;
        float scale = holderSize.x / spriteSize.x;
        _mapRT.sizeDelta = spriteSize * scale;
    }

    // Stage 1: Spice

    private void EnterSpiceStage()
    {
        ResetMap();
        var panel = NewStagePanel("SpicePanel");
        AddTitleBar(panel, _config.spiceTitle, _config.spiceSubtitle);

        var row = AddBottomBar(panel, _config.bottomBarHeight);
        foreach (Spice s in Enum.GetValues(typeof(Spice)))
        {
            bool available = DishDatabase.AvailableSpices().Contains(s);
            var btn = MakePill(row, FormatEnum(s), _config.spiceButtonWidth, !available);
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
        _region = null;
        _distanceBand = null;
        _destinationDish = null;
        _cooking = null;
        _resolvedDish = null;

        var origin = SpiceManager.AddSpiceOrigin(s);
        FadeOutCurrentStage();
        ZoomMapToSpiceOrigin(origin, 0.9f, () => EnterDistanceStage());
    }

    private void EnterDistanceStage()
    {
        var panel = NewStagePanel("DistancePanel");
        AddTitleBar(
            panel,
            FormatCopy(_config.distanceTitle, spice: FormatEnum(_spice.Value)),
            FormatCopy(_config.distanceSubtitle, spice: FormatEnum(_spice.Value)));

        var row = AddBottomBar(panel, _config.bottomBarHeight);
        foreach (TravelDistanceBand band in Enum.GetValues(typeof(TravelDistanceBand)))
        {
            bool available = DishDatabase.AvailableTravelBandsForSpice(_spice.Value, NearMaxMiles, MediumMaxMiles).Contains(band);
            var btn = MakePill(row, FormatDistanceBand(band), _config.distanceButtonWidth, !available);
            if (available)
            {
                var captured = band;
                btn.onClick.AddListener(() => OnPickDistance(captured));
            }
        }
    }

    private void OnPickDistance(TravelDistanceBand band)
    {
        _distanceBand = band;
        var candidates = DishDatabase.DestinationCandidates(_spice.Value, band, NearMaxMiles, MediumMaxMiles);
        if (candidates.Count == 0)
        {
            Debug.LogWarning($"[DishComposer] No destination for {_spice.Value} at {band} distance.");
            return;
        }

        _destinationDish = candidates[UnityEngine.Random.Range(0, candidates.Count)];
        _region = _destinationDish.Region;
        var origin = SpiceManager.AddSpiceOrigin(_spice.Value);

        FadeOutCurrentStage();
        ZoomMapToWorld(0.9f, () =>
        {
            PlayCaravanFromOriginToRegion(
                origin,
                _region.Value,
                2.0f,
                () => EnterCookingStage(),
                _destinationDish.CountryOfOrigin);
        });
    }

    // ───────────────────── Stage 3: Cooking ─────────────────────

    private GameObject _cookingTilesRow;

    private void EnterCookingStage()
    {
        var panel = NewStagePanel("CookingPanel");
        string destination = _destinationDish != null ? _destinationDish.CountryOfOrigin : "your destination";
        AddTitleBar(
            panel,
            FormatCopy(_config.cookingTitle, destination: destination),
            FormatCopy(_config.cookingSubtitle, destination: destination));

        // Container for the 3 tiles (no layout group — we position by index for reliable hit areas).
        // 3 张 tile 容器；不用 layout group，按 index 手控位置避免触摸热区错位。
        var row = NewRect("CookingTiles", panel.transform);
        var rRT = row.GetComponent<RectTransform>();
        rRT.anchorMin = new Vector2(0.5f, 0.5f); rRT.anchorMax = new Vector2(0.5f, 0.5f);
        rRT.pivot = new Vector2(0.5f, 0.5f); rRT.anchoredPosition = new Vector2(0, 0);
        rRT.sizeDelta = _config.cookingTilesContainerSize;
        _cookingTilesRow = row;

        var values = Enum.GetValues(typeof(CookingMethod));
        float spacing = _config.cookingTileSpacing;
        int total = values.Length;
        float startX = -spacing * (total - 1) / 2f;

        int i = 0;
        foreach (CookingMethod c in values)
        {
            bool available = _destinationDish != null && _distanceBand.HasValue
                ? DishDatabase.AvailableCookingForDestination(
                        _spice.Value, _distanceBand.Value, _destinationDish.CountryOfOrigin, NearMaxMiles, MediumMaxMiles)
                    .Contains(c)
                : DishDatabase.AvailableCookingForRegionSpice(_region.Value, _spice.Value).Contains(c);
            float x = startX + i * spacing;
            BuildCookingTile(row, c, available, x);
            i++;
        }
    }

    private void BuildCookingTile(GameObject parent, CookingMethod c, bool available, float xOffset)
    {
        if (TryBuildCookingTileFromPrefab(parent, c, available, xOffset))
            return;

        var tile = NewRect(c + "Tile", parent.transform);
        var tRT = tile.GetComponent<RectTransform>();
        tRT.anchorMin = new Vector2(0.5f, 0.5f); tRT.anchorMax = new Vector2(0.5f, 0.5f);
        tRT.pivot = new Vector2(0.5f, 0.5f);
        tRT.anchoredPosition = new Vector2(xOffset, 0);
        tRT.sizeDelta = _config.cookingTileSize;

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
        img.color = available ? _config.mainTextColor : WithAlpha(_config.mainTextColor, 0.3f);
        var iRT = imgGO.GetComponent<RectTransform>();
        iRT.anchorMin = new Vector2(0.5f, 1f); iRT.anchorMax = new Vector2(0.5f, 1f);
        iRT.pivot = new Vector2(0.5f, 1f);
        iRT.anchoredPosition = new Vector2(0, _config.cookingTileImageTopOffset);
        iRT.sizeDelta = _config.cookingTileImageSize;

        // Label below
        var label = NewText("Label", tile, FormatEnum(c), _config.cookingTileLabelFontSize, FontStyles.Bold,
            available ? _config.mainTextColor : WithAlpha(_config.mainTextColor, 0.4f), TextAlignmentOptions.Center);
        var lRT = label.GetComponent<RectTransform>();
        lRT.anchorMin = new Vector2(0.5f, 0f); lRT.anchorMax = new Vector2(0.5f, 0f);
        lRT.pivot = new Vector2(0.5f, 0f); lRT.anchoredPosition = new Vector2(0, _config.cookingTileLabelBottomOffset);
        lRT.sizeDelta = new Vector2(_config.cookingTileImageSize.x, _config.cookingTileLabelHeight);
        label.GetComponent<TextMeshProUGUI>().raycastTarget = false;

        if (available)
        {
            var captured = c;
            btn.onClick.AddListener(() => OnPickCooking(captured));
        }
    }

    private bool TryBuildCookingTileFromPrefab(GameObject parent, CookingMethod c, bool available, float xOffset)
    {
        var template = _canvasSkin != null ? _canvasSkin.cookingTileTemplate : null;
        if (template == null)
            return false;

        var view = Instantiate(template, parent.transform, false);
        view.gameObject.name = c + "Tile";
        view.gameObject.SetActive(true);

        var tRT = view.GetComponent<RectTransform>();
        tRT.anchorMin = new Vector2(0.5f, 0.5f);
        tRT.anchorMax = new Vector2(0.5f, 0.5f);
        tRT.pivot = new Vector2(0.5f, 0.5f);
        tRT.anchoredPosition = new Vector2(xOffset, 0);
        if (view.resizeFromConfig)
            tRT.sizeDelta = _config.cookingTileSize;

        Button btn = view.button != null ? view.button : view.GetComponent<Button>() ?? view.GetComponentInChildren<Button>(true);
        if (btn == null)
        {
            Graphic graphic = view.GetComponent<Graphic>();
            if (graphic == null)
            {
                var img = view.gameObject.AddComponent<Image>();
                img.color = new Color(0, 0, 0, 0);
                graphic = img;
            }
            btn = view.gameObject.AddComponent<Button>();
            btn.targetGraphic = graphic;
        }

        btn.interactable = available;
        if (view.iconImage != null)
        {
            view.iconImage.sprite = GetCookingSprite(c);
            view.iconImage.raycastTarget = false;
        }
        if (view.labelText != null)
        {
            view.labelText.text = FormatEnum(c);
            view.labelText.raycastTarget = false;
        }
        if (view.unavailableGroup != null)
            view.unavailableGroup.alpha = available ? 1f : 0.35f;

        if (available)
        {
            var captured = c;
            btn.onClick.AddListener(() => OnPickCooking(captured));
        }

        return true;
    }

    private Sprite GetCookingSprite(CookingMethod c) => c switch
    {
        CookingMethod.Pan  => _config.panSprite != null ? _config.panSprite : panSprite,
        CookingMethod.Pot  => _config.potSprite != null ? _config.potSprite : potSprite,
        CookingMethod.Oven => _config.ovenSprite != null ? _config.ovenSprite : ovenSprite,
        _ => null
    };

    private void OnPickCooking(CookingMethod c)
    {
        _cooking = c;
        _resolvedDish = _destinationDish != null && _distanceBand.HasValue
            ? DishDatabase.FindForDestination(
                _spice.Value, _distanceBand.Value, _destinationDish.CountryOfOrigin, _cooking.Value, NearMaxMiles, MediumMaxMiles)
            : DishDatabase.Find(_region.Value, _spice.Value, _cooking.Value);

        // Fade out the row of tiles
        if (_cookingTilesRow != null)
        {
            var cg = _cookingTilesRow.GetComponent<CanvasGroup>() ?? _cookingTilesRow.AddComponent<CanvasGroup>();
            cg.DOFade(0f, 0.3f);
        }

        // Build a new centerpiece on the same panel: big sprite + Cook button below.
        // 在同一个 panel 上构建新中心组：选中图（大）+ Cook 按钮在图下面。
        if (TryBuildCookingCenterpieceFromPrefab(c))
            return;

        var center = NewRect("CookingCenterpiece", _currentStagePanel.transform);
        var crt = center.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(0.5f, 0.5f); crt.anchorMax = new Vector2(0.5f, 0.5f);
        crt.pivot = new Vector2(0.5f, 0.5f); crt.anchoredPosition = _config.cookingCenterpiecePosition;
        crt.sizeDelta = _config.cookingCenterpieceSize;

        // Big cooking image
        var imgGO = NewRect("Image", center.transform);
        var img = imgGO.AddComponent<Image>();
        img.sprite = GetCookingSprite(c);
        img.preserveAspect = true;
        img.raycastTarget = false;
        var iRT = imgGO.GetComponent<RectTransform>();
        iRT.anchorMin = new Vector2(0.5f, 1f); iRT.anchorMax = new Vector2(0.5f, 1f);
        iRT.pivot = new Vector2(0.5f, 1f); iRT.anchoredPosition = new Vector2(0, 0);
        iRT.sizeDelta = _config.cookingCenterImageSize;

        // Method label
        var lab = NewText("Label", center, FormatEnum(c), _config.cookingCenterLabelFontSize, FontStyles.Bold,
            _config.mainTextColor, TextAlignmentOptions.Center);
        var lRT = lab.GetComponent<RectTransform>();
        lRT.anchorMin = new Vector2(0.5f, 1f); lRT.anchorMax = new Vector2(0.5f, 1f);
        lRT.pivot = new Vector2(0.5f, 1f); lRT.anchoredPosition = new Vector2(0, _config.cookingCenterLabelTopOffset);
        lRT.sizeDelta = new Vector2(_config.cookingCenterpieceSize.x, _config.cookingCenterLabelHeight);

        // Cook button below the image+label
        var btnGO = NewRect("CookBtn", center.transform);
        var bRT = btnGO.GetComponent<RectTransform>();
        bRT.anchorMin = new Vector2(0.5f, 1f); bRT.anchorMax = new Vector2(0.5f, 1f);
        bRT.pivot = new Vector2(0.5f, 1f); bRT.anchoredPosition = new Vector2(0, _config.cookButtonTopOffset);
        bRT.sizeDelta = _config.cookButtonSize;
        var bg = btnGO.AddComponent<Image>();
        bg.color = _config.cookButtonColor;
        ApplySprite(bg, _config.cookButtonSprite, true);
        var btn = btnGO.AddComponent<Button>();
        btn.targetGraphic = bg;
        var blab = NewText("Label", btnGO, _config.cookButtonLabel, _config.cookButtonFontSize, FontStyles.Bold, _config.mainTextColor, TextAlignmentOptions.Center);
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
    private bool TryBuildCookingCenterpieceFromPrefab(CookingMethod c)
    {
        var template = _canvasSkin != null ? _canvasSkin.cookingCenterpieceTemplate : null;
        if (template == null)
            return false;

        var view = Instantiate(template, _currentStagePanel.transform, false);
        view.gameObject.name = "CookingCenterpiece";
        view.gameObject.SetActive(true);

        var crt = view.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(0.5f, 0.5f);
        crt.anchorMax = new Vector2(0.5f, 0.5f);
        crt.pivot = new Vector2(0.5f, 0.5f);
        if (view.resizeFromConfig)
        {
            crt.anchoredPosition = _config.cookingCenterpiecePosition;
            crt.sizeDelta = _config.cookingCenterpieceSize;
        }

        if (view.iconImage != null)
        {
            view.iconImage.sprite = GetCookingSprite(c);
            view.iconImage.raycastTarget = false;
        }
        if (view.methodLabel != null)
        {
            view.methodLabel.text = FormatEnum(c);
            view.methodLabel.raycastTarget = false;
        }

        Button btn = view.cookButton != null ? view.cookButton : view.GetComponentInChildren<Button>(true);
        if (btn == null)
        {
            Debug.LogWarning("[DishComposer] Cooking centerpiece template has no Button; falling back to runtime centerpiece.");
            Destroy(view.gameObject);
            return false;
        }

        if (view.cookButtonLabel != null)
        {
            view.cookButtonLabel.text = _config.cookButtonLabel;
            view.cookButtonLabel.raycastTarget = false;
        }

        view.transform.localScale = Vector3.one * 0.6f;
        var ccg = view.GetComponent<CanvasGroup>() ?? view.gameObject.AddComponent<CanvasGroup>();
        ccg.alpha = 0;
        ccg.DOFade(1f, 0.4f).SetDelay(0.15f);
        view.transform.DOScale(1f, 0.4f).SetEase(Ease.OutBack).SetDelay(0.15f);

        btn.onClick.AddListener(() =>
        {
            FadeOutCurrentStage();
            EnterDishRevealStage();
        });

        return true;
    }

    private void EnterDishRevealStage()
    {
        var panel = NewStagePanel("DishRevealPanel");
        if (TryShowDishRevealFromPrefab(panel))
            return;

        var dim = NewRect("Dim", panel.transform);
        Stretch(dim);
        var dimImg = dim.AddComponent<Image>();
        dimImg.color = _config.revealDimColor;

        ShowDishCard(panel);
    }

    private bool TryShowDishRevealFromPrefab(GameObject panel)
    {
        var template = _canvasSkin != null ? _canvasSkin.dishRevealTemplate : null;
        if (template == null)
            return false;

        var view = Instantiate(template, panel.transform, false);
        view.gameObject.name = "DishReveal";
        view.gameObject.SetActive(true);

        if (view.dimImage != null && view.dimImage.sprite == null)
            view.dimImage.color = _config.revealDimColor;

        if (view.chefLabel != null)
        {
            view.chefLabel.text = _config.chefLabel;
            view.chefLabel.raycastTarget = false;
        }

        if (view.chefInput != null)
            ConfigureChefInput(view.chefInput);
        else if (view.chefInputRow != null)
            BuildChefInput(view.chefInputRow.gameObject);

        if (view.dishImage != null && _resolvedDish != null && _resolvedDish.DishSprite != null)
        {
            view.dishImage.sprite = _resolvedDish.DishSprite;
            view.dishImage.preserveAspect = true;
            view.dishImage.raycastTarget = false;
        }

        string dishName = _resolvedDish != null ? _resolvedDish.DishName : "Mystery Dish";
        string country = _resolvedDish != null ? _resolvedDish.CountryOfOrigin : "Unknown";
        if (view.dishNameText != null)
        {
            view.dishNameText.text = dishName;
            view.dishNameText.raycastTarget = false;
        }
        if (view.dishOriginText != null)
        {
            view.dishOriginText.text = FormatCopy(_config.dishOriginFormat, country: country);
            view.dishOriginText.raycastTarget = false;
        }
        if (view.swipeHintText != null)
        {
            view.swipeHintText.text = _config.swipeHint;
            view.swipeHintText.raycastTarget = false;
            if (view.pulseHint)
                view.swipeHintText.DOFade(0.3f, 0.8f).SetLoops(-1, LoopType.Yoyo);
        }

        RectTransform card = view.dishCard != null ? view.dishCard : view.GetComponent<RectTransform>();
        if (card != null)
        {
            EnsureRaycastTarget(card.gameObject);
            var drag = card.GetComponent<DishCardDragger>() ?? card.gameObject.AddComponent<DishCardDragger>();
            drag.Init(this, _config.swipeDistanceThreshold, _config.cardFlyOffDistance);

            if (view.animateCardIn)
            {
                card.localScale = Vector3.zero;
                card.DOScale(1f, 0.5f).SetEase(Ease.OutBack);
            }
        }

        return true;
    }

    private void ShowDishCard(GameObject panel)
    {
        // Chef name input (top)
        var inputRow = NewRect("ChefInputRow", panel.transform);
        var irt = inputRow.GetComponent<RectTransform>();
        irt.anchorMin = new Vector2(0.5f, 1); irt.anchorMax = new Vector2(0.5f, 1);
        irt.pivot = new Vector2(0.5f, 1); irt.anchoredPosition = _config.chefInputRowPosition;
        irt.sizeDelta = _config.chefInputRowSize;
        BuildChefInput(inputRow);

        // Reveal container — no background frame; the dim panel is already behind everything.
        // Holds the dish image (large), name + country (under image), hint (bottom), and is the drag target.
        // 揭示容器：没有自己的背景（dim 在更后面已经盖了），大图 + 文字 + 提示，整块作为拖拽接收。
        var card = NewRect("DishPlate", panel.transform);
        var crt = card.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(0.5f, 0.5f); crt.anchorMax = new Vector2(0.5f, 0.5f);
        crt.pivot = new Vector2(0.5f, 0.5f); crt.anchoredPosition = Vector2.zero;
        crt.sizeDelta = _config.dishCardSize;

        // Invisible raycast catcher so the entire card area accepts drags (incl. empty space around the image).
        // 透明捕获 Image，让整块区域都能接收拖拽。
        var rayCatcher = card.AddComponent<Image>();
        rayCatcher.color = _config.dishCardSprite != null ? Color.white : new Color(0, 0, 0, 0);
        ApplySprite(rayCatcher, _config.dishCardSprite, true);
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
            iRT.anchoredPosition = new Vector2(0, _config.revealDishImageTopOffset);
            iRT.sizeDelta = _config.revealDishImageSize;
        }

        // Dish name (under image)
        string dishName = _resolvedDish != null ? _resolvedDish.DishName : "Mystery Dish";
        var nameGO = NewText("Name", card, dishName, _config.dishNameFontSize, FontStyles.Bold,
            _config.titleColor, TextAlignmentOptions.Center);
        var nRT = nameGO.GetComponent<RectTransform>();
        nRT.anchorMin = new Vector2(0.5f, 1f); nRT.anchorMax = new Vector2(0.5f, 1f);
        nRT.pivot = new Vector2(0.5f, 1f);
        nRT.anchoredPosition = new Vector2(0, _config.dishNameTopOffset);
        nRT.sizeDelta = new Vector2(_config.dishCardSize.x, 60);
        nameGO.GetComponent<TextMeshProUGUI>().raycastTarget = false;

        // Country (under name)
        string country = _resolvedDish != null ? _resolvedDish.CountryOfOrigin : "Unknown";
        var originGO = NewText("Origin", card, FormatCopy(_config.dishOriginFormat, country: country), _config.countryFontSize, FontStyles.Italic,
            _config.subtitleColor, TextAlignmentOptions.Center);
        var oRT = originGO.GetComponent<RectTransform>();
        oRT.anchorMin = new Vector2(0.5f, 1f); oRT.anchorMax = new Vector2(0.5f, 1f);
        oRT.pivot = new Vector2(0.5f, 1f);
        oRT.anchoredPosition = new Vector2(0, _config.countryTopOffset);
        oRT.sizeDelta = new Vector2(_config.dishCardSize.x, 36);
        originGO.GetComponent<TextMeshProUGUI>().raycastTarget = false;

        // Hint (bottom, pulsing)
        var hintGO = NewText("Hint", card, _config.swipeHint, _config.hintFontSize, FontStyles.Bold,
            _config.hintColor, TextAlignmentOptions.Center);
        var hRT = hintGO.GetComponent<RectTransform>();
        hRT.anchorMin = new Vector2(0.5f, 0f); hRT.anchorMax = new Vector2(0.5f, 0f);
        hRT.pivot = new Vector2(0.5f, 0f);
        hRT.anchoredPosition = new Vector2(0, _config.hintBottomOffset);
        hRT.sizeDelta = new Vector2(_config.dishCardSize.x, 40);
        var hintTMP = hintGO.GetComponent<TextMeshProUGUI>();
        hintTMP.text = _config.swipeHint;
        hintTMP.color = _config.hintColor;
        hintTMP.raycastTarget = false;
        hintTMP.DOFade(0.3f, 0.8f).SetLoops(-1, LoopType.Yoyo);

        // Drag handler on the card itself
        var drag = card.AddComponent<DishCardDragger>();
        drag.Init(this, _config.swipeDistanceThreshold, _config.cardFlyOffDistance);

        card.transform.localScale = Vector3.zero;
        card.transform.DOScale(1f, 0.5f).SetEase(Ease.OutBack);
    }

    private void BuildChefInput(GameObject row)
    {
        var rowBg = row.AddComponent<Image>();
        rowBg.color = _config.panelColor;
        ApplySprite(rowBg, _config.panelSprite, true);

        var hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 12; hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.padding = new RectOffset(20, 20, 8, 8);
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;

        var label = NewText("Label", row, _config.chefLabel, _config.chefLabelFontSize, FontStyles.Normal, _config.mainTextColor, TextAlignmentOptions.MidlineRight);
        label.AddComponent<LayoutElement>().preferredWidth = _config.chefLabelWidth;

        var inputGO = NewRect("Input", row);
        var inputBg = inputGO.AddComponent<Image>();
        inputBg.color = _config.optionColor;
        ApplySprite(inputBg, _config.optionButtonSprite, true);
        inputGO.AddComponent<LayoutElement>().preferredWidth = _config.chefInputWidth;

        var ta = NewRect("TextArea", inputGO);
        var taRT = ta.GetComponent<RectTransform>();
        taRT.anchorMin = Vector2.zero; taRT.anchorMax = Vector2.one;
        taRT.offsetMin = new Vector2(12, 6); taRT.offsetMax = new Vector2(-12, -6);
        ta.AddComponent<RectMask2D>();

        var placeholder = string.IsNullOrEmpty(_config.chefPlaceholder) ? ChefNameGenerator.Generate() : _config.chefPlaceholder;
        var ph = NewText("Placeholder", ta, placeholder, _config.chefTextFontSize, FontStyles.Italic,
            new Color(0.6f, 0.6f, 0.7f, 0.8f), TextAlignmentOptions.MidlineLeft);
        Stretch(ph);

        var txt = NewText("Text", ta, "", _config.chefTextFontSize, FontStyles.Normal, _config.mainTextColor, TextAlignmentOptions.MidlineLeft);
        Stretch(txt);

        _chefInput = inputGO.AddComponent<TMP_InputField>();
        _chefInput.textViewport = taRT;
        _chefInput.textComponent = txt.GetComponent<TMP_Text>();
        _chefInput.placeholder = ph.GetComponent<TMP_Text>();
        _chefInput.characterLimit = ChefNameGenerator.MaxLength;
        _chefInput.text = "";
    }

    // ───────────────────── Stage 5: Slide submit (called by DishCardDragger) ─────────────────────

    private void ConfigureChefInput(TMP_InputField input)
    {
        _chefInput = input;
        if (_chefInput == null) return;

        _chefInput.characterLimit = ChefNameGenerator.MaxLength;
        _chefInput.text = "";

        var placeholder = string.IsNullOrEmpty(_config.chefPlaceholder) ? ChefNameGenerator.Generate() : _config.chefPlaceholder;
        if (_chefInput.placeholder is TMP_Text placeholderText)
        {
            placeholderText.text = placeholder;
            placeholderText.raycastTarget = false;
            UIFont.Apply(placeholderText);
        }
        if (_chefInput.textComponent != null)
        {
            _chefInput.textComponent.raycastTarget = false;
            UIFont.Apply(_chefInput.textComponent);
        }
    }

    private static void EnsureRaycastTarget(GameObject go)
    {
        if (go == null) return;

        Graphic graphic = go.GetComponent<Graphic>();
        if (graphic == null)
        {
            var image = go.AddComponent<Image>();
            image.color = new Color(0, 0, 0, 0);
            graphic = image;
        }
        graphic.raycastTarget = true;
    }

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

        DOVirtual.DelayedCall(_config.resetDelayAfterSubmit, ResetForNextRound).SetLink(gameObject);
    }

    private void ResetForNextRound()
    {
        _region = null;
        _spice = null;
        _distanceBand = null;
        _destinationDish = null;
        _cooking = null;
        _resolvedDish = null;
        FadeOutCurrentStage();
        EnterSpiceStage();
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

    private void ZoomMapToSpiceOrigin(SpiceOrigin origin, float duration, Action onDone)
    {
        Vector2 norm = new Vector2(0.5f, 0.5f);
        float zoom = mapCoords != null ? mapCoords.defaultRegionZoom : 2.5f;
        if (mapCoords != null) mapCoords.TryGetSpiceOrigin(origin, out norm);

        Vector2 size = _mapRT.rect.size;
        Vector2 offset = new Vector2(
            (0.5f - norm.x) * size.x * zoom,
            (0.5f - norm.y) * size.y * zoom);

        var seq = DOTween.Sequence();
        seq.Append(_mapRT.DOScale(zoom, duration).SetEase(Ease.InOutCubic));
        seq.Join(_mapRT.DOAnchorPos(offset, duration).SetEase(Ease.InOutCubic));
        seq.OnComplete(() => onDone?.Invoke());
    }

    private void PlayCaravanFromOriginToRegion(
        SpiceOrigin origin, Region dest, float duration, Action onDone, string destinationLabel = null)
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
        AddMarker(originPx, _config.originMarkerColor, origin.ToString());
        AddMarker(destPx, _config.destinationMarkerColor, destinationLabel ?? dest.ToString());

        // Draw the route progressively: each segment between samples fades in staggered.
        // 路径逐段淡入，形成"被画出来"的感觉。
        float drawWindow = Mathf.Max(0.2f, duration * 0.6f);
        float perSegmentDelay = samples.Count > 1 ? drawWindow / (samples.Count - 1) : 0f;
        for (int i = 1; i < samples.Count; i++)
        {
            var img = AddRouteSegment(samples[i - 1], samples[i]);
            img.color = WithAlpha(_config.routeColor, 0f);
            img.DOFade(_config.routeColor.a, 0.18f).SetDelay((i - 1) * perSegmentDelay);
        }

        // Caravan
        var car = NewRect("Caravan", _overlayLayer);
        var cImg = car.AddComponent<Image>();
        cImg.sprite = ActiveCaravanSprite;
        cImg.color = ActiveCaravanSprite != null ? Color.white : _config.originMarkerColor;
        cImg.preserveAspect = true;
        var crt = car.GetComponent<RectTransform>();
        crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(0.5f, 0.5f);
        crt.sizeDelta = _config.caravanSize;
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
        ApplySprite(img, _config.routeSegmentSprite, false);
        var rt = seg.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        Vector2 dir = b - a;
        float len = dir.magnitude;
        rt.sizeDelta = new Vector2(len, _config.routeLineWidth);
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
        ApplySprite(img, _config.markerSprite, false);
        img.preserveAspect = _config.markerSprite != null;
        var rt = dot.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = _config.markerSize;
        rt.anchoredPosition = anchoredPos;
        dot.transform.localScale = Vector3.zero;
        dot.transform.DOScale(1f, 0.3f).SetEase(Ease.OutBack);

        if (!string.IsNullOrEmpty(label))
        {
            var labelGO = NewText("MarkerLabel", _overlayLayer, label, _config.markerLabelFontSize, FontStyles.Bold,
                _config.mainTextColor, TextAlignmentOptions.Center);
            var labelRT = labelGO.GetComponent<RectTransform>();
            labelRT.anchorMin = labelRT.anchorMax = labelRT.pivot = new Vector2(0.5f, 0.5f);
            labelRT.sizeDelta = _config.markerLabelSize;
            labelRT.anchoredPosition = anchoredPos + _config.markerLabelOffset;
            var tmp = labelGO.GetComponent<TextMeshProUGUI>();
            tmp.raycastTarget = false;
            tmp.outlineColor = new Color(0, 0, 0, 0.85f);
            tmp.outlineWidth = 0.2f;
            tmp.alpha = 0f;
            tmp.DOFade(1f, 0.25f).SetDelay(0.1f);
        }
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
        if (TryAddTitleBarFromPrefab(panel, title, subtitle))
            return;

        var bar = NewRect("TitleBar", panel.transform);
        var rt = bar.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(0.5f, 1); rt.anchoredPosition = new Vector2(0, -_config.titleBarInset.y);
        rt.sizeDelta = new Vector2(-_config.titleBarInset.x, _config.titleBarHeight);
        var bg = bar.AddComponent<Image>();
        bg.color = _config.panelColor;
        ApplySprite(bg, _config.panelSprite, true);

        var vlg = bar.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(20, 20, 12, 12); vlg.spacing = 4;
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;

        var t = NewText("Title", bar, title, _config.titleFontSize, FontStyles.Bold,
            _config.titleColor, TextAlignmentOptions.Center);
        t.AddComponent<LayoutElement>().preferredHeight = 50;

        var s = NewText("Sub", bar, subtitle, _config.subtitleFontSize, FontStyles.Italic,
            _config.subtitleColor, TextAlignmentOptions.Center);
        s.AddComponent<LayoutElement>().preferredHeight = 32;
    }

    private bool TryAddTitleBarFromPrefab(GameObject panel, string title, string subtitle)
    {
        var template = _canvasSkin != null ? _canvasSkin.titleBarTemplate : null;
        if (template == null)
            return false;

        var view = Instantiate(template, panel.transform, false);
        view.gameObject.name = "TitleBar";
        view.gameObject.SetActive(true);

        if (view.titleText != null)
        {
            view.titleText.text = title;
            view.titleText.raycastTarget = false;
        }
        if (view.subtitleText != null)
        {
            view.subtitleText.text = subtitle;
            view.subtitleText.raycastTarget = false;
        }

        return true;
    }

    private GameObject AddBottomBar(GameObject panel, float height)
    {
        if (TryAddBottomBarFromPrefab(panel, out var container))
            return container;

        var bar = NewRect("BottomBar", panel.transform);
        var rt = bar.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(1, 0);
        rt.pivot = new Vector2(0.5f, 0); rt.anchoredPosition = new Vector2(0, _config.bottomBarBottomOffset);
        rt.sizeDelta = new Vector2(-_config.bottomBarSideInset, height);
        var bg = bar.AddComponent<Image>();
        bg.color = _config.panelColor;
        ApplySprite(bg, _config.panelSprite, true);

        var hlg = bar.AddComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(20, 20, 20, 20); hlg.spacing = 14;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = false; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
        return bar;
    }

    private bool TryAddBottomBarFromPrefab(GameObject panel, out GameObject optionContainer)
    {
        optionContainer = null;
        var template = _canvasSkin != null ? _canvasSkin.bottomBarTemplate : null;
        if (template == null)
            return false;

        var view = Instantiate(template, panel.transform, false);
        view.gameObject.name = "BottomBar";
        view.gameObject.SetActive(true);
        optionContainer = view.optionContainer != null ? view.optionContainer.gameObject : view.gameObject;
        return true;
    }

    private Button MakePill(GameObject parent, string label, float width, bool disabled)
    {
        if (TryMakePillFromPrefab(parent, label, width, disabled, out var prefabButton))
            return prefabButton;

        var go = NewRect(label + "Btn", parent.transform);
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(width, _config.optionButtonSize.y);

        var img = go.AddComponent<Image>();
        img.color = disabled ? _config.disabledOptionColor : _config.optionColor;
        var buttonSprite = disabled
            ? (_config.disabledOptionButtonSprite != null ? _config.disabledOptionButtonSprite : _config.optionButtonSprite)
            : _config.optionButtonSprite;
        ApplySprite(img, buttonSprite, true);

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.interactable = !disabled;

        var le = go.AddComponent<LayoutElement>();
        le.preferredWidth = width; le.preferredHeight = _config.optionButtonSize.y;

        var text = NewText("Label", go, label, _config.optionFontSize, FontStyles.Bold, _config.mainTextColor, TextAlignmentOptions.Center);
        Stretch(text);

        return btn;
    }

    private bool TryMakePillFromPrefab(GameObject parent, string label, float width, bool disabled, out Button button)
    {
        button = null;
        var template = _canvasSkin != null ? _canvasSkin.optionButtonTemplate : null;
        if (template == null)
            return false;

        var view = Instantiate(template, parent.transform, false);
        view.gameObject.name = label + "Btn";
        view.gameObject.SetActive(true);

        button = view.button != null ? view.button : view.GetComponent<Button>() ?? view.GetComponentInChildren<Button>(true);
        if (button == null)
        {
            Graphic graphic = view.GetComponent<Graphic>();
            if (graphic == null)
            {
                var image = view.gameObject.AddComponent<Image>();
                image.color = new Color(0, 0, 0, 0);
                graphic = image;
            }
            button = view.gameObject.AddComponent<Button>();
            button.targetGraphic = graphic;
        }
        button.interactable = !disabled;

        if (view.resizeToRequestedWidth)
        {
            var rt = view.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(width, _config.optionButtonSize.y);
            var le = view.layoutElement != null ? view.layoutElement : view.GetComponent<LayoutElement>();
            if (le != null)
            {
                le.preferredWidth = width;
                le.preferredHeight = _config.optionButtonSize.y;
            }
        }

        if (view.labelText != null)
        {
            view.labelText.text = label;
            view.labelText.raycastTarget = false;
        }

        return true;
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

    private string FormatDistanceBand(TravelDistanceBand band) => _config.LabelFor(band);

    private static string FormatCopy(string template, string spice = null, string destination = null, string country = null)
    {
        if (string.IsNullOrEmpty(template)) return string.Empty;

        return template
            .Replace("{spice}", spice ?? string.Empty)
            .Replace("{destination}", destination ?? string.Empty)
            .Replace("{country}", country ?? string.Empty);
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }

    private static void ApplySprite(Image image, Sprite sprite, bool sliced)
    {
        if (image == null || sprite == null) return;

        image.sprite = sprite;
        image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
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
    private float flyOffDistance = 1500f;
    private RectTransform rt;
    private Vector2 startPos;
    private bool committed;

    public void Init(DishComposer owner, float swipeThreshold, float cardFlyOffDistance)
    {
        this.owner = owner;
        this.threshold = swipeThreshold;
        this.flyOffDistance = cardFlyOffDistance;
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
            rt.DOAnchorPos(rt.anchoredPosition + new Vector2(0, flyOffDistance), 0.5f).SetEase(Ease.InCubic);
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
