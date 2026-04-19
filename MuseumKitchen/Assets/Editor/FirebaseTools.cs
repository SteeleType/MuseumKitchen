using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Editor utilities for the Firebase-backed Potluck pipeline.
/// 给 Firebase 模式提供编辑器工具菜单。
/// </summary>
public static class FirebaseTools
{
    private const string ConfigAssetPath = "Assets/Settings/FirebaseConfig.asset";

    [MenuItem("Tools/Firebase/Clear Potluck Database")]
    public static void ClearPotluckDatabase()
    {
        var cfg = AssetDatabase.LoadAssetAtPath<FirebaseConfig>(ConfigAssetPath);
        if (cfg == null)
        {
            Debug.LogError($"[FirebaseTools] Could not load FirebaseConfig at {ConfigAssetPath}");
            return;
        }

        if (!EditorUtility.DisplayDialog(
                "Clear Potluck Database?",
                $"Send DELETE to:\n{cfg.PotluckEndpoint}\n\nAll submissions will be erased. Continue?",
                "Erase", "Cancel"))
            return;

        // Fire-and-forget delete via UnityWebRequest. Editor coroutines aren't first-class,
        // so we just dispatch and poll inline.
        var req = UnityWebRequest.Delete(cfg.PotluckEndpoint);
        var op = req.SendWebRequest();
        EditorApplication.update += Poll;

        void Poll()
        {
            if (!op.isDone) return;
            EditorApplication.update -= Poll;
            if (req.result == UnityWebRequest.Result.Success)
                Debug.Log($"[FirebaseTools] Database cleared at {cfg.PotluckEndpoint}");
            else
                Debug.LogError($"[FirebaseTools] Clear failed: {req.error}");
            req.Dispose();
        }
    }

    [MenuItem("Tools/Firebase/Print Config")]
    public static void PrintConfig()
    {
        var cfg = AssetDatabase.LoadAssetAtPath<FirebaseConfig>(ConfigAssetPath);
        if (cfg == null) { Debug.LogError("FirebaseConfig not found at " + ConfigAssetPath); return; }
        Debug.Log($"[FirebaseTools] Database URL: {cfg.databaseUrl}\nPotluck endpoint: {cfg.PotluckEndpoint}");
    }
}
