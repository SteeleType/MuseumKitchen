using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// One-time editor utility to add BigScreen scene to Build Settings.
/// Run via menu: Tools > Add BigScreen to Build Settings
/// After running, you can safely delete this script.
/// </summary>
public class AddBigScreenToBuild
{
    [MenuItem("Tools/Add BigScreen to Build Settings")]
    public static void AddScene()
    {
        string bigScreenPath = "Assets/Scenes/BigScreen.unity";
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

        foreach (var s in scenes)
        {
            if (s.path == bigScreenPath)
            {
                UnityEngine.Debug.Log("[AddBigScreenToBuild] BigScreen already in Build Settings.");
                return;
            }
        }

        scenes.Add(new EditorBuildSettingsScene(bigScreenPath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        UnityEngine.Debug.Log($"[AddBigScreenToBuild] BigScreen added at index {scenes.Count - 1}");
    }
}
