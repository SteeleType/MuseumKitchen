// Legacy UDP-LAN receiver. Replaced by FirebaseReceiver for WebGL/cloud delivery.
// Kept compiled only on non-WebGL targets so existing scenes still load on Standalone builds.
// 旧的局域网 UDP 接收器；WebGL 不编译；保留是为了 Standalone 端兼容旧场景。
#if !UNITY_WEBGL || UNITY_EDITOR
using UnityEngine;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Collections.Concurrent;
using System.Threading;

public class MuseumLanReceiver : MonoBehaviour
{
    [Header("Network Listening Settings / 网络监听设置")]
    [Tooltip("The port to listen on. Must exactly match the Sender's target port.\n监听哪个端口的消息，必须和发送端(Sender)保持一致。")]
    public int listenPort = 9001;
    
    [Header("Events / 事件")]
    [Tooltip("Triggered when the big screen receives a data packet from a tablet. Attach your logic here.\n当大屏收到平板发来的数据包时触发，请在此处绑定你的生成逻辑。")]
    public PotluckEvent OnDataReceived;

    private UdpClient udpServer;
    private Thread listenerThread;
    private bool isListening = false;
    
    // Thread-safe queue for multi-threading (UDP receive is on a background thread, while Unity instantiation must be on the main thread)
    // 多线程通信安全队列 (UDP接收在后台线程，但Unity实例化必须在主线程执行)
    private ConcurrentQueue<string> receivedQueue = new ConcurrentQueue<string>();

    private PotluckManager autoLinkedManager;

    private void Start()
    {
        // Auto-link if they are on the same GameObject / 如果挂在同一个物体上，自动直连
        autoLinkedManager = GetComponent<PotluckManager>();
        
        StartListening();
    }

    private void StartListening()
    {
        try
        {
            udpServer = new UdpClient(listenPort);
            isListening = true;

            listenerThread = new Thread(ListenForData);
            listenerThread.IsBackground = true; // Ensure the thread is destroyed when Unity closes / 确保Unity关闭时线程会自动销毁
            listenerThread.Start();
            
            Debug.Log($"[Server Receiver] Big screen started listening on port / 大屏启动监听，目标端口: {listenPort}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Server Error / 服务端错误] Failed to bind port / 无法绑定端口 {listenPort}: {e.Message}");
        }
    }

    private void ListenForData()
    {
        IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, listenPort);

        while (isListening)
        {
            try
            {
                // This will block and wait for incoming messages / 阻塞等待接收消息
                byte[] data = udpServer.Receive(ref remoteEP);
                string jsonMessage = Encoding.UTF8.GetString(data);
                
                // Enqueue the received message for the main thread to handle in Update()
                // 把收到的消息丢进线程安全队列，让主线程在 Update 里提取
                receivedQueue.Enqueue(jsonMessage);
            }
            catch (SocketException)
            {
                // Normal exit when isListening is set to false and the socket is closed
                // 属于关闭接收器时的正常中断
                break; 
            }
        }
    }

    private void Update()
    {
        // Only the Unity Main Thread (Update) can instantiate objects and handle animations
        // 只有 Unity主线程（Update）能处理物体生成、动画等逻辑
        while (receivedQueue.TryDequeue(out string json))
        {
            Debug.Log($"[Server Receiver] Message received / 收到消息: {json}");
            
            try 
            {
                PotluckData data = JsonUtility.FromJson<PotluckData>(json);
                if (data != null)
                {
                    // 1. Trigger inspector events (if any) / 触发面板里挂载的事件
                    OnDataReceived?.Invoke(data);
                    
                    // 2. Auto-forward to manager (hard-linked) / 触发自动硬连接，无需拖拽连线
                    if (autoLinkedManager != null)
                    {
                        autoLinkedManager.OnPotluckDataReceived(data);
                    }
                }
            } 
            catch (System.Exception e)
            {
                Debug.LogError($"[Server Error / 服务端错误] JSON Parse Failed / 解析失败: {e.Message}\nRaw Data / 原始数据: {json}");
            }
        }
    }

    private void OnDisable()
    {
        isListening = false;
        
        if (udpServer != null)
        {
            udpServer.Close();
            udpServer = null;
        }

        if (listenerThread != null && listenerThread.IsAlive)
        {
            listenerThread.Abort();
        }
    }
}
#endif
