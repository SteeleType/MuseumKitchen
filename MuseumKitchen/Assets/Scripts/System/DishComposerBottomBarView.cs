using UnityEngine;

public class DishComposerBottomBarView : MonoBehaviour
{
    [Tooltip("Where generated option buttons should be inserted. If empty, buttons are inserted under this object.")]
    public RectTransform optionContainer;

    private void Reset()
    {
        Transform child = DishComposerCanvasSkin.FindDeepChild(transform, "Options");
        optionContainer = child as RectTransform;
    }
}
