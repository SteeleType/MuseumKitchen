using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Polls Firebase Realtime Database for new PotluckData submissions and routes them to PotluckManager.
/// Replaces the old UDP-based MuseumLanReceiver. Works on Standalone and WebGL.
/// 通过短轮询 Firebase RTDB 收新菜品；替代旧的 UDP MuseumLanReceiver；Standalone 与 WebGL 都能跑。
/// </summary>
public class FirebaseReceiver : MonoBehaviour
{
    [Tooltip("Firebase RTDB config asset. Required.\nFirebase 配置资源，必填。")]
    [SerializeField] private FirebaseConfig config;

    [Header("Polling / 轮询")]
    [Tooltip("Seconds between polls. 0.4 gives near-real-time without abusing the free tier.\n两次轮询间隔（秒），0.4 ≈ 接近实时且不爆免费额度。")]
    [SerializeField] private float pollInterval = 0.4f;

    [Tooltip("Max number of recent submissions to fetch each poll.\n每次拉取的最大条数。")]
    [SerializeField] private int fetchLimit = 50;

    [Tooltip("On the first poll, mark existing entries as 'seen' without animating them.\n首次轮询时把已有数据标记为已见，不回放历史动画。")]
    [SerializeField] private bool ignoreHistoryOnStart = true;

    [Header("Events / 事件")]
    public PotluckEvent OnDataReceived;

    private readonly HashSet<string> _seenIds = new HashSet<string>();
    private PotluckManager _autoLinkedManager;
    private bool _running;

    private void Start()
    {
        _autoLinkedManager = GetComponent<PotluckManager>();
        if (config == null)
        {
            Debug.LogError("[FirebaseReceiver] No FirebaseConfig assigned. Receiver disabled.");
            return;
        }
        _running = true;
        StartCoroutine(PollLoop());
    }

    private void OnDisable() => _running = false;

    private IEnumerator PollLoop()
    {
        bool firstPass = true;
        while (_running)
        {
            yield return PollOnce(firstPass);
            firstPass = false;
            yield return new WaitForSeconds(pollInterval);
        }
    }

    private IEnumerator PollOnce(bool firstPass)
    {
        string url = config.PotluckRecentEndpoint(fetchLimit);
        UnityWebRequest req = UnityWebRequest.Get(url);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning($"[FirebaseReceiver] GET failed: {req.error}\nURL: {url}");
            req.Dispose();
            yield break;
        }

        string text = req.downloadHandler.text;
        req.Dispose();

        if (string.IsNullOrEmpty(text) || text == "null") yield break;

        JObject obj;
        try { obj = JObject.Parse(text); }
        catch (Exception e)
        {
            Debug.LogWarning($"[FirebaseReceiver] JSON parse failed: {e.Message}\n{text}");
            yield break;
        }

        // Firebase auto-ids are time-orderable; sort to play in submission order.
        // Firebase 自动 id 按时间可排序，按 id 排序得到提交先后。
        foreach (var prop in obj.Properties().OrderBy(p => p.Name))
        {
            string id = prop.Name;
            if (_seenIds.Contains(id)) continue;
            _seenIds.Add(id);

            if (firstPass && ignoreHistoryOnStart) continue;

            PotluckData data = null;
            try { data = JsonUtility.FromJson<PotluckData>(prop.Value.ToString(Newtonsoft.Json.Formatting.None)); }
            catch (Exception e) { Debug.LogWarning($"[FirebaseReceiver] Bad item {id}: {e.Message}"); }
            if (data == null) continue;

            OnDataReceived?.Invoke(data);
            if (_autoLinkedManager != null) _autoLinkedManager.OnPotluckDataReceived(data);
        }
    }
}
