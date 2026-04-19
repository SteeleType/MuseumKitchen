using UnityEngine;

[CreateAssetMenu(fileName = "Dish", menuName = "Scriptable Objects/Dish")]
public class Dish : ScriptableObject
{
    [SerializeField] private string dishName;
    [SerializeField] private Sprite dishSprite;
    [SerializeField] private string countryOfOrigin;
    [SerializeField] private Region region;
    [SerializeField] private Spice spice;
    [SerializeField] private CookingMethod cookingMethod;
    [SerializeField] private int distanceTraveled;
    [SerializeField] private SpiceOrigin spiceOrigin;

    public string DishName => string.IsNullOrEmpty(dishName) ? name : dishName;
    public Sprite DishSprite => dishSprite;
    public string CountryOfOrigin => countryOfOrigin;
    public Region Region => region;
    public Spice Spice => spice;
    public CookingMethod CookingMethod => cookingMethod;
    public int DistanceTraveledMiles => distanceTraveled;
    public SpiceOrigin SpiceOrigin => spiceOrigin;

    private void OnEnable()
    {
        // Keep spiceOrigin in sync with spice; SpiceManager.AddSpiceOrigin no longer throws.
        spiceOrigin = SpiceManager.AddSpiceOrigin(spice);
    }
}
