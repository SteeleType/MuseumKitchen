using System;

/// <summary>
/// Network payload sent from a client (Kitchen) to the big screen.
/// Schema v2: built from a Dish SO selection (Region + Spice + CookingMethod) plus a chef name.
/// </summary>
[Serializable]
public class PotluckData
{
    // Chef-facing identity / 厨师名（玩家输入或随机生成）
    public string clientId;

    // Dish identity / 菜品身份
    public string dishName;
    public string countryOfOrigin;

    // The 3 selectors the player chose / 玩家选择的三个维度（以 string 形式存，跨端简单）
    public string region;
    public string spice;
    public string cookingMethod;

    // Spice trade narrative / 香料叙事
    public string spiceOrigin;
    public int distanceMiles;

    // Reference to the Dish SO asset name (for sprite lookup on the big screen) / 大屏端用名字反查 sprite
    public string dishAssetName;
}
