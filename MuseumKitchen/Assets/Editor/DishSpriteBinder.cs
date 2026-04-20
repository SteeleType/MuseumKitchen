using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor-only utility: scan a folder of dish images and auto-bind each one to the matching Dish SO.
/// Match rule: image file basename (without extension) equals Dish .asset file name (case-insensitive).
/// 一键把 Resources/Dishes/Sprites 下的图片按文件名匹配并绑到 Dish SO 的 dishSprite 字段。
/// </summary>
public static class DishSpriteBinder
{
    private const string DefaultSpriteFolder = "Assets/Resources/Dishes/Sprites";
    private const string DefaultDishFolder   = "Assets/Resources/Dishes";

    [MenuItem("Tools/Dishes/Auto-Bind Sprites from Resources/Dishes/Sprites")]
    public static void BindFromDefault()
    {
        Bind(DefaultSpriteFolder, DefaultDishFolder, requireMatch: false);
    }

    [MenuItem("Tools/Dishes/Auto-Bind Sprites from Art/AI_Tmp")]
    public static void BindFromAITmp()
    {
        Bind("Assets/Art/AI_Tmp", DefaultDishFolder, requireMatch: false);
    }

    [MenuItem("Tools/Dishes/Auto-Bind Sprites from… (pick folder)")]
    public static void BindFromPicked()
    {
        string picked = EditorUtility.OpenFolderPanel("Pick folder containing dish images", DefaultSpriteFolder, "");
        if (string.IsNullOrEmpty(picked)) return;

        // OpenFolderPanel returns absolute; convert to project-relative.
        string projectRoot = Path.GetFullPath(Application.dataPath + "/..").Replace('\\', '/');
        string normalized = picked.Replace('\\', '/');
        if (!normalized.StartsWith(projectRoot, System.StringComparison.OrdinalIgnoreCase))
        {
            EditorUtility.DisplayDialog("Wrong folder", "Pick a folder *inside* this Unity project.", "OK");
            return;
        }
        string rel = "Assets" + normalized.Substring(projectRoot.Length + "/Assets".Length);
        Bind(rel, DefaultDishFolder, requireMatch: false);
    }

    private static void Bind(string spriteFolder, string dishFolder, bool requireMatch)
    {
        if (!AssetDatabase.IsValidFolder(spriteFolder))
        {
            EditorUtility.DisplayDialog("Folder missing",
                $"Sprite folder not found:\n{spriteFolder}\n\nCreate it and drop your images there first.", "OK");
            return;
        }

        // 1. Load all Dish SOs
        var dishGuids = AssetDatabase.FindAssets("t:Dish", new[] { dishFolder });
        var dishMap = new Dictionary<string, (Dish dish, string path)>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var g in dishGuids)
        {
            var p = AssetDatabase.GUIDToAssetPath(g);
            var d = AssetDatabase.LoadAssetAtPath<Dish>(p);
            if (d == null) continue;
            dishMap[d.name] = (d, p);
        }
        if (dishMap.Count == 0)
        {
            EditorUtility.DisplayDialog("No dishes",
                $"No Dish SOs found under:\n{dishFolder}", "OK");
            return;
        }

        // 2. Load all Sprites in the folder (recursive)
        var spriteGuids = AssetDatabase.FindAssets("t:Sprite", new[] { spriteFolder });
        var spriteMap = new Dictionary<string, Sprite>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var g in spriteGuids)
        {
            var p = AssetDatabase.GUIDToAssetPath(g);
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(p);
            if (s == null) continue;
            // basename without extension
            var key = Path.GetFileNameWithoutExtension(p);
            spriteMap[key] = s;
        }

        int bound = 0, missing = 0, alreadyOk = 0;
        var report = new System.Text.StringBuilder();

        // 3. Match
        foreach (var kv in dishMap)
        {
            var dishName = kv.Key;
            var (dish, path) = kv.Value;

            if (!spriteMap.TryGetValue(dishName, out var sprite))
            {
                missing++;
                report.AppendLine($"  MISSING  {dishName}  (no '{dishName}.png' / .jpg in sprite folder)");
                continue;
            }

            var so = new SerializedObject(dish);
            var prop = so.FindProperty("dishSprite");
            if (prop == null)
            {
                report.AppendLine($"  ERROR    {dishName}  (no 'dishSprite' property)");
                continue;
            }

            if (prop.objectReferenceValue == sprite)
            {
                alreadyOk++;
                continue;
            }

            prop.objectReferenceValue = sprite;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(dish);
            bound++;
            report.AppendLine($"  BOUND    {dishName}  ←  {AssetDatabase.GetAssetPath(sprite)}");
        }

        if (bound > 0) AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[DishSpriteBinder] Bound {bound}, already-ok {alreadyOk}, missing {missing} of {dishMap.Count} dishes.\n{report}");

        EditorUtility.DisplayDialog("Done",
            $"Bound: {bound}\nAlready linked: {alreadyOk}\nMissing image: {missing}\n\nSee Console for the per-dish report.",
            "OK");
    }

    [MenuItem("Tools/Dishes/Clear All dishSprite Bindings")]
    public static void ClearAll()
    {
        if (!EditorUtility.DisplayDialog("Clear all dishSprite?",
                "This will set dishSprite = null on EVERY Dish SO. Continue?", "Clear", "Cancel"))
            return;

        var guids = AssetDatabase.FindAssets("t:Dish", new[] { DefaultDishFolder });
        int cleared = 0;
        foreach (var g in guids)
        {
            var d = AssetDatabase.LoadAssetAtPath<Dish>(AssetDatabase.GUIDToAssetPath(g));
            if (d == null) continue;
            var so = new SerializedObject(d);
            var p = so.FindProperty("dishSprite");
            if (p != null && p.objectReferenceValue != null)
            {
                p.objectReferenceValue = null;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(d);
                cleared++;
            }
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[DishSpriteBinder] Cleared dishSprite on {cleared} dishes.");
    }
}
