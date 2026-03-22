using UnityEngine;

[CreateAssetMenu(fileName = "Dumpling", menuName = "Scriptable Objects/Dumpling")]
public class Dumpling : ScriptableObject
{
    [Header("Dumpling Components")]
    [SerializeField] private string dumplingName;
    [SerializeField] private Ingredient dumplingFilling;
    [SerializeField] private Ingredient dumplingWrapping;
    [SerializeField] private Ingredient cookingMethod;
}
