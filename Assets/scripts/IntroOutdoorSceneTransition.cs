using UnityEngine;
using UnityEngine.SceneManagement;

public class IntroOutdoorSceneTransition : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField]
    private string nextSceneName = "ApartmentLoop";

    private bool isLoading = false;

    public void LoadNextScene()
    {
        // 既にScene読み込みを開始している場合は二重実行しない
        // Prevent duplicate loading if a scene transition has already started
        if (isLoading)
        {
            return;
        }

        // Scene名が設定されていない場合は処理しない
        // Do nothing if no scene name has been assigned
        if (string.IsNullOrWhiteSpace(nextSceneName))
        {
            Debug.LogError(
                "Next Scene Name is not assigned.",
                this
            );

            return;
        }

        // Scene読み込み開始状態にする
        // Mark the scene transition as started
        isLoading = true;

        // 指定したSceneへ切り替える
        // Load the specified scene and replace the current scene
        SceneManager.LoadScene(
            nextSceneName,
            LoadSceneMode.Single
        );
    }
}