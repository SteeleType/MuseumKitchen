using UnityEngine;

public static class SpiceManager
{
    public static SpiceOrigin AddSpiceOrigin(Spice spice)
    {
        switch (spice)
        {
            case Spice.BlackPepper: return SpiceOrigin.India;
            case Spice.Cardomom: return SpiceOrigin.India;
            case Spice.Cinnamon: return SpiceOrigin.SriLanka;
            case Spice.Cloves: return SpiceOrigin.Indonesia;
            case Spice.Cumin: return SpiceOrigin.Mediterranean;
            case Spice.Saffron: return SpiceOrigin.Iran;
            case Spice.Turmeric: return SpiceOrigin.India;
            default: throw new System.Exception("Spice not found");
        }
    }
}