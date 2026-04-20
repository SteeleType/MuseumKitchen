using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// EditorWindow with two modes:
///   • Points  — drag 5 Region centers + 5 Spice Origin markers on the map.
///   • Routes  — pick (Origin → Region), add/drag/delete waypoints to define caravan path.
///                Live-previews a Catmull-Rom spline through origin → waypoints → destination.
/// 在地图上直接拖点位 + 编辑商队路径的 EditorWindow。
///
/// Usage: Tools → Map Coordinates Editor
/// </summary>
public class MapCoordinatesEditorWindow : EditorWindow
{
    private MapCoordinatesConfig _config;
    private Texture2D _mapTex;
    private Sprite _mapSprite;

    private const float DotRadius = 8f;
    private const float WPRadius  = 7f;
    private const float HitRadius = 14f;

    private enum Mode { Points, Routes }
    private Mode _mode = Mode.Points;

    // Points-mode drag state
    private enum DragKind { None, Region, Origin, Waypoint }
    private DragKind _dragKind = DragKind.None;
    private int _dragIdx = -1;

    // Routes-mode selection
    private SpiceOrigin _routeFrom = SpiceOrigin.India;
    private Region _routeTo = Region.Europe;

    [MenuItem("Tools/Map Coordinates Editor")]
    public static void Open()
    {
        var w = GetWindow<MapCoordinatesEditorWindow>("Map Coords");
        w.minSize = new Vector2(800, 700);
    }

    private void OnEnable()
    {
        if (_config == null)
            _config = AssetDatabase.LoadAssetAtPath<MapCoordinatesConfig>("Assets/Resources/MapCoordinates.asset");
    }

    private void OnGUI()
    {
        // Header row: assets
        EditorGUILayout.BeginHorizontal();
        _config = (MapCoordinatesConfig)EditorGUILayout.ObjectField("Coords SO", _config, typeof(MapCoordinatesConfig), false);
        var pickedSprite = (Sprite)EditorGUILayout.ObjectField("Map Sprite", _mapSprite, typeof(Sprite), false);
        if (pickedSprite != _mapSprite)
        {
            _mapSprite = pickedSprite;
            _mapTex = pickedSprite != null ? pickedSprite.texture : null;
        }
        EditorGUILayout.EndHorizontal();

        if (_config == null)
        {
            EditorGUILayout.HelpBox("Assign a MapCoordinatesConfig asset (try Resources/MapCoordinates.asset).", MessageType.Warning);
            return;
        }
        if (_mapTex == null)
        {
            EditorGUILayout.HelpBox("Drag the World map sprite into 'Map Sprite' above.", MessageType.Info);
            if (GUILayout.Button("Auto-find sprite (uses DishComposer.mapSprite from open scene)"))
                TryAutoFindSprite();
            return;
        }

        // Mode toggle
        EditorGUILayout.BeginHorizontal();
        _mode = (Mode)GUILayout.Toolbar((int)_mode, new[] { "Points", "Routes" });
        EditorGUILayout.EndHorizontal();

        // Routes-mode controls
        if (_mode == Mode.Routes)
        {
            EditorGUILayout.BeginHorizontal();
            _routeFrom = (SpiceOrigin)EditorGUILayout.EnumPopup("Origin", _routeFrom);
            _routeTo   = (Region)EditorGUILayout.EnumPopup("Destination", _routeTo);
            if (GUILayout.Button("Clear waypoints", GUILayout.Width(120)))
            {
                var route = _config.GetOrCreateRoute(_routeFrom, _routeTo);
                route.waypoints.Clear();
                EditorUtility.SetDirty(_config);
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.HelpBox(
                "Click on empty map = add waypoint at end.  Drag a waypoint to move.  Right-click a waypoint to delete.\n" +
                "Path is auto-smoothed (Catmull-Rom spline).",
                MessageType.None);
        }
        else
        {
            EditorGUILayout.HelpBox(
                "Drag orange dots = Region centers.  Drag cyan dots = Spice Origins.\n" +
                "Coordinates are normalized 0..1; (0,0) = bottom-left.",
                MessageType.None);
        }

        // Map drawing area
        float availW = position.width - 20;
        float availH = position.height - (_mode == Mode.Routes ? 180 : 140);
        float texAspect = (float)_mapTex.width / _mapTex.height;
        float drawW, drawH;
        if (availW / availH > texAspect) { drawH = availH; drawW = drawH * texAspect; }
        else                              { drawW = availW; drawH = drawW / texAspect; }

        Rect mapRect = GUILayoutUtility.GetRect(drawW, drawH, GUILayout.Width(drawW), GUILayout.Height(drawH));
        mapRect.x = (position.width - drawW) * 0.5f;

        GUI.DrawTexture(mapRect, _mapTex, ScaleMode.StretchToFill);

        if (_mode == Mode.Points) DrawPointsMode(mapRect);
        else                       DrawRoutesMode(mapRect);

        EditorGUILayout.Space();
        if (GUILayout.Button("Save Asset"))
        {
            EditorUtility.SetDirty(_config);
            AssetDatabase.SaveAssets();
        }
    }

    // ───────────────────── Points mode ─────────────────────

    private void DrawPointsMode(Rect mapRect)
    {
        Event e = Event.current;

        for (int i = 0; i < _config.regions.Count; i++)
        {
            var pt = _config.regions[i];
            Vector2 screen = NormToScreen(pt.normalizedPos, mapRect);
            DrawDot(screen, new Color(1f, 0.55f, 0.15f), pt.region.ToString(), DotRadius);
            HandlePointDrag(e, screen, mapRect, DragKind.Region, i);
        }
        for (int i = 0; i < _config.spiceOrigins.Count; i++)
        {
            var pt = _config.spiceOrigins[i];
            Vector2 screen = NormToScreen(pt.normalizedPos, mapRect);
            DrawDot(screen, new Color(0.4f, 0.85f, 1f), pt.origin.ToString(), DotRadius);
            HandlePointDrag(e, screen, mapRect, DragKind.Origin, i);
        }

        if (e.type == EventType.MouseUp) { _dragKind = DragKind.None; _dragIdx = -1; }
        if (_dragKind != DragKind.None) Repaint();
    }

    private void HandlePointDrag(Event e, Vector2 dotScreenPos, Rect mapRect, DragKind kind, int idx)
    {
        if (e.type == EventType.MouseDown && e.button == 0
            && Vector2.Distance(e.mousePosition, dotScreenPos) <= HitRadius
            && _dragKind == DragKind.None)
        {
            _dragKind = kind; _dragIdx = idx;
            e.Use();
        }
        else if (e.type == EventType.MouseDrag && _dragKind == kind && _dragIdx == idx)
        {
            Vector2 newNorm = ScreenToNorm(e.mousePosition, mapRect);
            if (kind == DragKind.Region)
            {
                var p = _config.regions[idx]; p.normalizedPos = newNorm; _config.regions[idx] = p;
            }
            else
            {
                var p = _config.spiceOrigins[idx]; p.normalizedPos = newNorm; _config.spiceOrigins[idx] = p;
            }
            EditorUtility.SetDirty(_config);
            e.Use();
            Repaint();
        }
    }

    // ───────────────────── Routes mode ─────────────────────

    private void DrawRoutesMode(Rect mapRect)
    {
        Event e = Event.current;
        var route = _config.GetOrCreateRoute(_routeFrom, _routeTo);

        // Resolve origin/dest from existing point lists
        Vector2 originNorm = new Vector2(0.5f, 0.5f);
        Vector2 destNorm   = new Vector2(0.5f, 0.5f);
        _config.TryGetSpiceOrigin(_routeFrom, out originNorm);
        if (_config.TryGetRegion(_routeTo, out var pt)) destNorm = pt.normalizedPos;

        Vector2 originScr = NormToScreen(originNorm, mapRect);
        Vector2 destScr   = NormToScreen(destNorm, mapRect);

        // Draw origin (cyan, larger) and dest (orange, larger)
        DrawDot(originScr, new Color(0.4f, 0.85f, 1f), _routeFrom.ToString(), DotRadius + 2);
        DrawDot(destScr,   new Color(1f, 0.55f, 0.15f), _routeTo.ToString(),  DotRadius + 2);

        // Build screen-space control polyline and smooth-sample for preview
        var ctlScreen = new List<Vector2> { originScr };
        foreach (var w in route.waypoints) ctlScreen.Add(NormToScreen(w, mapRect));
        ctlScreen.Add(destScr);

        var samples = SampleCatmullRomScreen(ctlScreen, 16);
        DrawPolyline(samples, new Color(1f, 0.85f, 0.4f, 0.85f));

        // Waypoint dots + drag/delete
        for (int i = 0; i < route.waypoints.Count; i++)
        {
            Vector2 wScr = NormToScreen(route.waypoints[i], mapRect);
            DrawDot(wScr, new Color(1f, 1f, 1f), $"#{i + 1}", WPRadius);

            // Right-click on waypoint → delete
            if (e.type == EventType.MouseDown && e.button == 1
                && Vector2.Distance(e.mousePosition, wScr) <= HitRadius)
            {
                route.waypoints.RemoveAt(i);
                EditorUtility.SetDirty(_config);
                e.Use();
                Repaint();
                return;
            }

            // Left-click drag
            if (e.type == EventType.MouseDown && e.button == 0
                && Vector2.Distance(e.mousePosition, wScr) <= HitRadius
                && _dragKind == DragKind.None)
            {
                _dragKind = DragKind.Waypoint; _dragIdx = i;
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && _dragKind == DragKind.Waypoint && _dragIdx == i)
            {
                route.waypoints[i] = ScreenToNorm(e.mousePosition, mapRect);
                EditorUtility.SetDirty(_config);
                e.Use();
                Repaint();
            }
        }

        // Click on empty map (not on any waypoint) → add waypoint
        if (e.type == EventType.MouseDown && e.button == 0
            && _dragKind == DragKind.None
            && mapRect.Contains(e.mousePosition))
        {
            // Make sure not on origin/dest dots either
            if (Vector2.Distance(e.mousePosition, originScr) > HitRadius
             && Vector2.Distance(e.mousePosition, destScr)   > HitRadius)
            {
                route.waypoints.Add(ScreenToNorm(e.mousePosition, mapRect));
                EditorUtility.SetDirty(_config);
                e.Use();
                Repaint();
            }
        }

        if (e.type == EventType.MouseUp) { _dragKind = DragKind.None; _dragIdx = -1; }
        if (_dragKind != DragKind.None) Repaint();
    }

    // ───────────────────── Drawing helpers ─────────────────────

    private static Vector2 NormToScreen(Vector2 norm, Rect mapRect) =>
        new Vector2(mapRect.x + norm.x * mapRect.width,
                    mapRect.y + (1f - norm.y) * mapRect.height);

    private static Vector2 ScreenToNorm(Vector2 screen, Rect mapRect) =>
        new Vector2(Mathf.Clamp01((screen.x - mapRect.x) / mapRect.width),
                    Mathf.Clamp01(1f - (screen.y - mapRect.y) / mapRect.height));

    private void DrawDot(Vector2 center, Color color, string label, float radius)
    {
        var ringRect = new Rect(center.x - radius, center.y - radius, radius * 2, radius * 2);
        EditorGUI.DrawRect(ringRect, Color.white);
        EditorGUI.DrawRect(new Rect(ringRect.x + 2, ringRect.y + 2, ringRect.width - 4, ringRect.height - 4), color);
        var style = new GUIStyle(EditorStyles.whiteMiniLabel) { alignment = TextAnchor.MiddleLeft };
        GUI.Label(new Rect(center.x + radius + 4, center.y - 8, 200, 16), label, style);
    }

    private static void DrawPolyline(List<Vector2> pts, Color color)
    {
        if (pts == null || pts.Count < 2) return;
        Handles.BeginGUI();
        Handles.color = color;
        for (int i = 1; i < pts.Count; i++)
            Handles.DrawAAPolyLine(3.5f, pts[i - 1], pts[i]);
        Handles.color = Color.white;
        Handles.EndGUI();
    }

    private static List<Vector2> SampleCatmullRomScreen(List<Vector2> ctl, int samplesPerSegment)
    {
        var result = new List<Vector2>();
        if (ctl == null || ctl.Count == 0) return result;
        if (ctl.Count == 1) { result.Add(ctl[0]); return result; }
        var p = new List<Vector2>(ctl.Count + 2);
        p.Add(ctl[0] + (ctl[0] - ctl[1]));
        p.AddRange(ctl);
        p.Add(ctl[ctl.Count - 1] + (ctl[ctl.Count - 1] - ctl[ctl.Count - 2]));
        result.Add(ctl[0]);
        for (int i = 0; i < ctl.Count - 1; i++)
        {
            for (int s = 1; s <= samplesPerSegment; s++)
            {
                float t = s / (float)samplesPerSegment;
                result.Add(CatmullRom(p[i], p[i + 1], p[i + 2], p[i + 3], t));
            }
        }
        return result;
    }

    private static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        float t2 = t * t, t3 = t2 * t;
        return 0.5f * (
            2f * p1 +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    private void TryAutoFindSprite()
    {
        var composer = FindObjectsByType<DishComposer>(FindObjectsSortMode.None).FirstOrDefault();
        if (composer != null)
        {
            var so = new SerializedObject(composer);
            var sp = so.FindProperty("mapSprite");
            if (sp != null && sp.objectReferenceValue is Sprite s)
            {
                _mapSprite = s; _mapTex = s.texture; Repaint(); return;
            }
        }
        Debug.Log("[MapCoordinatesEditorWindow] Could not auto-find. Drag the sprite into 'Map Sprite' field.");
    }
}
