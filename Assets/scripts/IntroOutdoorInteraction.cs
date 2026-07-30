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

    // 現在カメラ中央にある操作対象です。
    // Current interactable target in the camera center.
    private IntroOutdoorInteractable currentTarget;

    // ゲーム開始時に白点を非表示にします。
    // Hides the interaction point when gameplay begins.
    private void Start()
    {
        SetInteractionPointVisible(false);
    }

    // 毎フレーム、対象確認と左クリック入力を処理します。
    // Checks the target and left-click input every frame.
    private void Update()
    {
        // カメラ中央にある操作対象を取得します。
        // Finds the interactable in the center of the camera.
        currentTarget = FindInteractableTarget();

        bool canInteract =
            currentTarget != null;

        // 操作可能な対象がある間だけ白点を表示します。
        // Shows the white point only while a valid target is present.
        SetInteractionPointVisible(canInteract);

        if (!canInteract ||
            Mouse.current == null ||
            Cursor.lockState != CursorLockMode.Locked ||
            !Mouse.current.leftButton.wasPressedThisFrame)
        {
            return;
        }

        // 左クリックされた対象の処理を実行します。
        // Interacts with the target when left-clicked.
        currentTarget.Interact();
    }

    // カメラ正面へRaycastを飛ばして操作対象を探します。
    // Casts a ray forward from the camera to find an interactable.
    private IntroOutdoorInteractable FindInteractableTarget()
    {
        if (playerCamera == null ||
            Cursor.lockState != CursorLockMode.Locked)
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

        // Collider自身または親から操作用コンポーネントを探します。
        // Searches the hit collider and its parents for an interactable.
        IntroOutdoorInteractable interactable =
            hit.collider.GetComponentInParent<
                IntroOutdoorInteractable>();

        if (interactable == null ||
            !interactable.CanInteract)
        {
            return null;
        }

        return interactable;
    }

    // 白点の表示・非表示を切り替えます。
    // Changes the visibility of the white interaction point.
    private void SetInteractionPointVisible(bool visible)
    {
        if (interactionPoint != null &&
            interactionPoint.activeSelf != visible)
        {
            interactionPoint.SetActive(visible);
        }
    }
}