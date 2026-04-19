// Legacy UDP-LAN sender. Replaced by FirebaseSender for WebGL/cloud delivery.
// Compiled out on WebGL because UnityEngine WebGL strips System.Net.Sockets.
// 旧的局域网 UDP 发送器；WebGL 不编译；保留是为了 Standalone 端兼容旧场景。
#if !UNITY_WEBGL || UNITY_EDITOR
using UnityEngine;
using System.Net.Sockets;
using System.Net;
using System.Text;

public class MuseumLanSender : MonoBehaviour
{
    [Tooltip("Target IP address of the Big Screen Server. Use 255.255.255.255 for automatic LAN broadcast without setting IP, or use 127.0.0.1 for strict local testing.\n目标大屏所在的 IP 地址。填 255.255.255.255 即为全展厅局域网广播，全自动盲发，免去手工填 IP 的烦恼！")]
    public string serverIP = "255.255.255.255";
    public int serverPort = 9001;

    public void SendDumplingData(PotluckData data)
    {
        // Auto-assign the device name if client ID is empty / 如果没有指定设备ID，自动赋予设备名
        if (string.IsNullOrEmpty(data.clientId))
        {
            data.clientId = SystemInfo.deviceName;
        }

        string jsonPayload = JsonUtility.ToJson(data);

        try
        {
            using (UdpClient udpClient = new UdpClient())
            {
                // Allow broadcasting to 255.255.255.255 / 允许发送到全局域网广播地址
                udpClient.EnableBroadcast = true;
                
                byte[] bytes = Encoding.UTF8.GetBytes(jsonPayload);
                udpClient.Send(bytes, bytes.Length, serverIP, serverPort);
                Debug.Log($"[Client Sender] Data successfully sent to / 成功发送数据至 {serverIP}:{serverPort}\nPayload / 内容: {jsonPayload}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Client Sender Error / 客户端发送错误] Failed to send data / 数据发送失败: {e.Message}");
        }
    }
}
#endif
