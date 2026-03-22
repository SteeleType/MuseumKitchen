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
    
    
}
