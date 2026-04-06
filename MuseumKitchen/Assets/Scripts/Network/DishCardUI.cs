using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// A single dish card displayed on the Big Screen when a client submits their creation.
/// Animates in with DOTween (scale punch + slide down), then auto-fades after a configurable lifetime.
///
/// 大屏上收到客户端提交后显示的单张菜品卡片。
/// 使用 DOTween 做弹入动画（缩放+下滑），超时后自动淡出销毁。
/// </summary>
public class DishCardUI : MonoBehaviour
{
    [Header("Lifetime / 生命周期")]
    [Tooltip("Seconds before the card starts fading out. 0 = never fade.\n卡片开始淡出前的秒数，0 = 永不消失。")]
    public float lifetime = 30f;

    [Tooltip("Fade-out duration in seconds.\n淡出动画时长（秒）。")]
    public float fadeDuration = 1.5f;

    // Internal refs (set by Initialize) / 内部引用（由 Initialize 设置）
    private CanvasGroup _canvasGroup;
    private TMP_Text _titleText;
    private TMP_Text _fillingText;
    private TMP_Text _wrappingText;
    private TMP_Text _cookingText;

    /// <summary>
    /// Build the card's visual content and kick off the entrance animation.
    /// 构建卡片的视觉内容并启动入场动画。
    /// </summary>
    public void Initialize(PotluckData data, float cardLifetime = 30f)
    {
        lifetime = cardLifetime;
        BuildCardVisuals(data);
        PlayEntranceAnimation();

        // Schedule fade-out / 安排淡出
        if (lifetime > 0)
        {
            DOVirtual.DelayedCall(lifetime, () => FadeOutAndDestroy());
        }
    }

    private void BuildCardVisuals(PotluckData data)
    {
        // Ensure we have a CanvasGroup for fading / 确保有 CanvasGroup 用于淡出
        _canvasGroup = gameObject.AddComponent<CanvasGroup>();

        var rect = GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(320, 180);

        // Card background / 卡片背景
        var bg = gameObject.AddComponent<Image>();
        bg.color = new Color(0.12f, 0.14f, 0.2f, 0.92f);

        // Vertical layout / 垂直布局
        var vlg = gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(14, 14, 10, 10);
        vlg.spacing = 4;
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        // Title (client name) / 标题（设备名）
        string displayName = string.IsNullOrEmpty(data.clientId) ? "Unknown Chef" : data.clientId;
        _titleText = CreateText("Title", $">> {displayName}", 18, FontStyles.Bold,
            new Color(1f, 0.85f, 0.35f), 26);

        // Separator line / 分隔线
        var sep = CreateUIChild("Separator");
        var sepImg = sep.AddComponent<Image>();
        sepImg.color = new Color(1f, 1f, 1f, 0.15f);
        var sepLE = sep.AddComponent<LayoutElement>();
        sepLE.preferredHeight = 2;

        // Ingredient rows / 食材行
        _fillingText = CreateText("Filling", $"Filling:  {data.fillingName}", 15, FontStyles.Normal,
            new Color(0.9f, 0.7f, 0.5f), 22);

        _wrappingText = CreateText("Wrapping", $"Wrapper:  {data.wrappingName}", 15, FontStyles.Normal,
            new Color(0.7f, 0.85f, 0.95f), 22);

        _cookingText = CreateText("Cooking", $"Cooking:  {data.cookingMethodName}", 15, FontStyles.Normal,
            new Color(0.65f, 0.95f, 0.65f), 22);
    }

    private void PlayEntranceAnimation()
    {
        var rect = GetComponent<RectTransform>();

        // Start slightly above and scaled down / 从上方略偏的位置开始，缩小状态
        Vector2 targetPos = rect.anchoredPosition;
        rect.anchoredPosition = targetPos + new Vector2(0, 60);
        transform.localScale = Vector3.one * 0.5f;
        _canvasGroup.alpha = 0f;

        // Animate in / 入场动画
        Sequence seq = DOTween.Sequence();
        seq.Append(rect.DOAnchorPos(targetPos, 0.5f).SetEase(Ease.OutBack));
        seq.Join(transform.DOScale(1f, 0.5f).SetEase(Ease.OutBack));
        seq.Join(_canvasGroup.DOFade(1f, 0.3f));
    }

    /// <summary>
    /// Fade out and self-destruct / 淡出并自毁
    /// </summary>
    public void FadeOutAndDestroy()
    {
        if (_canvasGroup == null) return;

        _canvasGroup.DOFade(0f, fadeDuration)
            .SetEase(Ease.InQuad)
            .OnComplete(() => Destroy(gameObject));
    }

    // ── Helpers / 辅助方法 ──

    private TMP_Text CreateText(string name, string content, float fontSize,
        FontStyles style, Color color, float height)
    {
        var go = CreateUIChild(name);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = content;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.richText = true;
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = height;
        return tmp;
    }

    private GameObject CreateUIChild(string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(transform, false);
        go.layer = LayerMask.NameToLayer("UI");
        return go;
    }
}
