using UnityEngine;
using UnityEngine.InputSystem;

// CharacterControllerがない場合は自動的に追加します。
// Automatically adds a CharacterController when missing.
[RequireComponent(typeof(CharacterController))]
public class IntroOutdoorPlayerController : MonoBehaviour
{
    // Playerの子になっているMain Cameraを指定します。
    // Assigns the Main Camera parented under the Player.
    [SerializeField] private Transform cameraTransform;

    // 通常歩行の速度です。単位はm/sです。
    // Normal walking speed in meters per second.
    [SerializeField] private float walkSpeed = 3f;

    // 左Shiftを押している間のダッシュ速度です。
    // Sprint speed while Left Shift is held.
    [SerializeField] private float sprintSpeed = 5.5f;

    // マウスによる視点移動の感度です。
    // Mouse-look sensitivity.
    [SerializeField] private float mouseSensitivity = 0.12f;

    // Playerへ加える重力です。
    // Gravity applied to the Player.
    [SerializeField] private float gravity = -20f;

    // カメラを上下へ動かせる最大角度です。
    // Maximum vertical camera angle.
    [SerializeField] private float maxLookAngle = 85f;

    // Playerに付いているCharacterControllerです。
    // CharacterController attached to the Player.
    private CharacterController controller;

    // 現在の上下方向の速度です。
    // Current vertical velocity.
    private float verticalVelocity;

    // 現在のカメラ上下角度です。
    // Current vertical camera angle.
    private float pitch;

    // 移動入力を受け付けるかどうかです。
    // Determines whether movement input is enabled.
    private bool movementEnabled = true;

    // 視点入力を受け付けるかどうかです。
    // Determines whether camera-look input is enabled.
    private bool lookEnabled = true;

    // ゲーム開始前にCharacterControllerを取得します。
    // Gets the CharacterController before gameplay begins.
    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    // ゲーム開始時にカーソルを中央へ固定します。
    // Locks the cursor when gameplay begins.
    private void Start()
    {
        LockCursor();
    }

    // 毎フレーム、移動・視点・カーソル操作を更新します。
    // Updates movement, camera look, and cursor control every frame.
    private void Update()
    {
        // CharacterControllerが有効な場合だけ移動処理を実行します。
        // Runs movement only while the CharacterController is active.
        if (movementEnabled &&
            controller != null &&
            controller.enabled &&
            gameObject.activeInHierarchy)
        {
            HandleMovement();
        }

        if (lookEnabled)
        {
            HandleLook();
        }

        // Escキーでカーソルを解放します。
        // Releases the cursor when Escape is pressed.
        if (Keyboard.current != null &&
            Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // カーソル解放中に左クリックすると再固定します。
        // Locks the cursor again when left-clicking while unlocked.
        if (Mouse.current != null &&
            Mouse.current.leftButton.wasPressedThisFrame &&
            Cursor.lockState != CursorLockMode.Locked)
        {
            LockCursor();
        }
    }

    // WASD移動、ダッシュ、重力を処理します。
    // Handles WASD movement, sprinting, and gravity.
    private void HandleMovement()
    {
        Vector2 input = Vector2.zero;
        Keyboard keyboard = Keyboard.current;

        if (keyboard != null)
        {
            // WとSで前後へ移動します。
            // W and S control forward and backward movement.
            if (keyboard.wKey.isPressed)
            {
                input.y += 1f;
            }

            if (keyboard.sKey.isPressed)
            {
                input.y -= 1f;
            }

            // AとDで左右へ移動します。
            // A and D control left and right movement.
            if (keyboard.aKey.isPressed)
            {
                input.x -= 1f;
            }

            if (keyboard.dKey.isPressed)
            {
                input.x += 1f;
            }
        }

        // 斜め移動だけ速くならないよう入力を制限します。
        // Prevents diagonal movement from becoming faster.
        input = Vector2.ClampMagnitude(input, 1f);

        // Playerの向きを基準に移動方向を計算します。
        // Calculates movement relative to the Player orientation.
        Vector3 horizontalMovement =
            transform.right * input.x +
            transform.forward * input.y;

        // 接地中は少しだけ下方向へ押します。
        // Maintains a small downward velocity while grounded.
        if (controller.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
        }

        // 現在の上下速度へ重力を加えます。
        // Adds gravity to the current vertical velocity.
        verticalVelocity += gravity * Time.deltaTime;

        // 左Shiftが押されているか確認します。
        // Checks whether Left Shift is held.
        bool isSprinting =
            keyboard != null &&
            keyboard.leftShiftKey.isPressed;

        // 通常速度またはダッシュ速度を選択します。
        // Selects walking or sprinting speed.
        float currentSpeed =
            isSprinting ? sprintSpeed : walkSpeed;

        // 水平移動と上下移動をまとめます。
        // Combines horizontal and vertical movement.
        Vector3 motion =
            horizontalMovement * currentSpeed;

        motion.y = verticalVelocity;

        // CharacterControllerが有効な場合だけ移動します。
        // Moves only while the CharacterController is active.
        if (controller != null && controller.enabled)
        {
            controller.Move(motion * Time.deltaTime);
        }
    }

    // マウスによる一人称視点操作を処理します。
    // Handles first-person mouse-look input.
    private void HandleLook()
    {
        if (Mouse.current == null ||
            cameraTransform == null ||
            Cursor.lockState != CursorLockMode.Locked)
        {
            return;
        }

        // 現在フレームのマウス移動量を取得します。
        // Reads mouse movement for the current frame.
        Vector2 mouseDelta =
            Mouse.current.delta.ReadValue() *
            mouseSensitivity;

        // カメラの上下角度を計算します。
        // Calculates the vertical camera angle.
        pitch -= mouseDelta.y;

        pitch = Mathf.Clamp(
            pitch,
            -maxLookAngle,
            maxLookAngle
        );

        // 上下回転はカメラへ適用します。
        // Applies vertical rotation to the camera.
        cameraTransform.localRotation =
            Quaternion.Euler(pitch, 0f, 0f);

        // 左右回転はPlayer全体へ適用します。
        // Applies horizontal rotation to the entire Player.
        transform.Rotate(Vector3.up * mouseDelta.x);
    }

    // 外部処理から移動を停止・再開できます。
    // Allows external systems to enable or disable movement.
    public void SetMovementEnabled(bool enabledState)
    {
        movementEnabled = enabledState;

        if (!enabledState)
        {
            verticalVelocity = 0f;
        }
    }

    // 外部処理から視点操作を停止・再開できます。
    // Allows external systems to enable or disable camera look.
    public void SetLookEnabled(bool enabledState)
    {
        lookEnabled = enabledState;
    }

    // カーソルを中央へ固定して非表示にします。
    // Locks and hides the cursor.
    private void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}