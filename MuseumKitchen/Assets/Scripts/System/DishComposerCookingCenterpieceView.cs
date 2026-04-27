using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DishComposerCookingCenterpieceView : MonoBehaviour
{
    public Image iconImage;
    public TMP_Text methodLabel;
    public Button cookButton;
    public TMP_Text cookButtonLabel;
    public bool resizeFromConfig = true;

    private void Reset()
    {
        iconImage = iconImage != null ? iconImage : FindImage("Image") ?? GetComponentInChildren<Image>(true);
        methodLabel = methodLabel != null ? methodLabel : FindText("Label");
        cookButton = cookButton != null ? cookButton : FindButton("CookBtn") ?? GetComponentInChildren<Button>(true);
        cookButtonLabel = cookButtonLabel != null ? cookButtonLabel : FindText("CookBtn/Label") ?? (cookButton != null ? cookButton.GetComponentInChildren<TMP_Text>(true) : null);
    }

    private Image FindImage(string childName)
    {
        Transform child = DishComposerCanvasSkin.FindDeepChild(transform, childName);
        return child != null ? child.GetComponent<Image>() : null;
    }

    private TMP_Text FindText(string childName)
    {
        Transform child = FindPath(transform, childName);
        return child != null ? child.GetComponent<TMP_Text>() : null;
    }

    private Button FindButton(string childName)
    {
        Transform child = DishComposerCanvasSkin.FindDeepChild(transform, childName);
        return child != null ? child.GetComponent<Button>() : null;
    }

    private static Transform FindPath(Transform root, string path)
    {
        if (root == null || string.IsNullOrEmpty(path)) return null;
        string[] parts = path.Split('/');
        Transform current = root;
        foreach (string part in parts)
        {
            current = DishComposerCanvasSkin.FindDeepChild(current, part);
            if (current == null) return null;
        }
        return current;
    }
}
