using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// One dish card on the big screen — frameless: a large dish image stacked over text.
/// Falls in from above (echoing the client's "fly up" animation), then auto-fades after lifetime.
/// 大屏卡片：无边框，大图压文字；从上方落入，超时淡出。
/// </summary>
public class DishCardUI : MonoBehaviour
{
    [SerializeField] private float lifetime = 30f;
    [SerializeField] private float fadeDuration = 1.5f;

    private CanvasGroup _canvasGroup;

    public void Initialize(PotluckData data, float cardLifetime = 30f)
    {
        lifetime = cardLifetime;
        BuildVisuals(data);
        PlayDropInAnimation();
        if (lifetime > 0)
            DOVirtual.DelayedCall(lifetime, () => FadeOutAndDestroy()).SetLink(gameObject);
    }

    private void BuildVisuals(PotluckData data)
    {
        _canvasGroup = gameObject.AddComponent<CanvasGroup>();

        var rect = GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(320, 360);

        // Resolve the Dish SO so we can pull its sprite (sender doesn't transmit images).
        var dish = DishDatabase.FindByName(data.dishAssetName);

        // Big dish image — top portion, centered.
        if (dish != null && dish.DishSprite != null)
        {
            var imgGO = NewChild("DishImage");
            var img = imgGO.AddComponent<Image>();
            img.sprite = dish.DishSprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
            var iRT = imgGO.GetComponent<RectTransform>();
            iRT.anchorMin = new Vector2(0.5f, 1f); iRT.anchorMax = new Vector2(0.5f, 1f);
            iRT.pivot = new Vector2(0.5f, 1f);
            iRT.anchoredPosition = new Vector2(0, 0);
            iRT.sizeDelta = new Vector2(280, 240);
        }

        // Dish name (under image)
        string dishName = string.IsNullOrEmpty(data.dishName) ? "Mystery Dish" : data.dishName;
        AddText("DishName", dishName, 24, FontStyles.Bold,
            new Color(1f, 0.9f, 0.45f), TextAlignmentOptions.Center,
            new Vector2(310, 32), new Vector2(0, -245));

        // Chef
        string chef = string.IsNullOrEmpty(data.clientId) ? "Anonymous Chef" : data.clientId;
        AddText("Chef", $"by <i>{chef}</i>", 16, FontStyles.Normal,
            new Color(0.9f, 0.9f, 0.95f), TextAlignmentOptions.Center,
            new Vector2(310, 22), new Vector2(0, -280));

        // Origin · cooking line
        if (!string.IsNullOrEmpty(data.countryOfOrigin))
            AddText("Origin", $"<color=#bcd>{data.countryOfOrigin}</color> · {data.cookingMethod}",
                14, FontStyles.Normal, new Color(0.85f, 0.9f, 0.95f), TextAlignmentOptions.Center,
                new Vector2(310, 20), new Vector2(0, -305));

        // Spice line
        if (!string.IsNullOrEmpty(data.spice))
            AddText("Spice",
                $"<color=#fda>{data.spice}</color>" +
                (data.distanceMiles > 0 ? $"  ·  {data.distanceMiles:N0} mi" : ""),
                14, FontStyles.Italic, new Color(0.95f, 0.85f, 0.55f), TextAlignmentOptions.Center,
                new Vector2(310, 20), new Vector2(0, -328));
    }

    private void PlayDropInAnimation()
    {
        var rect = GetComponent<RectTransform>();
        Vector2 target = rect.anchoredPosition;
        rect.anchoredPosition = target + new Vector2(0, 600);
        transform.localScale = Vector3.one * 1.1f;
        _canvasGroup.alpha = 0f;

        var seq = DOTween.Sequence();
        seq.Append(rect.DOAnchorPos(target, 0.7f).SetEase(Ease.OutBounce));
        seq.Join(transform.DOScale(1f, 0.5f).SetEase(Ease.OutBack));
        seq.Join(_canvasGroup.DOFade(1f, 0.3f));
    }

    public void FadeOutAndDestroy()
    {
        if (_canvasGroup == null) return;
        _canvasGroup.DOFade(0f, fadeDuration).SetEase(Ease.InQuad)
            .OnComplete(() => Destroy(gameObject));
    }

    private GameObject NewChild(string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(transform, false);
        go.layer = LayerMask.NameToLayer("UI");
        return go;
    }

    /// <summary>Add a TMP text positioned absolutely (anchored top-center of the card).</summary>
    private void AddText(string name, string content, float size, FontStyles style, Color color,
                          TextAlignmentOptions align, Vector2 sizeDelta, Vector2 anchoredPos)
    {
        var go = NewChild(name);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f); rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = content;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.alignment = align;
        tmp.richText = true;
        tmp.raycastTarget = false;
        // Outline so frameless text stays readable on any background.
        tmp.outlineColor = new Color(0, 0, 0, 0.85f);
        tmp.outlineWidth = 0.2f;
        UIFont.Apply(tmp);
    }
}
