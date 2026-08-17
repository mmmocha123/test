using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(IntroOutdoorPlayerController))]
public class IntroOutdoorInteraction : MonoBehaviour
{
    // PlayerのMain Cameraを指定します。
    // Assigns the Player's Main Camera.
    [SerializeField] private Camera playerCamera;

    // インタラクト可能なときに表示する白点です。
    // White point shown when interaction is available.
    [SerializeField] private GameObject interactionPoint;

    // 操作可能な最大距離です。
    // Maximum interaction distance.
    [SerializeField] private float interactionDistance = 2f;

    // Raycastで調べるレイヤーです。
    // Layers checked by the interaction ray.
    [SerializeField] private LayerMask interactionMask = ~0;

    // 文章表示状態を管理するManagerです。
    // Manager that controls dialogue interaction blocking.
    [SerializeField]
    private IntroOutdoorDialogueManager dialogueManager;

    // 現在カメラ中央にある操作対象です。
    // Current interactable target in the camera center.
    private IntroOutdoorInteractable currentTarget;

    private void Start()
    {
        // ゲーム開始時に白点を非表示にする
        // Hide the interaction point when gameplay begins
        SetInteractionPointVisible(false);
    }

    private void OnDisable()
    {
        // 無効化されたときに対象情報を消す
        // Clear the current target when disabled
        currentTarget = null;

        SetInteractionPointVisible(false);
    }

    private void Update()
    {
        // 文章表示中または終了後クールダウン中は操作を禁止する
        // Block interaction during dialogue and its cooldown period
        if (
            dialogueManager != null &&
            dialogueManager.IsWorldInteractionBlocked
        )
        {
            currentTarget = null;
            SetInteractionPointVisible(false);
            return;
        }

        // カメラ中央にある操作対象を取得する
        // Find the interactable in the center of the camera
        currentTarget = FindInteractableTarget();

        bool canInteract =
            currentTarget != null;

        // 操作可能な対象がある場合だけ白点を表示する
        // Show the interaction point only for a valid target
        SetInteractionPointVisible(canInteract);

        if (!canInteract)
        {
            return;
        }

        if (Mouse.current == null)
        {
            return;
        }

        if (Cursor.lockState != CursorLockMode.Locked)
        {
            return;
        }

        if (!Mouse.current.leftButton.wasPressedThisFrame)
        {
            return;
        }

        // 対象をインタラクトする
        // Interact with the current target
        currentTarget.Interact();
    }

    private IntroOutdoorInteractable FindInteractableTarget()
    {
        if (
            playerCamera == null ||
            Cursor.lockState != CursorLockMode.Locked
        )
        {
            return null;
        }

        Ray interactionRay = new Ray(
            playerCamera.transform.position,
            playerCamera.transform.forward
        );

        bool didHit = Physics.Raycast(
            interactionRay,
            out RaycastHit hit,
            interactionDistance,
            interactionMask,
            QueryTriggerInteraction.Ignore
        );

        if (!didHit)
        {
            return null;
        }

        // Collider自身または親からInteractableを探す
        // Search the hit collider and its parents for an interactable
        IntroOutdoorInteractable interactable =
            hit.collider.GetComponentInParent<
                IntroOutdoorInteractable>();

        if (
            interactable == null ||
            !interactable.CanInteract
        )
        {
            return null;
        }

        return interactable;
    }

    private void SetInteractionPointVisible(bool visible)
    {
        if (
            interactionPoint != null &&
            interactionPoint.activeSelf != visible
        )
        {
            interactionPoint.SetActive(visible);
        }
    }
}