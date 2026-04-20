using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Big-screen orchestrator. State machine:
///   Idle → first dish arrives → Counting (30s) → Settlement → back to Idle
///
/// During Counting all incoming dishes are spawned as cards.
/// On Settlement: cards stay, an overlay shows aggregate stats (count, countries, spices, miles).
///
/// 大屏状态机：第一道菜上桌 → 30 秒倒计时 → 结算（覆盖文字）→ 自动回归初始。
/// </summary>
public class PotluckManager : MonoBehaviour
{
    [Header("Display Settings / 显示设置")]
    public int maxCards = 30;
    public float cardLifetime = 0f; // 0 = never auto-fade during the round

    [Header("Grid Layout / 网格布局")]
    public int columns = 6;
    public Vector2 cardSpacing = new Vector2(20, 20);
    public Vector2 cardSize = new Vector2(320, 220);
    public Vector2 gridOffset = new Vector2(40, -180);

    [Header("Round / 一轮宴会")]
    [Tooltip("Seconds the round lasts after the first dish arrives.\n第一道菜上桌后倒计时秒数。")]
    public float roundDuration = 30f;

    [Tooltip("Seconds the settlement overlay stays before returning to Idle.\n结算面板停留秒数后回到初始。")]
    public float settlementHold = 12f;

    [Header("Big Screen Header / 大屏标题")]
    public string title = "Today's Museum Potluck";
    public string subtitle = "Build a dumpling on a tablet — it shows up here.";
    public string emptyStateText = "Waiting for our first chef...";

    private enum Phase { Idle, Counting, Settlement }
    private Phase _phase = Phase.Idle;

    // Runtime visuals
    private Transform _cardContainer;
    private List<DishCardUI> _activeCards = new List<DishCardUI>();
    private TMP_Text _emptyStateLabel;
    private TMP_Text _countdownLabel;
    private GameObject _settlementOverlay;
    private TMP_Text _settlementText;

    // Round aggregation
    private float _countdownEndsAt;
    private readonly List<PotluckData> _roundDishes = new List<PotluckData>();

    private void Awake()
    {
        var canvas = FindObjectOfType<Canvas>();
        if (canvas == null) { Debug.LogError("[PotluckManager] No Canvas in scene."); return; }

        var container = new GameObject("DishCardContainer", typeof(RectTransform));
        container.transform.SetParent(canvas.transform, false);
        container.layer = LayerMask.NameToLayer("UI");
        var rt = container.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        _cardContainer = container.transform;

        BuildHeader(canvas.transform);
        BuildEmptyState(_cardContainer);
        BuildCountdown(canvas.transform);
        BuildSettlementOverlay(canvas.transform);
    }

    public void OnPotluckDataReceived(PotluckData incoming)
    {
        if (_phase == Phase.Settlement)
        {
            // Round is finished and overlay is up; queue would be nice but for demo we just drop.
            Debug.Log("[PotluckManager] Dish arrived during settlement; ignored.");
            return;
        }

        if (_phase == Phase.Idle)
        {
            // First dish kicks off the round.
            StartRound();
        }

        if (_activeCards.Count >= maxCards)
            RemoveOldestCard();

        SpawnCard(incoming);
        _roundDishes.Add(incoming);
    }

    // ───────────────────── Round flow ─────────────────────

    private void StartRound()
    {
        _phase = Phase.Counting;
        _roundDishes.Clear();
        _countdownEndsAt = Time.time + roundDuration;
        if (_countdownLabel != null) _countdownLabel.gameObject.SetActive(true);
    }

    private void EndRound()
    {
        _phase = Phase.Settlement;
        if (_countdownLabel != null) _countdownLabel.gameObject.SetActive(false);
        ShowSettlement();
        DOVirtual.DelayedCall(settlementHold, ResetToIdle).SetLink(gameObject);
    }

    private void ResetToIdle()
    {
        _phase = Phase.Idle;
        _roundDishes.Clear();

        // Fade out all cards
        foreach (var c in _activeCards) if (c != null) c.FadeOutAndDestroy();
        _activeCards.Clear();

        if (_settlementOverlay != null) HideSettlement();
        UpdateEmptyState();
    }

    private void Update()
    {
        if (_phase == Phase.Counting)
        {
            float remaining = Mathf.Max(0, _countdownEndsAt - Time.time);
            if (_countdownLabel != null)
                _countdownLabel.text = Mathf.CeilToInt(remaining).ToString();
            if (remaining <= 0f) EndRound();
        }
    }

    // ───────────────────── Settlement ─────────────────────

    private void ShowSettlement()
    {
        if (_settlementOverlay == null || _settlementText == null) return;

        int count = _roundDishes.Count;
        var countries = new HashSet<string>();
        var spices = new HashSet<string>();
        long totalMiles = 0;
        foreach (var d in _roundDishes)
        {
            if (!string.IsNullOrEmpty(d.countryOfOrigin)) countries.Add(d.countryOfOrigin);
            if (!string.IsNullOrEmpty(d.spice)) spices.Add(d.spice);
            totalMiles += d.distanceMiles;
        }

        string spicesList = spices.Count == 0 ? "—" : string.Join(", ", spices);
        _settlementText.text =
            $"<size=70><b>The table is set!</b></size>\n\n" +
            $"<size=42>{count} dish{(count == 1 ? "" : "es")} from {countries.Count} countr{(countries.Count == 1 ? "y" : "ies")}</size>\n\n" +
            $"<size=30>Spices: <color=#fda>{spicesList}</color></size>\n" +
            $"<size=30>These spices traveled <color=#fda>{totalMiles:N0} miles</color> to reach our table.</size>";

        _settlementOverlay.SetActive(true);
        var cg = _settlementOverlay.GetComponent<CanvasGroup>();
        cg.alpha = 0;
        cg.DOFade(1f, 0.6f);
    }

    private void HideSettlement()
    {
        var cg = _settlementOverlay.GetComponent<CanvasGroup>();
        cg.DOFade(0f, 0.6f).OnComplete(() => _settlementOverlay.SetActive(false));
    }

    // ───────────────────── Cards ─────────────────────

    private void SpawnCard(PotluckData data)
    {
        if (_cardContainer == null) return;

        var cardGO = new GameObject($"DishCard_{data.clientId}", typeof(RectTransform));
        cardGO.transform.SetParent(_cardContainer, false);
        cardGO.layer = LayerMask.NameToLayer("UI");

        var cardRect = cardGO.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0, 1);
        cardRect.anchorMax = new Vector2(0, 1);
        cardRect.pivot = new Vector2(0, 1);
        cardRect.sizeDelta = cardSize;

        int index = _activeCards.Count;
        int col = index % columns;
        int row = index / columns;
        cardRect.anchoredPosition = new Vector2(
            gridOffset.x + col * (cardSize.x + cardSpacing.x),
            gridOffset.y - row * (cardSize.y + cardSpacing.y));

        var card = cardGO.AddComponent<DishCardUI>();
        card.Initialize(data, cardLifetime);
        _activeCards.Add(card);

        UpdateEmptyState();
    }

    private void RemoveOldestCard()
    {
        if (_activeCards.Count == 0) return;
        var oldest = _activeCards[0];
        _activeCards.RemoveAt(0);
        if (oldest != null) oldest.FadeOutAndDestroy();
        RepositionCards();
    }

    private void RepositionCards()
    {
        for (int i = 0; i < _activeCards.Count; i++)
        {
            if (_activeCards[i] == null) continue;
            int col = i % columns;
            int row = i / columns;
            var pos = new Vector2(
                gridOffset.x + col * (cardSize.x + cardSpacing.x),
                gridOffset.y - row * (cardSize.y + cardSpacing.y));
            _activeCards[i].GetComponent<RectTransform>().DOAnchorPos(pos, 0.4f).SetEase(Ease.OutCubic);
        }
    }

    private void LateUpdate()
    {
        int before = _activeCards.Count;
        _activeCards.RemoveAll(c => c == null);
        if (before != _activeCards.Count) UpdateEmptyState();
    }

    // ───────────────────── UI build ─────────────────────

    private void BuildHeader(Transform canvasTransform)
    {
        var header = NewRect("BigScreenHeader", canvasTransform);
        var rt = header.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(0.5f, 1); rt.anchoredPosition = new Vector2(0, -20);
        rt.sizeDelta = new Vector2(0, 110);

        var vlg = header.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(40, 40, 8, 8); vlg.spacing = 4;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;

        AddText(header, "Title", title, 48, FontStyles.Bold, new Color(1f, 0.85f, 0.35f), TextAlignmentOptions.Center, 60);
        AddText(header, "Subtitle", subtitle, 22, FontStyles.Italic, new Color(0.85f, 0.85f, 0.95f, 0.85f), TextAlignmentOptions.Center, 32);
    }

    private void BuildEmptyState(Transform parent)
    {
        var go = NewRect("EmptyState", parent);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f); rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero; rt.sizeDelta = new Vector2(900, 80);
        _emptyStateLabel = go.AddComponent<TextMeshProUGUI>();
        _emptyStateLabel.text = emptyStateText; _emptyStateLabel.fontSize = 36;
        _emptyStateLabel.fontStyle = FontStyles.Italic;
        _emptyStateLabel.alignment = TextAlignmentOptions.Center;
        _emptyStateLabel.color = new Color(1f, 1f, 1f, 0.5f);
        UIFont.Apply(_emptyStateLabel);
    }

    private void BuildCountdown(Transform canvasTransform)
    {
        var go = NewRect("Countdown", canvasTransform);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1, 1); rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(1, 1); rt.anchoredPosition = new Vector2(-30, -30);
        rt.sizeDelta = new Vector2(180, 120);

        _countdownLabel = go.AddComponent<TextMeshProUGUI>();
        _countdownLabel.text = "30"; _countdownLabel.fontSize = 90; _countdownLabel.fontStyle = FontStyles.Bold;
        _countdownLabel.alignment = TextAlignmentOptions.Right;
        _countdownLabel.color = new Color(1f, 0.6f, 0.4f);
        UIFont.Apply(_countdownLabel);
        go.SetActive(false);
    }

    private void BuildSettlementOverlay(Transform canvasTransform)
    {
        _settlementOverlay = NewRect("SettlementOverlay", canvasTransform);
        var rt = _settlementOverlay.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

        var bg = _settlementOverlay.AddComponent<Image>();
        bg.color = new Color(0.04f, 0.05f, 0.10f, 0.92f);
        bg.raycastTarget = false;

        _settlementOverlay.AddComponent<CanvasGroup>();

        var textGO = NewRect("SettlementText", _settlementOverlay.transform);
        var trt = textGO.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(80, 80); trt.offsetMax = new Vector2(-80, -80);

        _settlementText = textGO.AddComponent<TextMeshProUGUI>();
        _settlementText.text = ""; _settlementText.fontSize = 36;
        _settlementText.alignment = TextAlignmentOptions.Center;
        _settlementText.color = Color.white; _settlementText.richText = true;
        _settlementText.enableWordWrapping = true;
        UIFont.Apply(_settlementText);

        _settlementOverlay.SetActive(false);
    }

    private void UpdateEmptyState()
    {
        if (_emptyStateLabel == null) return;
        _emptyStateLabel.gameObject.SetActive(_phase == Phase.Idle && _activeCards.Count == 0);
    }

    // helpers
    private GameObject NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.layer = LayerMask.NameToLayer("UI");
        return go;
    }

    private void AddText(GameObject parent, string name, string content, float size, FontStyles style,
        Color color, TextAlignmentOptions align, float preferredHeight)
    {
        var go = NewRect(name, parent.transform);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = content; tmp.fontSize = size; tmp.fontStyle = style; tmp.color = color;
        tmp.alignment = align; tmp.richText = true;
        UIFont.Apply(tmp);
        go.AddComponent<LayoutElement>().preferredHeight = preferredHeight;
    }
}
