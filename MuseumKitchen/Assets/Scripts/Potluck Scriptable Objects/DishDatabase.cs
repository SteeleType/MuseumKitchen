using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum TravelDistanceBand
{
    Near,
    Medium,
    Far
}

/// <summary>
/// Loads every Dish SO under Resources/Dishes once and exposes lookup helpers
/// for the cascading selector and post-submit reverse lookup.
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

    public static IEnumerable<Region> AvailableRegions() =>
        All.Select(d => d.Region).Distinct();

    public static IEnumerable<Spice> AvailableSpices() =>
        All.Select(d => d.Spice).Distinct();

    public static IEnumerable<Spice> AvailableSpicesForRegion(Region r) =>
        All.Where(d => d.Region == r).Select(d => d.Spice).Distinct();

    public static IEnumerable<TravelDistanceBand> AvailableTravelBandsForSpice(Spice s) =>
        AvailableTravelBandsForSpice(s, 1500, 3500);

    public static IEnumerable<TravelDistanceBand> AvailableTravelBandsForSpice(
        Spice s, int nearMaxMiles, int mediumMaxMiles) =>
        All.Where(d => d.Spice == s)
            .Select(d => DistanceBandFor(d.DistanceTraveledMiles, nearMaxMiles, mediumMaxMiles))
            .Distinct();

    public static IReadOnlyList<Dish> DestinationCandidates(Spice s, TravelDistanceBand band) =>
        DestinationCandidates(s, band, 1500, 3500);

    public static IReadOnlyList<Dish> DestinationCandidates(
        Spice s, TravelDistanceBand band, int nearMaxMiles, int mediumMaxMiles) =>
        All.Where(d => d.Spice == s
                       && DistanceBandFor(d.DistanceTraveledMiles, nearMaxMiles, mediumMaxMiles) == band)
            .GroupBy(d => d.CountryOfOrigin)
            .Select(g => g.OrderBy(d => d.name).First())
            .OrderBy(d => d.CountryOfOrigin)
            .ToList();

    public static IEnumerable<CookingMethod> AvailableCookingForRegionSpice(Region r, Spice s) =>
        All.Where(d => d.Region == r && d.Spice == s).Select(d => d.CookingMethod).Distinct();

    public static IEnumerable<CookingMethod> AvailableCookingForDestination(
        Spice s, TravelDistanceBand band, string country) =>
        AvailableCookingForDestination(s, band, country, 1500, 3500);

    public static IEnumerable<CookingMethod> AvailableCookingForDestination(
        Spice s, TravelDistanceBand band, string country, int nearMaxMiles, int mediumMaxMiles) =>
        All.Where(d => d.Spice == s
                       && DistanceBandFor(d.DistanceTraveledMiles, nearMaxMiles, mediumMaxMiles) == band
                       && d.CountryOfOrigin == country)
            .Select(d => d.CookingMethod)
            .Distinct();

    public static Dish Find(Region r, Spice s, CookingMethod c) =>
        All.FirstOrDefault(d => d.Region == r && d.Spice == s && d.CookingMethod == c);

    public static Dish FindForDestination(Spice s, TravelDistanceBand band, string country, CookingMethod c) =>
        FindForDestination(s, band, country, c, 1500, 3500);

    public static Dish FindForDestination(
        Spice s, TravelDistanceBand band, string country, CookingMethod c, int nearMaxMiles, int mediumMaxMiles) =>
        All.FirstOrDefault(d => d.Spice == s
                                && DistanceBandFor(d.DistanceTraveledMiles, nearMaxMiles, mediumMaxMiles) == band
                                && d.CountryOfOrigin == country
                                && d.CookingMethod == c);

    public static Dish FindByName(string assetName) =>
        string.IsNullOrEmpty(assetName) ? null : All.FirstOrDefault(d => d.name == assetName);

    public static TravelDistanceBand DistanceBandFor(int miles) =>
        DistanceBandFor(miles, 1500, 3500);

    public static TravelDistanceBand DistanceBandFor(int miles, int nearMaxMiles, int mediumMaxMiles)
    {
        if (miles <= nearMaxMiles) return TravelDistanceBand.Near;
        if (miles <= mediumMaxMiles) return TravelDistanceBand.Medium;
        return TravelDistanceBand.Far;
    }
}
