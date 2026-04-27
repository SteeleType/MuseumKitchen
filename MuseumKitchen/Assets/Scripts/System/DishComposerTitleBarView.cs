using TMPro;
using UnityEngine;

public class DishComposerTitleBarView : MonoBehaviour
{
    public TMP_Text titleText;
    public TMP_Text subtitleText;

    private void Reset()
    {
        titleText = titleText != null ? titleText : FindText("Title");
        subtitleText = subtitleText != null ? subtitleText : FindText("Sub") ?? FindText("Subtitle");
    }

    private TMP_Text FindText(string childName)
    {
        Transform child = DishComposerCanvasSkin.FindDeepChild(transform, childName);
        return child != null ? child.GetComponent<TMP_Text>() : null;
    }
}
