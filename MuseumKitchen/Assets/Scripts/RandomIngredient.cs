using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class RandomIngredient : MonoBehaviour
{
    [SerializeField] TextMeshPro ingredientName;

    [SerializeField] private GameObject ingredientPrefab;
    [SerializeField] List<Ingredient> fillingList = new List<Ingredient>();
    [SerializeField] List<Ingredient> wrappingList = new List<Ingredient>();
    [SerializeField] List<Ingredient> cookingMethodList = new List<Ingredient>();

    public void FillingUpdate()
    {
       Ingredient currentFilling = fillingList[Random.Range(0, fillingList.Count)];
       ingredientName.text = currentFilling.ingredientName;
       ingredientPrefab.GetComponent<SpriteRenderer>().sprite = currentFilling.ingredientSprite;
    }

    public void CookingMethod()
    {
        Ingredient currentCooking = cookingMethodList[Random.Range(0, cookingMethodList.Count)];
        ingredientName.text = currentCooking.ingredientName;
        ingredientPrefab.GetComponent<SpriteRenderer>().sprite = currentCooking.ingredientSprite;
    }

    public void WrappingMethod()
    {
        Ingredient currentWrapping = wrappingList[Random.Range(0, wrappingList.Count)];
        ingredientName.text = currentWrapping.ingredientName;
        ingredientPrefab.GetComponent<SpriteRenderer>().sprite = currentWrapping.ingredientSprite;
    }
    
    
}
