using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(FirstPersonController))]
public class PlayerInteraction : MonoBehaviour
{
    // PlayerのMain Cameraです。
    // The Player's Main Camera.
    [SerializeField] private Camera playerCamera;

    // インタラクト可能なときに表示する中央の白いポイントです。
    // The center-screen point shown when interaction is available.
    [SerializeField] private GameObject interactionPoint;

    // ドアを操作できる最大距離です。
    // Maximum interaction distance.
    [SerializeField] private float interactionDistance = 2f;

    // Raycastの対象レイヤーです。
    // Layers checked by the interaction ray.
    [SerializeField] private LayerMask interactionMask = ~0;

    // Playerの操作スクリプトです。
    // The Player controller script.
    private FirstPersonController playerController;

    // 現在Playerが入っているロッカーです。
    // The locker currently occupied by the Player.
    private LockerHideSpot currentLocker;

    private bool interactionEnabled = true;
    private bool previousHidden;

    public event System.Action<bool> HiddenStateChanged;

    // ゲーム開始前に必要なコンポーネントを取得します。
    // Gets required components before gameplay starts.
    private void Awake()
    {
        playerController =
            GetComponent<FirstPersonController>();
    }

    // ゲーム開始時にポイントを非表示にします。
    // Hides the interaction point when gameplay begins.
    private void Start()
    {
        SetInteractionPointVisible(false);
    }

    // 毎フレーム、インタラクト対象と左クリックを確認します。
    // Checks the interaction target and left click every frame.
    private void Update()
    {
        bool hidden = playerController != null && playerController.IsHidden;
        if (hidden != previousHidden)
        {
            previousHidden = hidden;
            HiddenStateChanged?.Invoke(hidden);
        }

        if (!interactionEnabled)
        {
            SetInteractionPointVisible(false);
            return;
        }

        if (playerCamera == null ||
            Cursor.lockState != CursorLockMode.Locked)
        {
            SetInteractionPointVisible(false);
            return;
        }

        ApartmentLoopDoorRoleController storyDoor = null;
        bool canUseStoryDoor = currentLocker == null && TryGetStoryDoorInSight(out storyDoor);

        // ロッカー内で退出可能な状態かを確認します。
        // Checks whether the Player can exit the current locker.
        bool canExitLocker =
            currentLocker != null &&
            playerController.IsHidden;

        // 正面にあるロッカーを保存する変数を先に初期化します。
        // Initializes the locker variable before the conditional check.
         LockerHideSpot lockerInSight = null;

        // 正面に操作可能なロッカーがあるか確認します。
        // Checks whether an interactable locker is in front.
        bool canEnterLocker =
            currentLocker == null &&
            TryGetLockerInSight(out lockerInSight);

        // 入るか出ることができる場合だけポイントを表示します。
        // Shows the point only when entering or exiting is possible.
        SetInteractionPointVisible(
            canExitLocker || canUseStoryDoor || canEnterLocker
        );

        if (Mouse.current == null ||
            !Mouse.current.leftButton.wasPressedThisFrame)
        {
            return;
        }

        // 隠れている状態の左クリックで退出します。
        // Left click exits while hidden.
        if (canExitLocker)
        {
            if (currentLocker.TryExit(playerController))
            {
                currentLocker = null;
                SetInteractionPointVisible(false);
            }

            return;
        }

        if (canUseStoryDoor)
        {
            storyDoor.Interact();
            SetInteractionPointVisible(false);
            return;
        }

        // 正面のロッカーへ入ります。
        // Enters the locker currently in sight.
        if (canEnterLocker &&
            lockerInSight.TryEnter(playerController))
        {
            currentLocker = lockerInSight;
            SetInteractionPointVisible(false);
        }
    }

    private bool TryGetStoryDoorInSight(out ApartmentLoopDoorRoleController door)
    {
        door = null;
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, interactionDistance, interactionMask, QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        door = hit.collider.GetComponentInParent<ApartmentLoopDoorRoleController>();

        // 自宅ドア演出中は、視線がHideDoor本体ではなく同じLockerの
        // Bodyや蝶番Colliderへ当たる場合も、そのFloorの代表ドアへ解決する。
        // 通常のHideDoor状態ではHandlesStoryInteractionがfalseなので、
        // 既存のLockerHideSpot判定には影響しない。
        if (door == null)
        {
            Transform lockerRoot = hit.collider.transform;
            while (lockerRoot != null && lockerRoot.name != "Locker")
            {
                lockerRoot = lockerRoot.parent;
            }

            if (lockerRoot != null)
            {
                door = lockerRoot.GetComponentInChildren<
                    ApartmentLoopDoorRoleController>(true);
            }
        }

        return door != null && door.HandlesStoryInteraction;
    }

    public void SetInteractionEnabled(bool enabled)
    {
        interactionEnabled = enabled;
        if (!enabled) SetInteractionPointVisible(false);
    }

    // カメラ正面に操作可能なロッカーがあるか確認します。
    // Checks for an interactable locker in front of the camera.
    private bool TryGetLockerInSight(
        out LockerHideSpot locker)
    {
        locker = null;

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
            return false;
        }

        locker =
            hit.collider.GetComponentInParent<LockerHideSpot>();

        if (locker == null) return false;

        ApartmentLoopDoorRoleController role =
            locker.GetComponentInChildren<
                ApartmentLoopDoorRoleController>(true);

        return role == null ||
            role.Role == ApartmentLoopDoorRole.HideDoor;
    }

    // 中央ポイントの表示状態を切り替えます。
    // Changes the visibility of the center interaction point.
    private void SetInteractionPointVisible(bool visible)
    {
        if (interactionPoint != null &&
            interactionPoint.activeSelf != visible)
        {
            interactionPoint.SetActive(visible);
        }
    }
}
