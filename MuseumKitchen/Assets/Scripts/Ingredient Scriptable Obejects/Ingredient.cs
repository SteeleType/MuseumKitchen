using UnityEngine;
public enum IngredientType
{
   Filling,
   Wrapping,
   CookingMethod
}
[CreateAssetMenu(fileName = "Ingredient", menuName = "Scriptable Objects/Ingredient")]

public class Ingredient : ScriptableObject
{
 
   
   [Header("Ingredient Properties")]
   [SerializeField] string ingredientName;
   [SerializeField] private Sprite ingredientSprite;
   [SerializeField] private IngredientType ingredientType;
}
