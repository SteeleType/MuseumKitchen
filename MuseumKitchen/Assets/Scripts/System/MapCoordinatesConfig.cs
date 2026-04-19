using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Maps each Region and SpiceOrigin to a normalized (0-1) point on the world map sprite,
/// plus a zoom factor used when focusing on a Region.
/// 给每个 Region 和 SpiceOrigin 在地图 sprite 上的归一化坐标 (0-1)，以及聚焦时的缩放倍数。
///
/// Coordinates are in normalized sprite UV-style space:
///   (0, 0) = bottom-left of the map image
///   (1, 1) = top-right
/// 这样切换地图素材也不用重新填坐标。
/// </summary>
[CreateAssetMenu(fileName = "MapCoordinates", menuName = "Museum Kitchen/Map Coordinates")]
public class MapCoordinatesConfig : ScriptableObject
{
    [Serializable] public struct RegionPoint   { public Region region; public Vector2 normalizedPos; public float zoom; }
    [Serializable] public struct SpicePoint    { public SpiceOrigin origin; public Vector2 normalizedPos; }

    [Tooltip("Zoom factor when focused on a Region (e.g. 2.5 = 2.5x).\n聚焦 Region 时的缩放倍数。")]
    public float defaultRegionZoom = 2.5f;

    [Header("Region Centers (normalized 0..1)")]
    public List<RegionPoint> regions = new List<RegionPoint>
    {
        new RegionPoint { region = Region.Europe,      normalizedPos = new Vector2(0.50f, 0.78f), zoom = 2.5f },
        new RegionPoint { region = Region.MiddleEast,  normalizedPos = new Vector2(0.58f, 0.62f), zoom = 2.5f },
        new RegionPoint { region = Region.NorthAfrica, normalizedPos = new Vector2(0.50f, 0.58f), zoom = 2.5f },
        new RegionPoint { region = Region.SouthAsia,   normalizedPos = new Vector2(0.69f, 0.55f), zoom = 2.5f },
        new RegionPoint { region = Region.Asia,        normalizedPos = new Vector2(0.78f, 0.62f), zoom = 2.5f },
    };

    [Header("Spice Origins (normalized 0..1)")]
    public List<SpicePoint> spiceOrigins = new List<SpicePoint>
    {
        new SpicePoint { origin = SpiceOrigin.India,         normalizedPos = new Vector2(0.69f, 0.55f) },
        new SpicePoint { origin = SpiceOrigin.SriLanka,      normalizedPos = new Vector2(0.71f, 0.48f) },
        new SpicePoint { origin = SpiceOrigin.Iran,          normalizedPos = new Vector2(0.62f, 0.62f) },
        new SpicePoint { origin = SpiceOrigin.Indonesia,     normalizedPos = new Vector2(0.80f, 0.45f) },
        new SpicePoint { origin = SpiceOrigin.Mediterranean, normalizedPos = new Vector2(0.54f, 0.62f) },
    };

    public bool TryGetRegion(Region r, out RegionPoint point)
    {
        foreach (var p in regions) if (p.region == r) { point = p; return true; }
        point = default; return false;
    }

    public bool TryGetSpiceOrigin(SpiceOrigin o, out Vector2 normalizedPos)
    {
        foreach (var p in spiceOrigins) if (p.origin == o) { normalizedPos = p.normalizedPos; return true; }
        normalizedPos = new Vector2(0.5f, 0.5f); return false;
    }
}
