using UnityEngine;

public static class SpiceManager
{
    public static SpiceOrigin AddSpiceOrigin(Spice spice)
    {
        switch (spice)
        {
            case Spice.BlackPepper: return SpiceOrigin.India;
            case Spice.Cardomom:    return SpiceOrigin.India;
            case Spice.Cinnamon:    return SpiceOrigin.SriLanka;
            case Spice.Cloves:      return SpiceOrigin.Indonesia;
            case Spice.Cumin:       return SpiceOrigin.Mediterranean;
            case Spice.Saffron:     return SpiceOrigin.Iran;
            case Spice.Turmeric:    return SpiceOrigin.India;
            default:
                Debug.LogWarning($"[SpiceManager] Unmapped spice value '{spice}', defaulting to India.");
                return SpiceOrigin.India;
        }
    }
    
    public static SpiceFactoids GetSpiceFactoids(Spice spice)
    {
        switch (spice)
        {
            case Spice.BlackPepper: return new SpiceFactoids(
                "Once used as currency in ancient trade.",
                "Contains piperine, which enhances nutrient absorption.");

            case Spice.Cardomom: return new SpiceFactoids(
                "The third most expensive spice in the world.",
                "Used in Ayurvedic medicine for over 3,000 years.");

            case Spice.Cinnamon: return new SpiceFactoids(
                "Derived from the inner bark of Cinnamomum trees.",
                "Was once more valuable than gold in ancient Egypt.");

            case Spice.Cloves: return new SpiceFactoids(
                "Cloves are the dried flower buds of the clove tree.",
                "Contain eugenol, a natural antiseptic.");

            case Spice.Cumin: return new SpiceFactoids(
                "One of the most popular spices in the world.",
                "Seeds were found in ancient Egyptian tombs.");

            case Spice.Saffron: return new SpiceFactoids(
                "The most expensive spice by weight.",
                "Takes around 75,000 flowers to produce one pound.");

            case Spice.Turmeric: return new SpiceFactoids(
                "Used in Indian cooking for at least 4,000 years.",
                "Curcumin gives it its distinctive golden color.");

            default:
                Debug.LogWarning($"[SpiceManager] Unmapped spice value '{spice}', returning empty factoids.");
                return new SpiceFactoids("Unknown factoid.", "Unknown factoid.");
        }
    }
}