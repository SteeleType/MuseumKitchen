using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Loads every Dish SO under Resources/Dishes once and exposes lookup helpers
/// for the cascading (Region → Spice → CookingMethod) selector and post-submit reverse lookup.
/// 一次性加载 Resources/Dishes 下所有 Dish SO，提供级联选择和反查 API。
/// </summary>
public static class DishDatabase
{
    private static List<Dish> _all;

    public static IReadOnlyList<Dish> All
    {
        get
        {
            if (_all == null)
            {
                var loaded = Resources.LoadAll<Dish>("Dishes");
                _all = loaded != null ? new List<Dish>(loaded) : new List<Dish>();
                Debug.Log($"[DishDatabase] Loaded {_all.Count} dishes from Resources/Dishes.");
            }
            return _all;
        }
    }

    /// <summary>Regions that have at least one dish.</summary>
    public static IEnumerable<Region> AvailableRegions() =>
        All.Select(d => d.Region).Distinct();

    /// <summary>Spices that appear in at least one dish of the given Region.</summary>
    public static IEnumerable<Spice> AvailableSpicesForRegion(Region r) =>
        All.Where(d => d.Region == r).Select(d => d.Spice).Distinct();

    /// <summary>CookingMethods that appear in at least one dish of the given Region+Spice.</summary>
    public static IEnumerable<CookingMethod> AvailableCookingForRegionSpice(Region r, Spice s) =>
        All.Where(d => d.Region == r && d.Spice == s).Select(d => d.CookingMethod).Distinct();

    /// <summary>Find first dish matching all three; null if none.</summary>
    public static Dish Find(Region r, Spice s, CookingMethod c) =>
        All.FirstOrDefault(d => d.Region == r && d.Spice == s && d.CookingMethod == c);

    /// <summary>Find by asset name (used by big screen to recover the SO from PotluckData).</summary>
    public static Dish FindByName(string assetName) =>
        string.IsNullOrEmpty(assetName) ? null : All.FirstOrDefault(d => d.name == assetName);
}
