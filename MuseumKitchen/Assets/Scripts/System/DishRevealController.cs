using UnityEngine;
using UnityEngine.SceneManagement;
using DG.Tweening;

/// <summary>
/// Plays the DishReveal scene for a fixed duration, then returns to the Kitchen
/// so the next visitor can build their own dish.
/// 播放 DishReveal 揭示动画一段时间后自动跳回 Kitchen，让下一位观众继续。
/// </summary>
public class DishRevealController : MonoBehaviour
{
    [SerializeField] private float revealDuration = 3.5f;
    [SerializeField] private string returnScene = "Kitchen";

    private void Start()
    {
        DOVirtual.DelayedCall(revealDuration, () => SceneManager.LoadScene(returnScene))
            .SetLink(gameObject); // tween dies if scene unloads early / 场景提前卸载时 tween 自动停
    }
}
