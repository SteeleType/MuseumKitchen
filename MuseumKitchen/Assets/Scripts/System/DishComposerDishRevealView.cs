using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DishComposerDishRevealView : MonoBehaviour
{
    public Image dimImage;
    public RectTransform chefInputRow;
    public TMP_InputField chefInput;
    public TMP_Text chefLabel;
    public RectTransform dishCard;
    public Image dishImage;
    public TMP_Text dishNameText;
    public TMP_Text dishOriginText;
    public TMP_Text swipeHintText;
    public bool animateCardIn = true;
    public bool pulseHint = true;

    private void Reset()
    {
        dimImage = dimImage != null ? dimImage : FindImage("Dim");
        chefInputRow = chefInputRow != null ? chefInputRow : FindRect("ChefInputRow");
        chefInput = chefInput != null ? chefInput : GetComponentInChildren<TMP_InputField>(true);
        chefLabel = chefLabel != null ? chefLabel : FindText("ChefLabel") ?? FindText("Label");
        dishCard = dishCard != null ? dishCard : FindRect("DishPlate") ?? FindRect("DishCard");
        dishImage = dishImage != null ? dishImage : FindImage("DishImage");
        dishNameText = dishNameText != null ? dishNameText : FindText("Name") ?? FindText("DishName");
        dishOriginText = dishOriginText != null ? dishOriginText : FindText("Origin") ?? FindText("Country");
        swipeHintText = swipeHintText != null ? swipeHintText : FindText("Hint") ?? FindText("SwipeHint");
    }

    private RectTransform FindRect(string childName)
    {
        Transform child = DishComposerCanvasSkin.FindDeepChild(transform, childName);
        return child as RectTransform;
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
