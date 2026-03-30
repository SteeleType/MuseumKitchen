using System.Collections.Generic;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(MuseumLanSender))]
public class RandomIngredient : MonoBehaviour
{
    [Header("UI Reference - Text / UI 文本引用")]
    [SerializeField] private TextMeshPro fillingName;
    [SerializeField] private TextMeshPro wrappingName;
    [SerializeField] private TextMeshPro cookingMethodName;
    [SerializeField] private TextMeshPro dishMade;

    [Header("UI Reference - Visuals / UI 视觉引用")]
    [SerializeField] private GameObject fillingPrefab;
    [SerializeField] private GameObject wrappingPrefab;
    [SerializeField] private GameObject cookingMethodPrefab;
    [SerializeField] private GameObject dishMadePrefab;
    
    [Header("Configuration Database / 配置数据库")]
    [SerializeField] private List<Ingredient> fillingList = new List<Ingredient>();
    [SerializeField] private List<Ingredient> wrappingList = new List<Ingredient>();
    [SerializeField] private List<Ingredient> cookingMethodList = new List<Ingredient>();
    [SerializeField] private List<Dumpling> dishMadeList = new List<Dumpling>();

    // Store the ingredients currently selected by the user
    // 保存当前用户选到的材料
    private Ingredient currentSelectedFilling;
    private Ingredient currentSelectedWrapping;
    private Ingredient currentSelectedCooking;

    private MuseumLanSender lanSender;

    private void Awake()
    {
        // Automatically get the Sender module attached to the same GameObject
        // 自动获取挂在同一个物体上的 Sender 模块
        lanSender = GetComponent<MuseumLanSender>();
    }

    public void FillingUpdate()
    {
        if (fillingList.Count == 0) return;
        currentSelectedFilling = fillingList[Random.Range(0, fillingList.Count)];
        
        if (fillingName != null) fillingName.text = currentSelectedFilling.ingredientName;
        if (fillingPrefab != null) fillingPrefab.GetComponent<SpriteRenderer>().sprite = currentSelectedFilling.ingredientSprite;
    }

    public void WrappingMethod()
    {
        if (wrappingList.Count == 0) return;
        currentSelectedWrapping = wrappingList[Random.Range(0, wrappingList.Count)];
        
        if (wrappingName != null) wrappingName.text = currentSelectedWrapping.ingredientName;
        if (wrappingPrefab != null) wrappingPrefab.GetComponent<SpriteRenderer>().sprite = currentSelectedWrapping.ingredientSprite;
    }

    public void CookingMethod()
    {
        if (cookingMethodList.Count == 0) return;
        currentSelectedCooking = cookingMethodList[Random.Range(0, cookingMethodList.Count)];
        
        if (cookingMethodName != null) cookingMethodName.text = currentSelectedCooking.ingredientName;
        if (cookingMethodPrefab != null) cookingMethodPrefab.GetComponent<SpriteRenderer>().sprite = currentSelectedCooking.ingredientSprite;
    }

    // New: Send to the big screen (Attach this method to a "Serve / 完成制作" button)
    // 新增：发送给大屏幕 (可以把这个方法挂在一个 "Serve / 完成制作" 按钮上)
    public void SubmitPotluck()
    {
        if (currentSelectedFilling == null || currentSelectedWrapping == null || currentSelectedCooking == null)
        {
            Debug.LogWarning("Missing ingredients! Cannot submit. / 还没选齐材料，无法提交！");
            return;
        }

        PotluckData data = new PotluckData
        {
            fillingName = currentSelectedFilling.ingredientName,
            wrappingName = currentSelectedWrapping.ingredientName,
            cookingMethodName = currentSelectedCooking.ingredientName
        };

        // Send to the server / 发送给服务端大屏幕
        lanSender.SendDumplingData(data);
        
        // Output English text to console or UI upon successful sending
        // 发送完成后输出日志
        Debug.Log("Cooking complete! Sending to the Potluck Display... / 烹饪完成！发往大屏幕!");
        
        // Optional: Reset selections if you want the user to play again immediately
        // 如果想让用户再玩一次，可以加上数据清理或延时复位
        // ResetSelection(); 
    }
}
