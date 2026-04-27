using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DishComposerOptionButtonView : MonoBehaviour
{
    public Button button;
    public TMP_Text labelText;
    public LayoutElement layoutElement;
    public bool resizeToRequestedWidth = true;

    private void Reset()
    {
        button = button != null ? button : GetComponent<Button>() ?? GetComponentInChildren<Button>(true);
        labelText = labelText != null ? labelText : GetComponentInChildren<TMP_Text>(true);
        layoutElement = layoutElement != null ? layoutElement : GetComponent<LayoutElement>();
    }
}
