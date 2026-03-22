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
   [SerializeField] public string ingredientName;
   [SerializeField] public Sprite ingredientSprite;
   [SerializeField] public IngredientType ingredientType;
}
