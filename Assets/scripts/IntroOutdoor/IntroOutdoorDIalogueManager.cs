using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class IntroOutdoorDialogueManager : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject dialogueObject;
    [SerializeField] private TMP_Text dialogueText;
    [SerializeField] private TMP_FontAsset defaultDialogueFont;
    [SerializeField] private GameObject interactionPoint;

    [Header("Gameplay Lock")]
    [SerializeField]
    private MonoBehaviour[] behavioursToDisableDuringDialogue;

    [Header("Typewriter")]
    [SerializeField] private float charactersPerSecond = 20f;

    [Header("Interaction Cooldown")]
    [SerializeField]
    private float interactionCooldownAfterDialogue = 1f;

    private string[] currentMessages;
    private int currentMessageIndex;

    private bool dialogueActive;
    private bool isTyping;

    private Coroutine typingCoroutine;
    private bool[] previousBehaviourStates;

    private int dialogueStartedFrame;

    private float worldInteractionBlockedUntil;

    public bool IsDialogueActive => dialogueActive;

    public bool IsWorldInteractionBlocked =>
        dialogueActive ||
        Time.unscaledTime < worldInteractionBlockedUntil;

    public void BeginDialogue(
        string[] messages,
        TMP_FontAsset fontAsset)
    {
        // 既に文章表示中なら新しい文章を開始しない
        // Do not start another dialogue while one is already active
        if (dialogueActive)
        {
            return;
        }

        // 表示する文章が存在しない場合は処理しない
        // Do nothing if there are no messages to display
        if (messages == null || messages.Length == 0)
        {
            return;
        }

        // UI参照が設定されていない場合は処理しない
        // Do nothing if the required UI references are missing
        if (dialogueObject == null || dialogueText == null)
        {
            return;
        }

        // 今回表示する文章を保存する
        // Store the messages for this dialogue
        currentMessages = messages;

        // 最初の文章から開始する
        // Start from the first message
        currentMessageIndex = 0;

        // 文章表示状態にする
        // Mark the dialogue as active
        dialogueActive = true;

        // インタラクト開始フレームを保存する
        // Store the frame on which the interaction started
        dialogueStartedFrame = Time.frameCount;

        // 対象専用フォントが指定されていれば使用する
        // Use the target-specific font if one is assigned
        if (fontAsset != null)
        {
            dialogueText.font = fontAsset;
        }
        else if (defaultDialogueFont != null)
        {
            // 専用フォントがなければ主人公用フォントを使用する
            // Use the default protagonist font when no specific font is assigned
            dialogueText.font = defaultDialogueFont;
        }

        // 移動や視点操作などを停止する
        // Disable gameplay controls while dialogue is displayed
        SetGameplayEnabled(false);

        // インタラクト用の白点を消す
        // Hide the interaction point
        SetInteractionPointVisible(false);

        // セリフUIを表示する
        // Show the dialogue UI
        dialogueObject.SetActive(true);

        // 最初の文章を表示する
        // Show the first message
        ShowCurrentMessage();
    }

    private void Update()
    {
        // 文章表示中でなければ処理しない
        // Do nothing when no dialogue is active
        if (!dialogueActive)
        {
            return;
        }

        // 開始時と同じフレームのクリックは無視する
        // Ignore the click used to start the interaction
        if (Time.frameCount <= dialogueStartedFrame)
        {
            return;
        }

        // マウスが存在しない場合は処理しない
        // Do nothing if no mouse is available
        if (Mouse.current == null)
        {
            return;
        }

        // 左クリックされた瞬間だけ処理する
        // Process input only when the left mouse button is pressed
        if (!Mouse.current.leftButton.wasPressedThisFrame)
        {
            return;
        }

        // 文字送り途中なら現在の文章を全文表示する
        // Complete the current message if it is still typing
        if (isTyping)
        {
            CompleteCurrentMessage();
            return;
        }

        // 次の文章があれば進む
        // Advance to the next message if one remains
        if (currentMessageIndex < currentMessages.Length - 1)
        {
            currentMessageIndex++;
            ShowCurrentMessage();
            return;
        }

        // 最後の文章なら終了する
        // End the dialogue after the final message
        EndDialogue();
    }

    private void ShowCurrentMessage()
    {
        // 前の文字送り処理が残っていれば停止する
        // Stop the previous typing coroutine if necessary
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }

        // 現在の文章の文字送りを開始する
        // Start typing the current message
        typingCoroutine = StartCoroutine(
            TypeMessage(
                currentMessages[currentMessageIndex]
            )
        );
    }

    private IEnumerator TypeMessage(string message)
    {
        // 文字送り中の状態にする
        // Mark the message as typing
        isTyping = true;

        // 文章全体を設定する
        // Assign the complete message
        dialogueText.text = message;

        // 最初は全文を非表示にする
        // Hide all characters initially
        dialogueText.maxVisibleCharacters = 0;

        // 文字情報を更新する
        // Update the text information
        dialogueText.ForceMeshUpdate();

        // 文字数を取得する
        // Get the total character count
        int totalCharacters =
            dialogueText.textInfo.characterCount;

        // 一文字ごとの待機時間を計算する
        // Calculate the delay per character
        float secondsPerCharacter =
            1f / Mathf.Max(charactersPerSecond, 1f);

        // 一文字ずつ表示する
        // Reveal characters one at a time
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

        // 最後まで表示する
        // Ensure the complete message is visible
        dialogueText.maxVisibleCharacters =
            totalCharacters;

        // 文字送りを終了する
        // Mark typing as complete
        isTyping = false;

        typingCoroutine = null;
    }

    private void CompleteCurrentMessage()
    {
        // 文字送りを停止する
        // Stop the active typing coroutine
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }

        dialogueText.ForceMeshUpdate();

        // 現在の文章を全文表示する
        // Reveal the complete current message
        dialogueText.maxVisibleCharacters =
            dialogueText.textInfo.characterCount;

        isTyping = false;
    }

    private void EndDialogue()
    {
        // 残っている文字送り処理を停止する
        // Stop any remaining typing coroutine
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }

        // 文章表示を終了する
        // Mark the dialogue as inactive
        dialogueActive = false;
        isTyping = false;

        // 終了後のインタラクト禁止時間を設定する
        // Set the interaction cooldown after dialogue
        worldInteractionBlockedUntil =
            Time.unscaledTime +
            interactionCooldownAfterDialogue;

        currentMessages = null;
        currentMessageIndex = 0;

        // セリフUIを非表示にする
        // Hide the dialogue UI
        dialogueObject.SetActive(false);

        // 主人公用フォントへ戻す
        // Restore the default protagonist font
        if (defaultDialogueFont != null)
        {
            dialogueText.font = defaultDialogueFont;
        }

        // 移動や視点操作を元に戻す
        // Restore gameplay controls
        SetGameplayEnabled(true);

        // 白点はInteraction側が再び表示するまで消しておく
        // Keep the interaction point hidden until interaction becomes available
        SetInteractionPointVisible(false);
    }

    private void SetGameplayEnabled(bool gameplayEnabled)
    {
        // 管理対象がなければ処理しない
        // Do nothing if no gameplay behaviours are assigned
        if (behavioursToDisableDuringDialogue == null)
        {
            return;
        }

        if (!gameplayEnabled)
        {
            previousBehaviourStates =
                new bool[
                    behavioursToDisableDuringDialogue.Length
                ];

            for (
                int i = 0;
                i < behavioursToDisableDuringDialogue.Length;
                i++
            )
            {
                // 未設定要素は無視する
                // Ignore unassigned elements
                if (behavioursToDisableDuringDialogue[i] == null)
                {
                    continue;
                }

                // DialogueManager自身は無効化しない
                // Never disable the DialogueManager itself
                if (behavioursToDisableDuringDialogue[i] == this)
                {
                    continue;
                }

                previousBehaviourStates[i] =
                    behavioursToDisableDuringDialogue[i].enabled;

                behavioursToDisableDuringDialogue[i].enabled =
                    false;
            }

            return;
        }

        if (previousBehaviourStates == null)
        {
            return;
        }

        for (
            int i = 0;
            i < behavioursToDisableDuringDialogue.Length;
            i++
        )
        {
            // 未設定要素は無視する
            // Ignore unassigned elements
            if (behavioursToDisableDuringDialogue[i] == null)
            {
                continue;
            }

            // DialogueManager自身は変更しない
            // Never change the DialogueManager itself
            if (behavioursToDisableDuringDialogue[i] == this)
            {
                continue;
            }

            behavioursToDisableDuringDialogue[i].enabled =
                previousBehaviourStates[i];
        }

        previousBehaviourStates = null;
    }

    private void SetInteractionPointVisible(bool visible)
    {
        // 白点が設定されている場合のみ変更する
        // Change visibility only if the interaction point is assigned
        if (interactionPoint != null)
        {
            interactionPoint.SetActive(visible);
        }
    }
}