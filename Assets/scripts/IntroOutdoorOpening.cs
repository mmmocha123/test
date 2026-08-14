using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class IntroOutdoorOpening : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private Transform playerTransform;
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private CharacterController characterController;
    [SerializeField] private IntroOutdoorPlayerController playerController;
    [SerializeField] private IntroOutdoorInteraction playerInteraction;
    [SerializeField] private GameObject interactionPoint;

    [Header("Stand Up")]
    [SerializeField] private Transform standPoint;
    [SerializeField] private float seatedCameraY = 0.95f;
    [SerializeField] private float standingCameraY = 1.35f;
    [SerializeField] private float standDuration = 1.2f;

    [Header("Dialogue")]
    [SerializeField] private GameObject dialogueObject;
    [SerializeField] private TMP_Text dialogueText;

    [SerializeField] private string[] openingDialogues =
    {
        "あれ......",
        "寝ちゃってたみたい",
        "......そろそろ家に帰らなくちゃ"
    };

    [Header("Typewriter")]
    [SerializeField] private float charactersPerSecond = 20f;

    private int currentDialogueIndex = 0;
    private bool isTyping = false;
    private bool isStandingUp = false;
    private bool introFinished = false;

    private Coroutine typingCoroutine;

    private void Start()
    {
        // オープニング中は移動と視点操作を無効にする
        // Disable movement and camera control during the opening
        playerController.enabled = false;

        // オープニング中はインタラクトを無効にする
        // Disable interaction during the opening
        playerInteraction.enabled = false;

        // ベンチとの衝突を避けるためCharacterControllerを無効にする
        // Disable the CharacterController to avoid collision with the bench
        characterController.enabled = false;

        // オープニング中はインタラクションポイントを非表示にする
        // Hide the interaction point during the opening
        if (interactionPoint != null)
        {
            interactionPoint.SetActive(false);
        }

        // カメラを座っている高さに設定する
        // Set the camera to the seated height
        Vector3 cameraPosition = cameraTransform.localPosition;
        cameraPosition.y = seatedCameraY;
        cameraTransform.localPosition = cameraPosition;

        // セリフUIを表示する
        // Show the dialogue UI
        dialogueObject.SetActive(true);

        // 最初のセリフを開始する
        // Start the first dialogue
        ShowDialogue(currentDialogueIndex);
    }

    private void Update()
    {
        // オープニング終了後はクリック処理を行わない
        // Ignore opening click input after the intro has finished
        if (introFinished)
        {
            return;
        }

        // 立ち上がり中はクリック処理を行わない
        // Ignore click input while the Player is standing up
        if (isStandingUp)
        {
            return;
        }

        // マウスが取得できない場合は処理しない
        // Do nothing if no mouse device is available
        if (Mouse.current == null)
        {
            return;
        }

        // 左クリックされたフレームだけ処理する
        // Process input only on the frame the left mouse button is pressed
        if (!Mouse.current.leftButton.wasPressedThisFrame)
        {
            return;
        }

        // 文字送り中なら現在の文章を最後まで表示する
        // Complete the current sentence immediately if it is still typing
        if (isTyping)
        {
            CompleteCurrentDialogue();
            return;
        }

        // まだ次の文章が残っている場合は次へ進む
        // Advance to the next sentence if another sentence remains
        if (currentDialogueIndex < openingDialogues.Length - 1)
        {
            currentDialogueIndex++;
            ShowDialogue(currentDialogueIndex);
            return;
        }

        // 最後の文章を読み終えたらセリフを消して立ち上がる
        // Hide the dialogue and stand up after the final sentence
        dialogueObject.SetActive(false);
        StartCoroutine(StandUpSequence());
    }

    private void ShowDialogue(int dialogueIndex)
    {
        // 以前の文字送り処理が残っていれば停止する
        // Stop any previous typing coroutine if one is still running
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }

        // 指定された文章の文字送りを開始する
        // Start typing the specified dialogue
        typingCoroutine =
            StartCoroutine(TypeDialogue(openingDialogues[dialogueIndex]));
    }

    private IEnumerator TypeDialogue(string text)
    {
        // 現在文字送り中であることを記録する
        // Mark the dialogue as currently typing
        isTyping = true;

        // TextMesh Proへ文章全体を設定する
        // Assign the complete sentence to TextMesh Pro
        dialogueText.text = text;

        // 最初は一文字も表示しない
        // Hide all characters initially
        dialogueText.maxVisibleCharacters = 0;

        // テキスト情報を直ちに生成する
        // Force TextMesh Pro to generate its text information immediately
        dialogueText.ForceMeshUpdate();

        // 現在の文章に含まれる文字数を取得する
        // Get the number of characters in the current sentence
        int totalCharacters = dialogueText.textInfo.characterCount;

        // 0除算を防止しつつ一文字あたりの待機時間を計算する
        // Calculate the delay per character while preventing division by zero
        float secondsPerCharacter =
            1f / Mathf.Max(charactersPerSecond, 1f);

        // 左から順番に表示文字数を増やしていく
        // Increase the number of visible characters from left to right
        for (int visibleCharacters = 1;
             visibleCharacters <= totalCharacters;
             visibleCharacters++)
        {
            // 現在の文字数まで表示する
            // Reveal characters up to the current count
            dialogueText.maxVisibleCharacters = visibleCharacters;

            // 次の文字を表示するまで少し待つ
            // Wait briefly before revealing the next character
            yield return new WaitForSeconds(secondsPerCharacter);
        }

        // 全文表示が完了したので文字送り状態を解除する
        // Mark the typing animation as complete
        isTyping = false;

        // Coroutineの参照を解除する
        // Clear the coroutine reference
        typingCoroutine = null;
    }

    private void CompleteCurrentDialogue()
    {
        // 実行中の文字送りCoroutineを停止する
        // Stop the active typing coroutine
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }

        // 現在の文章をすべて表示する
        // Reveal the entire current sentence
        dialogueText.maxVisibleCharacters =
            dialogueText.textInfo.characterCount;

        // 文字送り完了状態にする
        // Mark the typing animation as complete
        isTyping = false;
    }

    private IEnumerator StandUpSequence()
    {
        // 立ち上がり開始状態にする
        // Mark the stand-up sequence as active
        isStandingUp = true;

        // 立ち上がり開始時のPlayer位置を保存する
        // Store the Player position at the start of the stand-up sequence
        Vector3 startPosition = playerTransform.position;

        // 立ち上がり開始時のPlayer回転を保存する
        // Store the Player rotation at the start of the stand-up sequence
        Quaternion startRotation = playerTransform.rotation;

        // 経過時間を初期化する
        // Initialize elapsed time
        float elapsedTime = 0f;

        // 指定時間が経過するまで立ち上がり処理を続ける
        // Continue the stand-up animation until its duration has elapsed
        while (elapsedTime < standDuration)
        {
            // このフレームの経過時間を加算する
            // Add this frame's elapsed time
            elapsedTime += Time.deltaTime;

            // 立ち上がり全体の進行度を0～1で計算する
            // Calculate stand-up progress from zero to one
            float t = elapsedTime / standDuration;

            // 進行度が1を超えないように制限する
            // Prevent progress from exceeding one
            t = Mathf.Clamp01(t);

            // 開始と終了を滑らかにする
            // Smooth the beginning and end of the movement
            float smoothT = t * t * (3f - 2f * t);

            // Playerを座っている位置からStandPointへ移動する
            // Move the Player from the seated position to the StandPoint
            playerTransform.position =
                Vector3.Lerp(
                    startPosition,
                    standPoint.position,
                    smoothT
                );

            // PlayerをStandPointの向きへ回転させる
            // Rotate the Player toward the StandPoint orientation
            playerTransform.rotation =
                Quaternion.Slerp(
                    startRotation,
                    standPoint.rotation,
                    smoothT
                );

            // 現在のカメラ位置を取得する
            // Get the current local camera position
            Vector3 cameraPosition =
                cameraTransform.localPosition;

            // カメラを座った高さから立った高さへ移動する
            // Raise the camera from seated height to standing height
            cameraPosition.y =
                Mathf.Lerp(
                    seatedCameraY,
                    standingCameraY,
                    smoothT
                );

            // 計算した位置をカメラへ反映する
            // Apply the calculated camera position
            cameraTransform.localPosition =
                cameraPosition;

            // 次のフレームまで待つ
            // Wait until the next frame
            yield return null;
        }

        // Playerを最終的なStandPoint位置へ正確に合わせる
        // Snap the Player exactly to the final StandPoint position
        playerTransform.position =
            standPoint.position;

        // PlayerをStandPointの向きへ正確に合わせる
        // Snap the Player exactly to the StandPoint rotation
        playerTransform.rotation =
            standPoint.rotation;

        // 最終的なカメラ位置を取得する
        // Get the final local camera position
        Vector3 finalCameraPosition =
            cameraTransform.localPosition;

        // カメラを通常の立ち状態の高さへ合わせる
        // Set the camera to the normal standing height
        finalCameraPosition.y =
            standingCameraY;

        // 最終的なカメラ位置を反映する
        // Apply the final camera position
        cameraTransform.localPosition =
            finalCameraPosition;

        // CharacterControllerを再び有効にする
        // Re-enable the CharacterController
        characterController.enabled = true;

        // 移動と視点操作を有効にする
        // Enable movement and camera control
        playerController.enabled = true;

        // インタラクト処理を有効にする
        // Enable interaction
        playerInteraction.enabled = true;

        // 立ち上がり処理が終了したことを記録する
        // Mark the stand-up sequence as complete
        isStandingUp = false;

        // IntroOutdoor開始演出全体が終了したことを記録する
        // Mark the entire IntroOutdoor opening sequence as complete
        introFinished = true;
    }
}