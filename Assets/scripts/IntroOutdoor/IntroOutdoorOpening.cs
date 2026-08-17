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

    [SerializeField]
    private string postStandDialogue =
        "カバン、忘れないようにしないと";

    [Header("Typewriter")]
    [SerializeField] private float charactersPerSecond = 20f;

    private int currentDialogueIndex = 0;

    private bool isTyping = false;
    private bool isStandingUp = false;
    private bool isPostStandDialogue = false;
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
        Vector3 cameraPosition =
            cameraTransform.localPosition;

        cameraPosition.y = seatedCameraY;

        cameraTransform.localPosition =
            cameraPosition;

        // セリフUIを表示する
        // Show the dialogue UI
        dialogueObject.SetActive(true);

        // 最初のセリフを開始する
        // Start the first dialogue
        ShowDialogue(
            openingDialogues[currentDialogueIndex]
        );
    }

    private void Update()
    {
        // オープニング終了後は処理しない
        // Do nothing after the opening has finished
        if (introFinished)
        {
            return;
        }

        // 立ち上がり中は入力を受け付けない
        // Ignore input while the Player is standing up
        if (isStandingUp)
        {
            return;
        }

        // マウスが存在しなければ処理しない
        // Do nothing if no mouse device is available
        if (Mouse.current == null)
        {
            return;
        }

        // 左クリックされた瞬間だけ処理する
        // Process only the frame when left-click is pressed
        if (!Mouse.current.leftButton.wasPressedThisFrame)
        {
            return;
        }

        // 文字送り中なら全文を即座に表示する
        // Complete the current text immediately while typing
        if (isTyping)
        {
            CompleteCurrentDialogue();
            return;
        }

        // 立ち上がり後の追加セリフならオープニングを終了する
        // Finish the opening after the post-stand dialogue
        if (isPostStandDialogue)
        {
            FinishOpening();
            return;
        }

        // 通常のオープニングセリフが残っていれば次へ進む
        // Advance to the next opening dialogue if one remains
        if (
            currentDialogueIndex <
            openingDialogues.Length - 1
        )
        {
            currentDialogueIndex++;

            ShowDialogue(
                openingDialogues[currentDialogueIndex]
            );

            return;
        }

        // 最後の通常セリフが終わったら立ち上がる
        // Start standing up after the final opening dialogue
        dialogueObject.SetActive(false);

        StartCoroutine(
            StandUpSequence()
        );
    }

    private void ShowDialogue(string text)
    {
        // 前の文字送りが残っていれば停止する
        // Stop the previous typing coroutine if necessary
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }

        // 指定された文章の文字送りを開始する
        // Start typing the specified dialogue text
        typingCoroutine =
            StartCoroutine(
                TypeDialogue(text)
            );
    }

    private IEnumerator TypeDialogue(string text)
    {
        // 文字送り中であることを記録する
        // Mark the dialogue as currently typing
        isTyping = true;

        // TextMesh Proへ全文を設定する
        // Assign the complete text to TextMesh Pro
        dialogueText.text = text;

        // 最初は一文字も表示しない
        // Hide all characters initially
        dialogueText.maxVisibleCharacters = 0;

        // TMPの文字情報を更新する
        // Update the TextMesh Pro character information
        dialogueText.ForceMeshUpdate();

        // 表示対象の文字数を取得する
        // Get the total number of characters
        int totalCharacters =
            dialogueText.textInfo.characterCount;

        // 一文字あたりの表示時間を計算する
        // Calculate the delay for each character
        float secondsPerCharacter =
            1f /
            Mathf.Max(
                charactersPerSecond,
                1f
            );

        // 一文字ずつ表示する
        // Reveal the text one character at a time
        for (
            int visibleCharacters = 1;
            visibleCharacters <= totalCharacters;
            visibleCharacters++
        )
        {
            dialogueText.maxVisibleCharacters =
                visibleCharacters;

            yield return new WaitForSeconds(
                secondsPerCharacter
            );
        }

        // 全文字を確実に表示する
        // Ensure that every character is visible
        dialogueText.maxVisibleCharacters =
            totalCharacters;

        // 文字送りを終了する
        // Mark typing as complete
        isTyping = false;

        typingCoroutine = null;
    }

    private void CompleteCurrentDialogue()
    {
        // 実行中の文字送りを停止する
        // Stop the active typing coroutine
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }

        // TMPの文字情報を更新する
        // Refresh the TextMesh Pro character information
        dialogueText.ForceMeshUpdate();

        // 全文を表示する
        // Reveal the complete dialogue
        dialogueText.maxVisibleCharacters =
            dialogueText.textInfo.characterCount;

        // 文字送り完了状態にする
        // Mark typing as complete
        isTyping = false;
    }

    private IEnumerator StandUpSequence()
    {
        // 立ち上がり中であることを記録する
        // Mark the stand-up sequence as active
        isStandingUp = true;

        // 開始位置を保存する
        // Store the starting Player position
        Vector3 startPosition =
            playerTransform.position;

        // 開始回転を保存する
        // Store the starting Player rotation
        Quaternion startRotation =
            playerTransform.rotation;

        // 経過時間を初期化する
        // Initialize elapsed time
        float elapsedTime = 0f;

        // 指定時間まで立ち上がり処理を行う
        // Continue the stand-up animation for the specified duration
        while (elapsedTime < standDuration)
        {
            elapsedTime += Time.deltaTime;

            // 0～1の進行度を作る
            // Calculate normalized progress from zero to one
            float t =
                elapsedTime /
                standDuration;

            t = Mathf.Clamp01(t);

            // 動きの開始と終了を滑らかにする
            // Smooth the beginning and end of the movement
            float smoothT =
                t * t * (3f - 2f * t);

            // PlayerをStandPointまで移動する
            // Move the Player toward the StandPoint
            playerTransform.position =
                Vector3.Lerp(
                    startPosition,
                    standPoint.position,
                    smoothT
                );

            // PlayerをStandPointの向きへ回転する
            // Rotate the Player toward the StandPoint rotation
            playerTransform.rotation =
                Quaternion.Slerp(
                    startRotation,
                    standPoint.rotation,
                    smoothT
                );

            // 現在のカメラ位置を取得する
            // Read the current local camera position
            Vector3 cameraPosition =
                cameraTransform.localPosition;

            // 座り状態から立ち状態の高さまで移動する
            // Raise the camera from seated to standing height
            cameraPosition.y =
                Mathf.Lerp(
                    seatedCameraY,
                    standingCameraY,
                    smoothT
                );

            // カメラ位置を反映する
            // Apply the calculated camera position
            cameraTransform.localPosition =
                cameraPosition;

            yield return null;
        }

        // 最終位置をStandPointへ正確に合わせる
        // Snap the Player exactly to the StandPoint
        playerTransform.position =
            standPoint.position;

        // 最終回転をStandPointへ正確に合わせる
        // Snap the Player exactly to the StandPoint rotation
        playerTransform.rotation =
            standPoint.rotation;

        // 最終的なカメラ位置を取得する
        // Read the final local camera position
        Vector3 finalCameraPosition =
            cameraTransform.localPosition;

        // カメラを立ち状態の高さへ固定する
        // Set the camera to the standing height
        finalCameraPosition.y =
            standingCameraY;

        cameraTransform.localPosition =
            finalCameraPosition;

        // 立ち上がり後なのでCharacterControllerだけ戻す
        // Re-enable only the CharacterController after standing
        characterController.enabled = true;

        // 立ち上がり処理を終了する
        // Mark the stand-up sequence as complete
        isStandingUp = false;

        // 追加セリフ状態へ移行する
        // Enter the post-stand dialogue state
        isPostStandDialogue = true;

        // セリフUIを再表示する
        // Show the dialogue UI again
        dialogueObject.SetActive(true);

        // 「カバン、忘れないようにしないと」を表示する
        // Display the post-stand bag reminder dialogue
        ShowDialogue(
            postStandDialogue
        );
    }

    private void FinishOpening()
    {
        // 追加セリフ状態を終了する
        // End the post-stand dialogue state
        isPostStandDialogue = false;

        // セリフUIを消す
        // Hide the dialogue UI
        dialogueObject.SetActive(false);

        // 移動と視点操作を再開する
        // Re-enable movement and camera control
        playerController.enabled = true;

        // ワールドへのインタラクトを再開する
        // Re-enable world interaction
        playerInteraction.enabled = true;

        // 白点はInteraction側に管理させるため一度消す
        // Hide the interaction point until Interaction updates it
        if (interactionPoint != null)
        {
            interactionPoint.SetActive(false);
        }

        // オープニング完了状態にする
        // Mark the entire opening as finished
        introFinished = true;
    }
}