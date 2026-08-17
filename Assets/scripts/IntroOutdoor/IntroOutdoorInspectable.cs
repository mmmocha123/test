using TMPro;
using UnityEngine;

public class IntroOutdoorInspectable : MonoBehaviour
{
    [Header("Dialogue")]
    [SerializeField]
    private IntroOutdoorDialogueManager dialogueManager;

    [SerializeField]
    private TMP_FontAsset dialogueFont;

    [SerializeField]
    private string[] messages;

    public void BeginInspection()
    {
        // DialogueManagerが設定されていなければ処理しない
        // Do nothing if the DialogueManager is not assigned
        if (dialogueManager == null)
        {
            return;
        }

        // 文章が設定されていなければ処理しない
        // Do nothing if no inspection messages are assigned
        if (messages == null || messages.Length == 0)
        {
            return;
        }

        // このオブジェクト用の文章表示を開始する
        // Start the dialogue sequence assigned to this object
        dialogueManager.BeginDialogue(
            messages,
            dialogueFont
        );
    }
}