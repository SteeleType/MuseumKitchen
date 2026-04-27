using UnityEngine;

[CreateAssetMenu(fileName = "DishComposerConfig", menuName = "Museum Kitchen/Dish Composer Config")]
public class DishComposerConfig : ScriptableObject
{
    [Header("Assets")]
    public Sprite mapSprite;
    public Sprite caravanSprite;
    public Sprite panSprite;
    public Sprite potSprite;
    public Sprite ovenSprite;

    [Header("UI Sprites")]
    public Sprite panelSprite;
    public Sprite optionButtonSprite;
    public Sprite disabledOptionButtonSprite;
    public Sprite cookButtonSprite;
    public Sprite dishCardSprite;
    public Sprite markerSprite;
    public Sprite routeSegmentSprite;

    [Header("UI Prefab")]
    [Tooltip("Optional Canvas prefab with a DishComposerCanvasSkin component. Use it when art wants to control UI images, layout, and button states.")]
    public GameObject canvasPrefab;

    [Header("Stage Copy")]
    public string spiceTitle = "Choose a spice.";
    public string spiceSubtitle = "Every journey starts at its source.";
    public string distanceTitle = "How far should {spice} travel?";
    public string distanceSubtitle = "Pick a journey length.";
    public string cookingTitle = "How do they cook in {destination}?";
    public string cookingSubtitle = "Pick a method.";
    public string cookButtonLabel = "Cook!";
    public string chefLabel = "Chef:";
    public string chefPlaceholder = "";
    public string dishOriginFormat = "<i>from {country}</i>";
    public string swipeHint = "Slide up to send";

    [Header("Distance Bands")]
    [Min(0)] public int nearMaxMiles = 1500;
    [Min(0)] public int mediumMaxMiles = 3500;
    public string nearLabel = "Near\n0-1,500 mi";
    public string mediumLabel = "Medium\n1,501-3,500 mi";
    public string farLabel = "Far\n3,501+ mi";

    [Header("Timing")]
    [Min(0f)] public float resetDelayAfterSubmit = 1.0f;
    [Min(0f)] public float swipeDistanceThreshold = 300f;

    [Header("Colors")]
    public Color backgroundColor = new Color(0.06f, 0.13f, 0.18f, 1f);
    public Color mapTint = new Color(1f, 1f, 1f, 0.85f);
    public Color panelColor = new Color(0f, 0f, 0f, 0.55f);
    public Color optionColor = new Color(0.18f, 0.18f, 0.24f, 0.92f);
    public Color disabledOptionColor = new Color(0.12f, 0.12f, 0.15f, 0.4f);
    public Color titleColor = new Color(1f, 0.85f, 0.35f);
    public Color subtitleColor = new Color(0.85f, 0.85f, 0.95f);
    public Color mainTextColor = Color.white;
    public Color mutedTextColor = new Color(0.85f, 0.85f, 0.95f);
    public Color cookButtonColor = new Color(0.85f, 0.35f, 0.2f, 1f);
    public Color revealDimColor = new Color(0f, 0f, 0f, 0.85f);
    public Color hintColor = new Color(1f, 0.6f, 0.4f);
    public Color routeColor = new Color(1f, 0.85f, 0.4f, 0.85f);
    public Color originMarkerColor = new Color(1f, 0.85f, 0.4f);
    public Color destinationMarkerColor = new Color(1f, 0.5f, 0.3f);

    [Header("Stage Layout")]
    public Vector2 titleBarInset = new Vector2(80f, 20f);
    public float titleBarHeight = 130f;
    public float bottomBarSideInset = 80f;
    public float bottomBarBottomOffset = 30f;
    public float bottomBarHeight = 180f;
    public Vector2 optionButtonSize = new Vector2(200f, 110f);
    public float spiceButtonWidth = 200f;
    public float distanceButtonWidth = 260f;
    public float titleFontSize = 38f;
    public float subtitleFontSize = 22f;
    public float optionFontSize = 24f;

    [Header("Cooking Layout")]
    public Vector2 cookingTilesContainerSize = new Vector2(1400f, 460f);
    public Vector2 cookingTileSize = new Vector2(360f, 440f);
    public float cookingTileSpacing = 460f;
    public Vector2 cookingTileImageSize = new Vector2(300f, 300f);
    public float cookingTileImageTopOffset = -10f;
    public float cookingTileLabelBottomOffset = 10f;
    public float cookingTileLabelHeight = 60f;
    public float cookingTileLabelFontSize = 32f;
    public Vector2 cookingCenterpiecePosition = new Vector2(0f, 30f);
    public Vector2 cookingCenterpieceSize = new Vector2(540f, 700f);
    public Vector2 cookingCenterImageSize = new Vector2(480f, 480f);
    public float cookingCenterLabelTopOffset = -490f;
    public float cookingCenterLabelHeight = 50f;
    public float cookingCenterLabelFontSize = 36f;
    public float cookButtonTopOffset = -560f;
    public Vector2 cookButtonSize = new Vector2(420f, 110f);
    public float cookButtonFontSize = 50f;

    [Header("Dish Reveal Layout")]
    public Vector2 chefInputRowSize = new Vector2(700f, 80f);
    public Vector2 chefInputRowPosition = new Vector2(0f, -40f);
    public float chefLabelWidth = 80f;
    public float chefInputWidth = 460f;
    public float chefTextFontSize = 20f;
    public float chefLabelFontSize = 22f;
    public Vector2 dishCardSize = new Vector2(800f, 900f);
    public Vector2 revealDishImageSize = new Vector2(640f, 640f);
    public float revealDishImageTopOffset = -20f;
    public float dishNameTopOffset = -680f;
    public float dishNameFontSize = 44f;
    public float countryTopOffset = -748f;
    public float countryFontSize = 24f;
    public float hintBottomOffset = 20f;
    public float hintFontSize = 26f;
    public float cardFlyOffDistance = 1500f;

    [Header("Route Layout")]
    public float routeLineWidth = 4f;
    public Vector2 markerSize = new Vector2(20f, 20f);
    public Vector2 markerLabelSize = new Vector2(240f, 34f);
    public Vector2 markerLabelOffset = new Vector2(0f, 28f);
    public float markerLabelFontSize = 18f;
    public Vector2 caravanSize = new Vector2(40f, 40f);

    public TravelDistanceBand DistanceBandFor(int miles)
    {
        if (miles <= nearMaxMiles) return TravelDistanceBand.Near;
        if (miles <= mediumMaxMiles) return TravelDistanceBand.Medium;
        return TravelDistanceBand.Far;
    }

    public string LabelFor(TravelDistanceBand band) => band switch
    {
        TravelDistanceBand.Near => nearLabel,
        TravelDistanceBand.Medium => mediumLabel,
        TravelDistanceBand.Far => farLabel,
        _ => band.ToString()
    };

    private void OnValidate()
    {
        nearMaxMiles = Mathf.Max(0, nearMaxMiles);
        mediumMaxMiles = Mathf.Max(nearMaxMiles, mediumMaxMiles);
        resetDelayAfterSubmit = Mathf.Max(0f, resetDelayAfterSubmit);
        swipeDistanceThreshold = Mathf.Max(0f, swipeDistanceThreshold);
    }
}
