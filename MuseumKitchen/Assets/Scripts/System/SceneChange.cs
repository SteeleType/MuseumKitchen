using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneChange : MonoBehaviour
{
    [SerializeField] private string sceneToLoad;

    private void Awake()
    {
        // If attached to a Button, auto-wire its onClick to ChangeScene()
        // 挂在 Button 所在 GO 上时，自动绑定 onClick
        var btn = GetComponent<Button>();
        if (btn != null) btn.onClick.AddListener(ChangeScene);
    }

    public void ChangeScene()
    {
        if (string.IsNullOrEmpty(sceneToLoad))
        {
            Debug.LogWarning($"[SceneChange] '{name}' has no sceneToLoad set.");
            return;
        }
        SceneManager.LoadScene(sceneToLoad);
    }

    public void SetSceneToLoad(string sceneName) => sceneToLoad = sceneName;
}
