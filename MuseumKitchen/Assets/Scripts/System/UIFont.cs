using TMPro;
using UnityEngine;

/// <summary>
/// Single source of truth for the UI's default TextMeshPro font.
/// Loads AveriaLibre SDF from Resources once and caches it.
/// 所有运行时构建的 TMP 文本都通过这里取字体；找不到则退回 TMP 默认。
/// </summary>
public static class UIFont
{
    private static TMP_FontAsset _cached;
    private static bool _attempted;

    public static TMP_FontAsset Default
    {
        get
        {
            if (_attempted) return _cached;
            _attempted = true;
            _cached = Resources.Load<TMP_FontAsset>("Fonts/AveriaLibre-Regular SDF");
            if (_cached == null)
                Debug.LogWarning("[UIFont] AveriaLibre SDF not found in Resources/Fonts; falling back to TMP default.");
            return _cached;
        }
    }

    /// <summary>Apply the default font to a TMP text component (no-op if font missing).</summary>
    public static void Apply(TMP_Text tmp)
    {
        if (tmp == null) return;
        var f = Default;
        if (f != null) tmp.font = f;
    }
}
