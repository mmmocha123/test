using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(IntroOutdoorInteractable))]
public class IntroOutdoorPickupItem : MonoBehaviour
{
    [Header("Pickup")]
    [SerializeField]
    private IntroOutdoorPickupUI pickupUI;

    [SerializeField]
    private Texture itemTexture;

    [SerializeField]
    [TextArea(2, 5)]
    private string description;

    [Header("After Pickup")]
    [SerializeField]
    private GameObject objectToHideAfterPickup;

    [SerializeField]
    private UnityEvent onCollected;

    private IntroOutdoorInteractable interactable;

    private bool collected;

    private void Awake()
    {
        // 同じGameObjectからInteractableを取得する
        // Get the Interactable from the same GameObject
        interactable =
            GetComponent<IntroOutdoorInteractable>();
    }

    public void BeginPickup()
    {
        // 既に取得済みなら何もしない
        // Do nothing if the item has already been collected
        if (collected)
        {
            return;
        }

        // PickupUIが未設定なら何もしない
        // Do nothing if the PickupUI is not assigned
        if (pickupUI == null)
        {
            return;
        }

        // 取得画面中の再インタラクトを禁止する
        // Prevent repeated interaction while the pickup screen is open
        if (interactable != null)
        {
            interactable.SetInteractionEnabled(false);
        }

        // 取得画面を開く
        // Open the pickup screen
        bool opened =
            pickupUI.OpenPickup(
                itemTexture,
                description,
                CompletePickup
            );

        // 開けなかった場合は再び操作可能にする
        // Re-enable interaction if opening failed
        if (
            !opened &&
            interactable != null
        )
        {
            interactable.SetInteractionEnabled(true);
        }
    }

    private void CompletePickup()
    {
        // 二重取得を防止する
        // Prevent duplicate collection
        if (collected)
        {
            return;
        }

        // 取得済みにする
        // Mark the item as collected
        collected = true;

        // アイテム固有の取得イベントを実行する
        // Invoke item-specific collection events
        onCollected?.Invoke();

        // 指定されたオブジェクトをSceneから消す
        // Hide the assigned world object
        if (objectToHideAfterPickup != null)
        {
            objectToHideAfterPickup.SetActive(false);
            return;
        }

        // 未指定なら自身を消す
        // Hide this GameObject if no target is assigned
        gameObject.SetActive(false);
    }
}