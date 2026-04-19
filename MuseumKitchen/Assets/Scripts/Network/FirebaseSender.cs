using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Sends a PotluckData JSON to Firebase Realtime Database via REST.
/// Replaces the old UDP-based MuseumLanSender so the client works on WebGL/iPad.
/// 通过 REST 把 PotluckData 写入 Firebase RTDB；替代旧的 UDP MuseumLanSender，使客户端能在 WebGL/iPad 上运行。
/// </summary>
public class FirebaseSender : MonoBehaviour
{
    [Tooltip("Firebase RTDB config asset. Required.\nFirebase 配置资源，必填。")]
    [SerializeField] private FirebaseConfig config;

    [Tooltip("If true, also auto-fills clientId from SystemInfo.deviceName when missing.\n勾选后未指定 clientId 会自动用设备名。")]
    [SerializeField] private bool autoFillClientId = true;

    public void SendDumplingData(PotluckData data)
    {
        if (config == null)
        {
            Debug.LogError("[FirebaseSender] No FirebaseConfig assigned. Cannot send.");
            return;
        }

        if (autoFillClientId && string.IsNullOrEmpty(data.clientId))
            data.clientId = SystemInfo.deviceName;

        StartCoroutine(PostRoutine(data));
    }

    private System.Collections.IEnumerator PostRoutine(PotluckData data)
    {
        string json = JsonUtility.ToJson(data);
        byte[] body = Encoding.UTF8.GetBytes(json);

        // POST appends a new auto-id child under /potluck.
        // POST 会在 /potluck 下创建一个自动 id 的新节点。
        using (var req = new UnityWebRequest(config.PotluckEndpoint, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(body) { contentType = "application/json" };
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
                Debug.Log($"[FirebaseSender] Sent OK: {json}\n→ {req.downloadHandler.text}");
            else
                Debug.LogError($"[FirebaseSender] POST failed: {req.error}\nURL: {config.PotluckEndpoint}");
        }
    }
}
