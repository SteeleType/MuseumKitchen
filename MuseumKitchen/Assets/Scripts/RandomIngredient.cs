using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class RandomIngredient : MonoBehaviour
{
    [SerializeField] TextMeshPro fillingName;
    [SerializeField] private TextMeshPro wrappingName;
    [SerializeField] private TextMeshPro cookingMethodName;
    [SerializeField] private TextMeshPro dishMade;

    [SerializeField] private GameObject fillingPrefab;
    [SerializeField] private GameObject wrappingPrefab;
    [SerializeField] private GameObject cookingMethodPrefab;
    [SerializeField] private GameObject dishMadePrefab;
    
    [SerializeField] List<Ingredient> fillingList = new List<Ingredient>();
    [SerializeField] List<Ingredient> wrappingList = new List<Ingredient>();
    [SerializeField] List<Ingredient> cookingMethodList = new List<Ingredient>();
    [SerializeField] private List<Dumpling> dishMadeList = new List<Dumpling>();

    public void FillingUpdate()
    {
       Ingredient currentFilling = fillingList[Random.Range(0, fillingList.Count)];
       fillingName.text = currentFilling.ingredientName;
       fillingPrefab.GetComponent<SpriteRenderer>().sprite = currentFilling.ingredientSprite;
    }

    public void WrappingMethod()
    {
        Ingredient currentWrapping = wrappingList[Random.Range(0, wrappingList.Count)];
        wrappingName.text = currentWrapping.ingredientName;
        wrappingPrefab.GetComponent<SpriteRenderer>().sprite = currentWrapping.ingredientSprite;
    }
    public void CookingMethod()
    {
        Ingredient currentCooking = cookingMethodList[Random.Range(0, cookingMethodList.Count)];
        cookingMethodName.text = currentCooking.ingredientName;
        cookingMethodPrefab.GetComponent<SpriteRenderer>().sprite = currentCooking.ingredientSprite;
    }

  
}
