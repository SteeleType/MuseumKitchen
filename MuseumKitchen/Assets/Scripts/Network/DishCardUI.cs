using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// One dish card on the big screen. Falls in from above (so it visually
/// echoes the client's "fly up" animation), then auto-fades after lifetime.
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
        rect.sizeDelta = new Vector2(320, 220);

        var bg = gameObject.AddComponent<Image>();
        bg.color = new Color(0.10f, 0.12f, 0.18f, 0.92f);

        var vlg = gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(14, 14, 10, 10);
        vlg.spacing = 4;
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;

        // Resolve the Dish SO so we can pull its sprite (sender doesn't transmit images)
        var dish = DishDatabase.FindByName(data.dishAssetName);

        // Top row: dish sprite (if any) + dish name
        if (dish != null && dish.DishSprite != null)
        {
            var img = NewChild("DishImage").AddComponent<Image>();
            img.sprite = dish.DishSprite;
            img.preserveAspect = true;
            img.transform.SetParent(transform, false);
            img.gameObject.AddComponent<LayoutElement>().preferredHeight = 90;
        }

        string dishName = string.IsNullOrEmpty(data.dishName) ? "Mystery Dish" : data.dishName;
        Text("DishName", dishName, 22, FontStyles.Bold, new Color(1f, 0.85f, 0.35f), 30);

        string chef = string.IsNullOrEmpty(data.clientId) ? "Anonymous Chef" : data.clientId;
        Text("Chef", $"by <i>{chef}</i>", 14, FontStyles.Normal, new Color(0.85f, 0.85f, 0.95f), 20);

        // Origin + spice line
        if (!string.IsNullOrEmpty(data.countryOfOrigin))
            Text("Origin", $"<color=#aac>{data.countryOfOrigin}</color> · {data.cookingMethod}", 13,
                FontStyles.Normal, new Color(0.75f, 0.85f, 0.95f), 18);

        if (!string.IsNullOrEmpty(data.spice))
            Text("Spice", $"Spice: <color=#fda>{data.spice}</color>" +
                          (data.distanceMiles > 0 ? $"  ({data.distanceMiles:N0} mi)" : ""),
                13, FontStyles.Italic, new Color(0.95f, 0.85f, 0.55f), 18);
    }

    private void PlayDropInAnimation()
    {
        var rect = GetComponent<RectTransform>();
        Vector2 target = rect.anchoredPosition;
        rect.anchoredPosition = target + new Vector2(0, 600); // start way above
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

    private void Text(string name, string content, float size, FontStyles style, Color color, float h)
    {
        var go = NewChild(name);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = content; tmp.fontSize = size; tmp.fontStyle = style; tmp.color = color;
        tmp.alignment = TextAlignmentOptions.MidlineLeft; tmp.richText = true;
        go.AddComponent<LayoutElement>().preferredHeight = h;
    }
}
