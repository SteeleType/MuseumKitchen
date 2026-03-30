using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneChange : MonoBehaviour
{
    [SerializeField] string sceneToLoad;
    public void ChangeScene()
    {
        SceneManager.LoadScene(sceneToLoad);
    }
}
