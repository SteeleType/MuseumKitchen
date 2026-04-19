using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Runtime-spawns a small "← Back" button anchored to the top-left of the first Canvas in the scene.
/// Attach to any persistent GameObject in scenes that need a manual escape hatch back to StartScene.
/// 运行时在 Canvas 左上角创建"← Back"按钮，用于任意场景一键回到 StartScene。
/// </summary>
public class RuntimeBackButton : MonoBehaviour
{
    [SerializeField] private string targetScene = "StartScene";
    [SerializeField] private string buttonLabel = "\u2190 Back";

    private void Start()
    {
        var canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("[RuntimeBackButton] No Canvas in scene; back button skipped.");
            return;
        }

        var go = new GameObject("RuntimeBackButton", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);
        go.layer = LayerMask.NameToLayer("UI");

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(20, -20);
        rt.sizeDelta = new Vector2(140, 50);

        var img = go.AddComponent<Image>();
        img.color = new Color(0.1f, 0.1f, 0.15f, 0.85f);

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() => SceneManager.LoadScene(targetScene));

        var labelGO = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(go.transform, false);
        labelGO.layer = LayerMask.NameToLayer("UI");
        var lrt = labelGO.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero;
        lrt.offsetMax = Vector2.zero;
        var tmp = labelGO.AddComponent<TextMeshProUGUI>();
        tmp.text = buttonLabel;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = 22;
        tmp.color = Color.white;
    }
}
