using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DishComposerCookingTileView : MonoBehaviour
{
    public Button button;
    public Image iconImage;
    public TMP_Text labelText;
    public CanvasGroup unavailableGroup;
    public bool resizeFromConfig = true;

    private void Reset()
    {
        button = button != null ? button : GetComponent<Button>() ?? GetComponentInChildren<Button>(true);
        iconImage = iconImage != null ? iconImage : FindImage("Image") ?? GetComponentInChildren<Image>(true);
        labelText = labelText != null ? labelText : FindText("Label") ?? GetComponentInChildren<TMP_Text>(true);
        unavailableGroup = unavailableGroup != null ? unavailableGroup : GetComponent<CanvasGroup>();
    }

    private Image FindImage(string childName)
    {
        Transform child = DishComposerCanvasSkin.FindDeepChild(transform, childName);
        return child != null ? child.GetComponent<Image>() : null;
    }

    private TMP_Text FindText(string childName)
    {
        Transform child = DishComposerCanvasSkin.FindDeepChild(transform, childName);
        return child != null ? child.GetComponent<TMP_Text>() : null;
    }
}
