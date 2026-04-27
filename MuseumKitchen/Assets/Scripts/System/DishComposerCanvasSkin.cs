using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Optional view binding for a DishComposer Canvas prefab.
/// Artists can build a full Canvas, then wire these references so DishComposer only injects data and listeners.
/// </summary>
[DisallowMultipleComponent]
public class DishComposerCanvasSkin : MonoBehaviour
{
    [Header("Scaffold")]
    public Canvas canvas;
    public Image backgroundImage;
    public RectTransform mapHolder;
    public RectTransform mapRect;
    public Image mapImage;
    public RectTransform overlayLayer;
    public RectTransform stagePanelHolder;
    public bool useConfigScaffoldColors = true;

    [Header("Stage Templates")]
    public DishComposerTitleBarView titleBarTemplate;
    public DishComposerBottomBarView bottomBarTemplate;
    public DishComposerOptionButtonView optionButtonTemplate;
    public DishComposerCookingTileView cookingTileTemplate;
    public DishComposerCookingCenterpieceView cookingCenterpieceTemplate;
    public DishComposerDishRevealView dishRevealTemplate;
    public bool hideTemplatesOnAwake = true;

    private void Reset()
    {
        ResolveMissingReferences();
    }

    private void Awake()
    {
        ResolveMissingReferences();
        if (hideTemplatesOnAwake)
            HideTemplates();
    }

    public void ResolveMissingReferences()
    {
        canvas = canvas != null ? canvas : GetComponentInChildren<Canvas>(true);
        backgroundImage = backgroundImage != null ? backgroundImage : FindComponentInNamedChild<Image>("Background");
        mapHolder = mapHolder != null ? mapHolder : FindRect("MapHolder");
        mapRect = mapRect != null ? mapRect : FindRect("Map");
        mapImage = mapImage != null ? mapImage : (mapRect != null ? mapRect.GetComponent<Image>() : null);
        overlayLayer = overlayLayer != null ? overlayLayer : FindRect("Overlay");
        stagePanelHolder = stagePanelHolder != null ? stagePanelHolder : FindRect("StagePanels");

        titleBarTemplate = titleBarTemplate != null ? titleBarTemplate : GetComponentInChildren<DishComposerTitleBarView>(true);
        bottomBarTemplate = bottomBarTemplate != null ? bottomBarTemplate : GetComponentInChildren<DishComposerBottomBarView>(true);
        optionButtonTemplate = optionButtonTemplate != null ? optionButtonTemplate : GetComponentInChildren<DishComposerOptionButtonView>(true);
        cookingTileTemplate = cookingTileTemplate != null ? cookingTileTemplate : GetComponentInChildren<DishComposerCookingTileView>(true);
        cookingCenterpieceTemplate = cookingCenterpieceTemplate != null ? cookingCenterpieceTemplate : GetComponentInChildren<DishComposerCookingCenterpieceView>(true);
        dishRevealTemplate = dishRevealTemplate != null ? dishRevealTemplate : GetComponentInChildren<DishComposerDishRevealView>(true);
    }

    public void HideTemplates()
    {
        Hide(titleBarTemplate);
        Hide(bottomBarTemplate);
        Hide(optionButtonTemplate);
        Hide(cookingTileTemplate);
        Hide(cookingCenterpieceTemplate);
        Hide(dishRevealTemplate);
    }

    private static void Hide(Component component)
    {
        if (component != null)
            component.gameObject.SetActive(false);
    }

    private RectTransform FindRect(string childName)
    {
        Transform child = FindDeepChild(transform, childName);
        return child as RectTransform;
    }

    private T FindComponentInNamedChild<T>(string childName) where T : Component
    {
        Transform child = FindDeepChild(transform, childName);
        return child != null ? child.GetComponent<T>() : null;
    }

    public static Transform FindDeepChild(Transform root, string childName)
    {
        if (root == null || string.IsNullOrEmpty(childName)) return null;
        if (root.name == childName) return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform result = FindDeepChild(root.GetChild(i), childName);
            if (result != null) return result;
        }

        return null;
    }
}
