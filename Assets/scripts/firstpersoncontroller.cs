using UnityEngine;
using UnityEngine.InputSystem;

// CharacterControllerがない場合は自動で追加します。
// Automatically adds a CharacterController when missing.
[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : MonoBehaviour
{
    // Playerの子になっているMain Cameraです。
    // The Main Camera parented under the Player.
    [SerializeField] private Transform cameraTransform;

    // 通常歩行速度です。
    // Normal walking speed.
    [SerializeField] private float moveSpeed = 3f;

    // ダッシュ速度です。
    // Sprinting speed.
    [SerializeField] private float sprintSpeed = 5.5f;

    // しゃがみ中の移動速度です。
    // Movement speed while crouching.
    [SerializeField] private float crouchSpeed = 1.5f;

    // マウス感度です。
    // Mouse-look sensitivity.
    [SerializeField] private float mouseSensitivity = 0.12f;

    // 重力です。
    // Gravity applied to the Player.
    [SerializeField] private float gravity = -20f;

    // 視点を上下に向けられる最大角度です。
    // Maximum vertical camera angle.
    [SerializeField] private float maxLookAngle = 85f;

    [Header("Crouch Settings")]

    // 立っているときのCharacterControllerの高さです。
    // CharacterController height while standing.
    [SerializeField] private float standingHeight = 1.5f;

    // しゃがんでいるときのCharacterControllerの高さです。
    // CharacterController height while crouching.
    [SerializeField] private float crouchingHeight = 0.9f;

    // 立っているときのカメラのLocal Position Yです。
    // Camera local Y position while standing.
    [SerializeField] private float standingCameraY = 1.35f;

    // しゃがんでいるときのカメラのLocal Position Yです。
    // Camera local Y position while crouching.
    [SerializeField] private float crouchingCameraY = 0.75f;

    // しゃがみ・立ち上がりの速さです。
    // Speed of the crouch transition.
    [SerializeField] private float crouchTransitionSpeed = 4f;

    // PlayerのCharacterControllerです。
    // CharacterController attached to the Player.
    private CharacterController controller;

    // 現在の上下方向の速度です。
    // Current vertical velocity.
    private float verticalVelocity;

    // 現在のカメラ上下角度です。
    // Current vertical camera angle.
    private float pitch;

    // 移動を許可するかどうかです。
    // Determines whether movement is enabled.
    private bool movementEnabled = true;

    // 視点操作を許可するかどうかです。
    // Determines whether camera look is enabled.
    private bool lookEnabled = true;

    // 敵から見えない状態かどうかです。
    // Whether the Player is hidden from enemies.
    public bool IsHidden { get; private set; }

    // 現在しゃがみ状態かどうかです。
    // Whether the Player is currently crouching.
    public bool IsCrouching { get; private set; }

    // ゲーム開始前に必要なコンポーネントを取得します。
    // Gets required components before gameplay begins.
    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        // 開始時は立った状態にします。
        // Initializes the controller in the standing state.
        controller.height = standingHeight;

        Vector3 controllerCenter = controller.center;
        controllerCenter.y = standingHeight * 0.5f;
        controller.center = controllerCenter;

        if (cameraTransform != null)
        {
            Vector3 cameraPosition =
                cameraTransform.localPosition;

            cameraPosition.y = standingCameraY;
            cameraTransform.localPosition = cameraPosition;
        }
    }

    // ゲーム開始時にカーソルを固定します。
    // Locks the cursor when gameplay begins.
    private void Start()
    {
        LockCursor();
    }

    // 毎フレーム、しゃがみ・移動・視点操作を更新します。
    // Updates crouching, movement, and camera look every frame.
    private void Update()
    {
        bool canMove =
            movementEnabled &&
            controller != null &&
            controller.enabled &&
            gameObject.activeInHierarchy;

        if (canMove)
        {
            HandleCrouch();
            HandleMovement();
        }

        if (lookEnabled)
        {
            HandleLook();
        }

        // Escキーでカーソルを解放します。
        // Releases the cursor with Escape.
        if (Keyboard.current != null &&
            Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // 左クリックでカーソルを再固定します。
        // Locks the cursor again with a left click.
        if (Mouse.current != null &&
            Mouse.current.leftButton.wasPressedThisFrame &&
            Cursor.lockState != CursorLockMode.Locked)
        {
            LockCursor();
        }
    }

    // 左Ctrl入力とCharacterControllerの高さを処理します。
    // Handles Left Ctrl input and controller height.
    private void HandleCrouch()
    {
        if (Keyboard.current == null ||
            cameraTransform == null)
        {
            return;
        }

        // 左Ctrlを押している間、しゃがみを要求します。
        // Requests crouching while Left Ctrl is held.
        bool crouchKeyHeld =
            Keyboard.current.leftCtrlKey.isPressed;

        bool controllerIsShort =
            controller.height <
            standingHeight - 0.02f;

        // 頭上に障害物がある場合は立ち上がらせません。
        // Keeps the Player crouched when overhead space is blocked.
        bool mustRemainCrouched =
            controllerIsShort &&
            !crouchKeyHeld &&
            !CanStandUp();

        bool shouldCrouch =
            crouchKeyHeld || mustRemainCrouched;

        float targetHeight =
            shouldCrouch
                ? crouchingHeight
                : standingHeight;

        float targetCameraY =
            shouldCrouch
                ? crouchingCameraY
                : standingCameraY;

        // Colliderの高さを滑らかに変更します。
        // Smoothly changes the controller height.
        controller.height =
            Mathf.MoveTowards(
                controller.height,
                targetHeight,
                crouchTransitionSpeed * Time.deltaTime
            );

        // 足元の位置を変えないようCenterを調整します。
        // Adjusts the center while keeping the feet in place.
        Vector3 controllerCenter = controller.center;

        controllerCenter.y =
            controller.height * 0.5f;

        controller.center = controllerCenter;

        // カメラを滑らかに上下させます。
        // Smoothly moves the camera vertically.
        Vector3 cameraPosition =
            cameraTransform.localPosition;

        cameraPosition.y =
            Mathf.MoveTowards(
                cameraPosition.y,
                targetCameraY,
                crouchTransitionSpeed * Time.deltaTime
            );

        cameraTransform.localPosition = cameraPosition;

        IsCrouching =
            shouldCrouch ||
            controller.height <
            standingHeight - 0.02f;
    }

    // 立った状態のカプセルが障害物と重ならないか確認します。
    // Checks whether the standing capsule overlaps any obstacles.
    private bool CanStandUp()
    {
        float checkRadius =
            Mathf.Max(
                0.01f,
                controller.radius - 0.02f
            );

        Vector3 standingCenterLocal =
            controller.center;

        standingCenterLocal.y =
            standingHeight * 0.5f;

        Vector3 standingCenterWorld =
            transform.TransformPoint(
                standingCenterLocal
            );

        float capsuleHalfSegment =
            Mathf.Max(
                0f,
                standingHeight * 0.5f -
                checkRadius
            );

        Vector3 bottomPoint =
            standingCenterWorld +
            Vector3.down * capsuleHalfSegment;

        Vector3 topPoint =
            standingCenterWorld +
            Vector3.up * capsuleHalfSegment;

        Collider[] overlappingColliders =
            Physics.OverlapCapsule(
                bottomPoint,
                topPoint,
                checkRadius,
                ~0,
                QueryTriggerInteraction.Ignore
            );

        foreach (Collider hitCollider in overlappingColliders)
        {
            if (hitCollider == null)
            {
                continue;
            }

            // Player自身のColliderは無視します。
            // Ignores colliders belonging to the Player.
            if (hitCollider.transform == transform ||
                hitCollider.transform.IsChildOf(transform))
            {
                continue;
            }

            return false;
        }

        return true;
    }

    // WASD移動・ダッシュ・重力を処理します。
    // Handles WASD movement, sprinting, and gravity.
    private void HandleMovement()
    {
        Vector2 input = Vector2.zero;
        Keyboard keyboard = Keyboard.current;

        if (keyboard != null)
        {
            if (keyboard.wKey.isPressed)
            {
                input.y += 1f;
            }

            if (keyboard.sKey.isPressed)
            {
                input.y -= 1f;
            }

            if (keyboard.aKey.isPressed)
            {
                input.x -= 1f;
            }

            if (keyboard.dKey.isPressed)
            {
                input.x += 1f;
            }
        }

        // 斜め移動だけ速くならないようにします。
        // Prevents faster diagonal movement.
        input = Vector2.ClampMagnitude(input, 1f);

        Vector3 horizontalMovement =
            transform.right * input.x +
            transform.forward * input.y;

        if (controller.isGrounded &&
            verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
        }

        verticalVelocity +=
            gravity * Time.deltaTime;

        // しゃがみ中はダッシュできません。
        // Disables sprinting while crouching.
        bool isSprinting =
            keyboard != null &&
            keyboard.leftShiftKey.isPressed &&
            !IsCrouching;

        float currentSpeed;

        if (IsCrouching)
        {
            currentSpeed = crouchSpeed;
        }
        else if (isSprinting)
        {
            currentSpeed = sprintSpeed;
        }
        else
        {
            currentSpeed = moveSpeed;
        }

        Vector3 motion =
            horizontalMovement * currentSpeed;

        motion.y = verticalVelocity;

        if (controller != null &&
            controller.enabled)
        {
            controller.Move(
                motion * Time.deltaTime
            );
        }
    }

    // マウスによる視点操作を処理します。
    // Handles mouse-look input.
    private void HandleLook()
    {
        if (Mouse.current == null ||
            cameraTransform == null ||
            Cursor.lockState != CursorLockMode.Locked)
        {
            return;
        }

        Vector2 mouseDelta =
            Mouse.current.delta.ReadValue() *
            mouseSensitivity;

        pitch -= mouseDelta.y;

        pitch = Mathf.Clamp(
            pitch,
            -maxLookAngle,
            maxLookAngle
        );

        cameraTransform.localRotation =
            Quaternion.Euler(
                pitch,
                0f,
                0f
            );

        transform.Rotate(
            Vector3.up * mouseDelta.x
        );
    }

    // 移動の有効・無効を切り替えます。
    // Enables or disables movement.
    public void SetMovementEnabled(bool enabled)
    {
        movementEnabled = enabled;

        if (!enabled)
        {
            verticalVelocity = 0f;
        }
    }

    // 視点操作の有効・無効を切り替えます。
    // Enables or disables camera look.
    public void SetLookEnabled(bool enabled)
    {
        lookEnabled = enabled;
    }

    // 敵から見えない状態を設定します。
    // Sets whether the Player is hidden.
    public void SetHidden(bool hidden)
    {
        IsHidden = hidden;
    }

    // カメラの上下角度を正面へ戻します。
    // Resets the vertical camera angle.
    public void ResetLook()
    {
        pitch = 0f;

        if (cameraTransform != null)
        {
            cameraTransform.localRotation =
                Quaternion.identity;
        }
    }

    // CharacterControllerを返します。
    // Returns the CharacterController.
    public CharacterController GetCharacterController()
    {
        return controller;
    }

    // カーソルを固定して非表示にします。
    // Locks and hides the cursor.
    private void LockCursor()
    {
        Cursor.lockState =
            CursorLockMode.Locked;

        Cursor.visible = false;
    }
}