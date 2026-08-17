using UnityEngine;

public class AreaLimitTrigger : MonoBehaviour
{
    [SerializeField] private GameObject messageObject;

    private void Start()
    {
        // ゲーム開始時は警告メッセージを非表示にする
        // Hide the warning message when the game starts
        if (messageObject != null)
        {
            messageObject.SetActive(false);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Triggerに入ったオブジェクトがPlayerか確認する
        // Check whether the object entering the trigger is the Player
        if (!other.CompareTag("Player"))
        {
            return;
        }

        // 警告メッセージを表示する
        // Show the warning message
        if (messageObject != null)
        {
            messageObject.SetActive(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // Triggerから出たオブジェクトがPlayerか確認する
        // Check whether the object leaving the trigger is the Player
        if (!other.CompareTag("Player"))
        {
            return;
        }

        // 公園側へ戻ったら警告メッセージを非表示にする
        // Hide the warning message when the Player moves away
        if (messageObject != null)
        {
            messageObject.SetActive(false);
        }
    }
}