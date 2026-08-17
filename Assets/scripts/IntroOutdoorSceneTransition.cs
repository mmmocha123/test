using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class IntroOutdoorSceneTransition : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField]
    private string nextSceneName = "Apartmentloop";

    [Header("Fade")]
    [SerializeField]
    private CanvasGroup fadeCanvasGroup;

    [SerializeField]
    private float fadeOutDuration = 0.5f;

    [SerializeField]
    private float blackHoldDuration = 2f;

    [SerializeField]
    private float fadeInDuration = 0.7f;

    [SerializeField]
    private bool fadeInOnStart = false;

    [Header("Door Audio")]
    [SerializeField]
    private AudioSource doorAudioSource;

    [SerializeField]
    private AudioClip doorOpenClip;

    [SerializeField, Range(0f, 1f)]
    private float doorVolume = 1f;

    [Header("Player Lock")]
    [SerializeField]
    private IntroOutdoorPlayerController playerController;

    [SerializeField]
    private IntroOutdoorInteraction playerInteraction;

    private IntroOutdoorInteractable interactable;

    private bool isTransitioning;

    private void Awake()
    {
        // 同じGameObjectにInteractableがあれば取得する
        // Get the Interactable if one exists on this GameObject
        interactable =
            GetComponent<IntroOutdoorInteractable>();

        // Fade用CanvasGroupがなければここでは何もしない
        // Do nothing here if the fade CanvasGroup is missing
        if (fadeCanvasGroup == null)
        {
            return;
        }

        // 遷移先Sceneでは最初から黒画面にする
        // Start fully black in the destination Scene
        if (fadeInOnStart)
        {
            fadeCanvasGroup.alpha = 1f;
            fadeCanvasGroup.blocksRaycasts = true;
            return;
        }

        // 通常SceneではFade画像を透明にしておく
        // Keep the fade image transparent during normal gameplay
        fadeCanvasGroup.alpha = 0f;
        fadeCanvasGroup.blocksRaycasts = false;
    }

    private void Start()
    {
        // 遷移先Sceneでは開始時に黒から明転する
        // Fade from black when the destination Scene starts
        if (fadeInOnStart)
        {
            StartCoroutine(
                FadeInAtSceneStart()
            );
        }
    }

    public void BeginTransition()
    {
        // 既に遷移中なら二重実行しない
        // Prevent duplicate transitions
        if (isTransitioning)
        {
            return;
        }

        // Scene名が設定されていなければ処理しない
        // Do nothing if the destination Scene name is missing
        if (string.IsNullOrWhiteSpace(nextSceneName))
        {
            Debug.LogError(
                "Next Scene Name is not assigned.",
                this
            );

            return;
        }

        // Fade用UIが設定されていなければ処理しない
        // Do nothing if the fade CanvasGroup is missing
        if (fadeCanvasGroup == null)
        {
            Debug.LogError(
                "Fade Canvas Group is not assigned.",
                this
            );

            return;
        }

        // Scene遷移処理を開始する
        // Start the Scene transition sequence
        StartCoroutine(
            TransitionSequence()
        );
    }

    private IEnumerator TransitionSequence()
    {
        // 遷移中状態にする
        // Mark the transition as active
        isTransitioning = true;

        // 同じドアをもう一度操作できないようにする
        // Prevent the door from being interacted with again
        if (interactable != null)
        {
            interactable.SetInteractionEnabled(false);
        }

        // プレイヤー移動と視点操作を停止する
        // Disable player movement and camera control
        if (playerController != null)
        {
            playerController.enabled = false;
        }

        // インタラクト操作を停止する
        // Disable world interaction
        if (playerInteraction != null)
        {
            playerInteraction.enabled = false;
        }

        // Fade画面でクリックを遮断する
        // Block UI raycasts during the transition
        fadeCanvasGroup.blocksRaycasts = true;

        // ドアを開ける音を再生する
        // Play the door opening sound
        if (
            doorAudioSource != null &&
            doorOpenClip != null
        )
        {
            doorAudioSource.PlayOneShot(
                doorOpenClip,
                doorVolume
            );
        }

        // 現在の画面から黒画面へ暗転する
        // Fade from the current view to black
        yield return Fade(
            0f,
            1f,
            fadeOutDuration
        );

        // 少なくとも指定秒数は黒画面を維持する
        // Keep the screen black for at least the specified duration
        float holdDuration =
            blackHoldDuration;

        // ドア音が長い場合は途中でSceneを切り替えない
        // Avoid changing Scenes before a long door sound has finished
        if (doorOpenClip != null)
        {
            float remainingDoorTime =
                doorOpenClip.length -
                fadeOutDuration;

            holdDuration =
                Mathf.Max(
                    holdDuration,
                    remainingDoorTime
                );
        }

        // timeScaleの影響を受けずに黒画面で待機する
        // Wait on the black screen independently of timeScale
        yield return new WaitForSecondsRealtime(
            Mathf.Max(0f, holdDuration)
        );

        // Apartmentloopを非同期で読み込む
        // Load Apartmentloop asynchronously
        AsyncOperation loadOperation =
            SceneManager.LoadSceneAsync(
                nextSceneName,
                LoadSceneMode.Single
            );

        // Scene読み込み完了まで待つ
        // Wait until Scene loading has completed
        while (
            loadOperation != null &&
            !loadOperation.isDone
        )
        {
            yield return null;
        }
    }

    private IEnumerator FadeInAtSceneStart()
    {
        // 新しいSceneの最初の描画まで黒画面を維持する
        // Keep the first rendered frame of the new Scene black
        yield return null;

        // 黒画面からゲーム画面へ明転する
        // Fade from black into the game view
        yield return Fade(
            1f,
            0f,
            fadeInDuration
        );

        // 明転完了後はUIクリックを遮断しない
        // Stop blocking raycasts after the fade has completed
        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.blocksRaycasts = false;
        }
    }

    private IEnumerator Fade(
        float startAlpha,
        float endAlpha,
        float duration)
    {
        // Fade用CanvasGroupがなければ終了する
        // Stop if the fade CanvasGroup is missing
        if (fadeCanvasGroup == null)
        {
            yield break;
        }

        // 時間0の場合は即座に指定Alphaへ変更する
        // Apply the target alpha immediately when duration is zero
        if (duration <= 0f)
        {
            fadeCanvasGroup.alpha =
                endAlpha;

            yield break;
        }

        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            // timeScaleに依存しない経過時間を加算する
            // Accumulate time independently of timeScale
            elapsedTime +=
                Time.unscaledDeltaTime;

            // Fade全体の進行度を0から1で求める
            // Calculate fade progress from zero to one
            float t =
                Mathf.Clamp01(
                    elapsedTime / duration
                );

            // Alphaを開始値から終了値へ補間する
            // Interpolate alpha from the start value to the end value
            fadeCanvasGroup.alpha =
                Mathf.Lerp(
                    startAlpha,
                    endAlpha,
                    t
                );

            yield return null;
        }

        // 最後に正確なAlpha値へ合わせる
        // Ensure the final alpha value is exact
        fadeCanvasGroup.alpha =
            endAlpha;
    }
}