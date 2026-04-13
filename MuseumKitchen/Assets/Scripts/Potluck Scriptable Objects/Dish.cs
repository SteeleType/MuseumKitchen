using UnityEngine;
using UnityEngine.UIElements;

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
}


