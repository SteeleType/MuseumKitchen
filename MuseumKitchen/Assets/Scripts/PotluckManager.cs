using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using DG.Tweening;

/// <summary>
/// Big Screen display manager: receives PotluckData from LAN clients
/// and spawns animated dish cards on the Canvas.
///
/// 大屏展示管理器：接收局域网客户端的 PotluckData，
/// 在 Canvas 上生成带动画的菜品卡片。
///
/// Cards are arranged in a grid layout. When the max card count is
/// exceeded, the oldest card fades out automatically.
/// 卡片以网格排列。超过最大数量时，最早的卡片自动淡出。
/// </summary>
public class PotluckManager : MonoBehaviour
{
    [Header("Display Settings / 显示设置")]
    [Tooltip("Max cards visible on screen. Oldest card fades when exceeded.\n屏幕上最多显示的卡片数，超出时最老的卡片淡出。")]
    public int maxCards = 12;

    [Tooltip("Card lifetime in seconds before auto-fade. 0 = never.\n卡片自动淡出前的存活秒数，0 = 永不消失。")]
    public float cardLifetime = 60f;

    [Header("Grid Layout / 网格布局")]
    [Tooltip("Number of columns in the card grid.\n卡片网格的列数。")]
    public int columns = 4;

    [Tooltip("Spacing between cards.\n卡片间距。")]
    public Vector2 cardSpacing = new Vector2(20, 20);

    [Tooltip("Card size in pixels.\n卡片像素尺寸。")]
    public Vector2 cardSize = new Vector2(320, 180);

    [Tooltip("Offset from top-left of canvas for the grid origin.\n网格起点相对Canvas左上角的偏移。")]
    public Vector2 gridOffset = new Vector2(40, -120);

    // Runtime state / 运行时状态
    private Transform _cardContainer;
    private List<DishCardUI> _activeCards = new List<DishCardUI>();

    private void Awake()
    {
        // Create a container under the Canvas for all cards
        // 在 Canvas 下创建一个容器来放所有卡片
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas != null)
        {
            var container = new GameObject("DishCardContainer", typeof(RectTransform));
            container.transform.SetParent(canvas.transform, false);
            container.layer = LayerMask.NameToLayer("UI");

            // Stretch fill the entire canvas / 撑满整个Canvas
            var rt = container.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            _cardContainer = container.transform;
        }
        else
        {
            Debug.LogError("[PotluckManager] No Canvas found! Cards cannot be displayed.");
        }
    }

    /// <summary>
    /// Called by MuseumLanReceiver when a LAN message arrives.
    /// 当局域网消息到达时由 MuseumLanReceiver 调用。
    /// </summary>
    public void OnPotluckDataReceived(PotluckData incomingDish)
    {
        Debug.Log($"[BigScreen] New dish from {incomingDish.clientId}: " +
                  $"Filling={incomingDish.fillingName}, " +
                  $"Wrapper={incomingDish.wrappingName}, " +
                  $"Cooking={incomingDish.cookingMethodName}");

        // Remove oldest card if at max / 如果达到上限，移除最老的卡片
        if (_activeCards.Count >= maxCards)
        {
            RemoveOldestCard();
        }

        SpawnCard(incomingDish);
    }

    private void SpawnCard(PotluckData data)
    {
        if (_cardContainer == null) return;

        // Create card GameObject / 创建卡片对象
        var cardGO = new GameObject($"DishCard_{data.clientId}", typeof(RectTransform));
        cardGO.transform.SetParent(_cardContainer, false);
        cardGO.layer = LayerMask.NameToLayer("UI");

        // Position in grid / 网格定位
        var cardRect = cardGO.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0, 1);
        cardRect.anchorMax = new Vector2(0, 1);
        cardRect.pivot = new Vector2(0, 1);
        cardRect.sizeDelta = cardSize;

        int index = _activeCards.Count;
        int col = index % columns;
        int row = index / columns;
        float x = gridOffset.x + col * (cardSize.x + cardSpacing.x);
        float y = gridOffset.y - row * (cardSize.y + cardSpacing.y);
        cardRect.anchoredPosition = new Vector2(x, y);

        // Add and initialize DishCardUI / 添加并初始化卡片脚本
        var card = cardGO.AddComponent<DishCardUI>();
        card.Initialize(data, cardLifetime);

        _activeCards.Add(card);
    }

    private void RemoveOldestCard()
    {
        if (_activeCards.Count == 0) return;

        var oldest = _activeCards[0];
        _activeCards.RemoveAt(0);

        if (oldest != null)
        {
            oldest.FadeOutAndDestroy();
        }

        // Re-position remaining cards with animation / 用动画重新排列剩余卡片
        RepositionCards();
    }

    private void RepositionCards()
    {
        for (int i = 0; i < _activeCards.Count; i++)
        {
            if (_activeCards[i] == null) continue;

            var rt = _activeCards[i].GetComponent<RectTransform>();
            int col = i % columns;
            int row = i / columns;
            float x = gridOffset.x + col * (cardSize.x + cardSpacing.x);
            float y = gridOffset.y - row * (cardSize.y + cardSpacing.y);

            // Smooth slide to new position / 平滑滑动到新位置
            rt.DOAnchorPos(new Vector2(x, y), 0.4f).SetEase(Ease.OutCubic);
        }
    }

    /// <summary>
    /// Clean up dead cards from the active list (called periodically or on demand).
    /// 清理已销毁的卡片引用。
    /// </summary>
    private void LateUpdate()
    {
        _activeCards.RemoveAll(c => c == null);
    }
}
