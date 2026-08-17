using UnityEngine;
using UnityEngine.Events;

public class IntroOutdoorInteractable : MonoBehaviour
{
    // このオブジェクトを現在操作できるかを設定します。
    // Determines whether this object can currently be interacted with.
    [SerializeField] private bool interactionEnabled = true;

    // 左クリック時に実行する処理をInspectorから登録します。
    // Functions invoked from the Inspector when left-clicked.
    [SerializeField] private UnityEvent onInteract;

    // 現在操作可能かを外部へ公開します。
    // Exposes whether interaction is currently available.
    public bool CanInteract =>
        interactionEnabled &&
        enabled &&
        gameObject.activeInHierarchy;

    // Playerから呼び出されるインタラクト処理です。
    // Interaction method called by the Player.
    public void Interact()
    {
        if (!CanInteract)
        {
            return;
        }

        // 動作確認用にConsoleへ対象名を表示します。
        // Logs the target name for testing.
        Debug.Log(
            $"Interacted with: {gameObject.name}",
            this
        );

        // Inspectorで登録された処理を実行します。
        // Invokes functions assigned in the Inspector.
        onInteract?.Invoke();
    }

    // 外部処理からインタラクト可否を変更します。
    // Allows external systems to change interaction availability.
    public void SetInteractionEnabled(bool enabledState)
    {
        interactionEnabled = enabledState;
    }
}