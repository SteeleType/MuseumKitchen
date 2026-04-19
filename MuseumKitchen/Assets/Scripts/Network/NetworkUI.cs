// LAN-mode network UI. Not used in cloud (Firebase) mode; compiled out on WebGL because
// it depends on System.Net.Sockets and on the MuseumLanSender type (also #if'd).
// 局域网模式的 IP 面板；Firebase 模式不需要；WebGL 不编译。
#if !UNITY_WEBGL || UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Net;
using System.Net.Sockets;

/// <summary>
/// Runtime-created network UI panel. Two modes:
///   Host mode: only displays local IP (for the big screen receiver)
///   Client mode: displays local IP + target IP input field
/// </summary>
public class NetworkUI : MonoBehaviour
{
    [Header("Mode / 模式")]
    [Tooltip("Host mode: only shows local IP.\nClient mode: shows IP + target IP input.\n主机模式只显示本机IP；客户端模式额外显示目标IP输入框。")]
    public bool isHostMode = false;

    [Header("Auto-link / 自动关联 (Client only)")]
    [Tooltip("Reference to the MuseumLanSender. Auto-finds if left empty.\n发送器引用，留空则自动查找。仅客户端需要。")]
    public MuseumLanSender lanSender;

    private TMP_Text _localIPText;
    private TMP_InputField _targetIPInput;

    private void Start()
    {
        if (!isHostMode && lanSender == null)
            lanSender = FindObjectOfType<MuseumLanSender>();

        BuildUI();

        string localIP = GetLocalIPAddress();
        _localIPText.text = $"Host IP:  <b>{localIP}</b>";
        Debug.Log($"[NetworkUI] Local IP / 本机IP: {localIP}");

        if (!isHostMode && _targetIPInput != null)
        {
            if (lanSender != null)
                _targetIPInput.text = lanSender.serverIP;
            _targetIPInput.onEndEdit.AddListener(OnTargetIPChanged);
        }
    }

    private void BuildUI()
    {
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[NetworkUI] No Canvas found in scene!");
            return;
        }

        // Panel
        var panel = CreateUIObject("NetworkPanel", canvas.transform);
        var panelRect = panel.GetComponent<RectTransform>();
        SetAnchors(panelRect, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
        panelRect.anchoredPosition = new Vector2(20, -20);
        panelRect.sizeDelta = isHostMode ? new Vector2(420, 90) : new Vector2(460, 150);

        var panelImg = panel.AddComponent<Image>();
        panelImg.color = new Color(0.08f, 0.08f, 0.12f, 0.88f);

        var vlg = panel.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(16, 16, 12, 12);
        vlg.spacing = 8;
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        // Row 1: Title
        var titleGO = CreateUIObject("Title", panel.transform);
        var titleText = titleGO.AddComponent<TextMeshProUGUI>();
        titleText.text = isHostMode ? ">> Big Screen (Host)" : ">> Network Settings";
        titleText.fontSize = 22;
        titleText.fontStyle = FontStyles.Bold;
        titleText.color = new Color(1f, 0.85f, 0.35f);
        var titleLE = titleGO.AddComponent<LayoutElement>();
        titleLE.preferredHeight = 30;

        // Row 2: Local IP
        var ipGO = CreateUIObject("LocalIPDisplay", panel.transform);
        _localIPText = ipGO.AddComponent<TextMeshProUGUI>();
        _localIPText.text = "Host IP:  loading...";
        _localIPText.fontSize = 17;
        _localIPText.color = new Color(0.55f, 1f, 0.6f);
        _localIPText.richText = true;
        var ipLE = ipGO.AddComponent<LayoutElement>();
        ipLE.preferredHeight = 26;

        // Row 3: Target IP input (Client only)
        if (!isHostMode)
        {
            var rowGO = CreateUIObject("TargetIPRow", panel.transform);
            var hlg = rowGO.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            var rowLE = rowGO.AddComponent<LayoutElement>();
            rowLE.preferredHeight = 38;

            // Label
            var labelGO = CreateUIObject("Label", rowGO.transform);
            var labelTMP = labelGO.AddComponent<TextMeshProUGUI>();
            labelTMP.text = "Target IP:";
            labelTMP.fontSize = 16;
            labelTMP.color = Color.white;
            labelTMP.alignment = TextAlignmentOptions.MidlineLeft;
            var labelLE = labelGO.AddComponent<LayoutElement>();
            labelLE.preferredWidth = 85;

            // InputField container
            var inputGO = CreateUIObject("TargetIPInputField", rowGO.transform);
            inputGO.AddComponent<Image>().color = new Color(0.18f, 0.18f, 0.24f, 1f);
            var inputLE = inputGO.AddComponent<LayoutElement>();
            inputLE.flexibleWidth = 1;

            // Text Area
            var textArea = CreateUIObject("Text Area", inputGO.transform);
            var taRect = textArea.GetComponent<RectTransform>();
            StretchFill(taRect, new Vector2(8, 4), new Vector2(-8, -4));
            textArea.AddComponent<RectMask2D>();

            // Placeholder
            var phGO = CreateUIObject("Placeholder", textArea.transform);
            var phTMP = phGO.AddComponent<TextMeshProUGUI>();
            phTMP.text = "Enter IP... (e.g. 192.168.1.100)";
            phTMP.fontSize = 14;
            phTMP.fontStyle = FontStyles.Italic;
            phTMP.color = new Color(0.5f, 0.5f, 0.55f, 0.6f);
            phTMP.alignment = TextAlignmentOptions.MidlineLeft;
            StretchFill(phGO.GetComponent<RectTransform>());

            // Input text
            var txtGO = CreateUIObject("Text", textArea.transform);
            var txtTMP = txtGO.AddComponent<TextMeshProUGUI>();
            txtTMP.fontSize = 15;
            txtTMP.color = Color.white;
            txtTMP.alignment = TextAlignmentOptions.MidlineLeft;
            StretchFill(txtGO.GetComponent<RectTransform>());

            // Wire up TMP_InputField
            _targetIPInput = inputGO.AddComponent<TMP_InputField>();
            _targetIPInput.textViewport = taRect;
            _targetIPInput.textComponent = txtTMP;
            _targetIPInput.placeholder = phTMP;
            _targetIPInput.text = "255.255.255.255";
            _targetIPInput.characterValidation = TMP_InputField.CharacterValidation.None;
            _targetIPInput.contentType = TMP_InputField.ContentType.Standard;
            _targetIPInput.caretColor = new Color(1f, 0.85f, 0.35f);
            _targetIPInput.selectionColor = new Color(1f, 0.85f, 0.35f, 0.3f);
        }
    }

    private void OnTargetIPChanged(string newIP)
    {
        if (lanSender != null && !string.IsNullOrWhiteSpace(newIP))
        {
            lanSender.serverIP = newIP.Trim();
            Debug.Log($"[NetworkUI] Target IP updated / 目标IP已更新: {lanSender.serverIP}");
        }
    }

    private string GetLocalIPAddress()
    {
        try
        {
            using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
            {
                socket.Connect("8.8.8.8", 65530);
                IPEndPoint ep = socket.LocalEndPoint as IPEndPoint;
                return ep?.Address.ToString() ?? "N/A";
            }
        }
        catch
        {
            try
            {
                foreach (var ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
                    if (ip.AddressFamily == AddressFamily.InterNetwork)
                        return ip.ToString();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[NetworkUI] IP detection failed: {e.Message}");
            }
            return "N/A";
        }
    }

    private GameObject CreateUIObject(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.layer = LayerMask.NameToLayer("UI");
        return go;
    }

    private void SetAnchors(RectTransform rt, Vector2 min, Vector2 max, Vector2 pivot)
    {
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.pivot = pivot;
    }

    private void StretchFill(RectTransform rt, Vector2? offsetMin = null, Vector2? offsetMax = null)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = offsetMin ?? Vector2.zero;
        rt.offsetMax = offsetMax ?? Vector2.zero;
    }

    private void OnDestroy()
    {
        if (_targetIPInput != null)
            _targetIPInput.onEndEdit.RemoveListener(OnTargetIPChanged);
    }
}
#endif
