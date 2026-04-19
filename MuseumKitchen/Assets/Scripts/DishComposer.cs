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
        bgImg.color = new Color(0.04f, 0.05f, 0.10f, 1f);

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
        mapImg.preserveAspect = true;
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
        // Make the map fill the holder so zoom 1 = world view.
        _mapRT.sizeDelta = _mapHolder.rect.size;
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

    private void EnterCookingStage()
    {
        var panel = NewStagePanel("CookingPanel");
        AddTitleBar(panel, "How do you cook it?", "Pick a method.");

        var row = AddBottomBar(panel, 180f);
        foreach (CookingMethod c in Enum.GetValues(typeof(CookingMethod)))
        {
            bool available = DishDatabase
                .AvailableCookingForRegionSpice(_region.Value, _spice.Value)
                .Contains(c);
            var btn = MakePill(row, CookingEmoji(c) + "  " + FormatEnum(c), 240, !available);
            if (available)
            {
                var captured = c;
                btn.onClick.AddListener(() => OnPickCooking(captured));
            }
        }
    }

    private void OnPickCooking(CookingMethod c)
    {
        _cooking = c;
        _resolvedDish = DishDatabase.Find(_region.Value, _spice.Value, _cooking.Value);

        // Quick punch animation: a giant emoji in the center
        var burst = NewRect("CookingBurst", hostCanvas.transform);
        var brt = burst.GetComponent<RectTransform>();
        brt.anchorMin = new Vector2(0.5f, 0.5f); brt.anchorMax = new Vector2(0.5f, 0.5f);
        brt.pivot = new Vector2(0.5f, 0.5f); brt.anchoredPosition = Vector2.zero;
        brt.sizeDelta = new Vector2(400, 400);
        var t = burst.AddComponent<TextMeshProUGUI>();
        t.text = CookingEmoji(c);
        t.fontSize = 240; t.alignment = TextAlignmentOptions.Center;
        burst.transform.localScale = Vector3.zero;
        burst.transform.DOScale(1f, 0.4f).SetEase(Ease.OutBack);

        FadeOutCurrentStage();
        DOVirtual.DelayedCall(0.7f, () =>
        {
            burst.transform.DOScale(0f, 0.25f).OnComplete(() => Destroy(burst));
            EnterCookRevealStage();
        });
    }

    // ───────────────────── Stage 4: Cook Reveal ─────────────────────

    private void EnterCookRevealStage()
    {
        var panel = NewStagePanel("CookRevealPanel");

        // Dim the map a bit
        var dim = NewRect("Dim", panel.transform);
        Stretch(dim);
        var dimImg = dim.AddComponent<Image>();
        dimImg.color = new Color(0, 0, 0, 0.55f);

        // Big Cook! button center
        var btnGO = NewRect("CookBigButton", panel.transform);
        var brt = btnGO.GetComponent<RectTransform>();
        brt.anchorMin = new Vector2(0.5f, 0.5f); brt.anchorMax = new Vector2(0.5f, 0.5f);
        brt.pivot = new Vector2(0.5f, 0.5f); brt.anchoredPosition = Vector2.zero;
        brt.sizeDelta = new Vector2(520, 200);
        var btnImg = btnGO.AddComponent<Image>();
        btnImg.color = new Color(0.85f, 0.35f, 0.2f, 1f);
        var btn = btnGO.AddComponent<Button>();
        btn.targetGraphic = btnImg;
        var lab = NewText("Label", btnGO, "Cook!", 80, FontStyles.Bold, Color.white, TextAlignmentOptions.Center);
        Stretch(lab);

        btnGO.transform.localScale = Vector3.one * 0.6f;
        btnGO.transform.DOScale(1f, 0.4f).SetEase(Ease.OutBack);

        btn.onClick.AddListener(() =>
        {
            btnGO.transform.DOScale(0f, 0.25f).OnComplete(() => Destroy(btnGO));
            ShowDishCard(panel);
        });
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

        // The dish "plate" — draggable card
        var card = NewRect("DishPlate", panel.transform);
        var crt = card.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(0.5f, 0.5f); crt.anchorMax = new Vector2(0.5f, 0.5f);
        crt.pivot = new Vector2(0.5f, 0.5f); crt.anchoredPosition = Vector2.zero;
        crt.sizeDelta = new Vector2(560, 560);

        var cbg = card.AddComponent<Image>();
        cbg.color = new Color(0.10f, 0.12f, 0.18f, 0.95f);

        var vlg = card.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(24, 24, 30, 30); vlg.spacing = 12;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;

        if (_resolvedDish != null && _resolvedDish.DishSprite != null)
        {
            var imgGO = NewRect("Sprite", card.transform);
            var img = imgGO.AddComponent<Image>();
            img.sprite = _resolvedDish.DishSprite;
            img.preserveAspect = true;
            imgGO.AddComponent<LayoutElement>().preferredHeight = 280;
        }

        string dishName = _resolvedDish != null ? _resolvedDish.DishName : "Mystery Dish";
        var name = NewText("Name", card, dishName, 38, FontStyles.Bold,
            new Color(1f, 0.85f, 0.35f), TextAlignmentOptions.Center);
        name.AddComponent<LayoutElement>().preferredHeight = 60;

        string country = _resolvedDish != null ? _resolvedDish.CountryOfOrigin : "Unknown";
        var origin = NewText("Origin", card, $"<i>from {country}</i>", 22, FontStyles.Italic,
            new Color(0.85f, 0.85f, 0.95f), TextAlignmentOptions.Center);
        origin.AddComponent<LayoutElement>().preferredHeight = 32;

        var hint = NewText("Hint", card, "↑  Slide up to send", 24, FontStyles.Bold,
            new Color(1f, 0.6f, 0.4f), TextAlignmentOptions.Center);
        hint.AddComponent<LayoutElement>().preferredHeight = 40;
        hint.GetComponent<TextMeshProUGUI>().DOFade(0.3f, 0.8f).SetLoops(-1, LoopType.Yoyo);

        // Make the card draggable
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
        Vector2 originPx = new Vector2((normOrigin.x - 0.5f) * size.x, (normOrigin.y - 0.5f) * size.y);
        Vector2 destPx = new Vector2((normDest.x - 0.5f) * size.x, (normDest.y - 0.5f) * size.y);

        // Route line (a thin Image stretched between two points)
        var line = NewRect("RouteLine", _overlayLayer);
        var lImg = line.AddComponent<Image>();
        lImg.color = new Color(1f, 0.85f, 0.4f, 0.85f);
        var lrt = line.GetComponent<RectTransform>();
        lrt.anchorMin = lrt.anchorMax = lrt.pivot = new Vector2(0.5f, 0.5f);
        Vector2 dir = destPx - originPx;
        float len = dir.magnitude;
        lrt.sizeDelta = new Vector2(0, 4);
        lrt.anchoredPosition = (originPx + destPx) * 0.5f;
        lrt.localEulerAngles = new Vector3(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        lImg.DOFade(0.85f, 0.4f).From(0f);
        lrt.DOSizeDelta(new Vector2(len, 4), 0.5f).SetEase(Ease.OutQuad);

        // Origin + destination markers
        AddMarker(originPx, new Color(1f, 0.85f, 0.4f), origin.ToString());
        AddMarker(destPx, new Color(1f, 0.5f, 0.3f), dest.ToString());

        // Caravan
        var car = NewRect("Caravan", _overlayLayer);
        var cImg = car.AddComponent<Image>();
        cImg.sprite = caravanSprite;
        cImg.color = caravanSprite != null ? Color.white : new Color(0.95f, 0.7f, 0.25f);
        cImg.preserveAspect = true;
        var crt = car.GetComponent<RectTransform>();
        crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(0.5f, 0.5f);
        crt.sizeDelta = new Vector2(40, 40);
        crt.anchoredPosition = originPx;
        crt.localEulerAngles = new Vector3(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);

        DOVirtual.DelayedCall(0.5f, () =>
        {
            crt.DOAnchorPos(destPx, duration - 0.5f).SetEase(Ease.InOutSine)
                .OnComplete(() => onDone?.Invoke());
        });
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
