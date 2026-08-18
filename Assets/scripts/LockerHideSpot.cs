using System.Collections;
using UnityEngine;

public class LockerHideSpot : MonoBehaviour
{
    [Header("Door Animation")]
    [SerializeField] private bool animateDoor = true;

    // 回転させるドアの蝶番です。
    // The door hinge that will be rotated.
    [SerializeField] private Transform doorHinge;

    // 収納後のPlayer位置です。
    // The Player position inside the locker.
    [SerializeField] private Transform hidePoint;

    // 退出後のPlayer位置です。
    // The Player position outside the locker.
    [SerializeField] private Transform exitPoint;

    // ドアを開くY角度です。
    // Door opening angle around the local Y axis.
    [SerializeField] private float openAngleY = -90f;

    // ドアの開閉にかける時間です。
    // Duration of the door animation.
    [SerializeField] private float doorDuration = 0.4f;

    // Playerを内部へ移動させる時間です。
    // Duration of the Player movement into the locker.
    [SerializeField] private float enterMoveDuration = 0.45f;

    // ドアが閉じているときの回転です。
    // Door rotation in the closed state.
    private Quaternion closedRotation;

    // ドアが開いているときの回転です。
    // Door rotation in the open state.
    private Quaternion openRotation;

    // 現在収納されているPlayerです。
    // The Player currently occupying this locker.
    private FirstPersonController currentPlayer;

    // PlayerのCharacterControllerです。
    // The Player CharacterController.
    private CharacterController characterController;

    // 入退室処理中かを記録します。
    // Whether an enter or exit operation is running.
    private bool isTransitioning;

    // Playerが中にいるかを記録します。
    // Whether the Player is inside this locker.
    private bool isOccupied;

    // ゲーム開始前にドアの開閉角度を保存します。
    // Stores the closed and open door rotations.
    private void Awake()
    {
        if (!animateDoor || doorHinge == null)
        {
            return;
        }

        closedRotation = doorHinge.localRotation;

        openRotation =
            closedRotation *
            Quaternion.Euler(0f, openAngleY, 0f);
    }

    // Playerの収納処理を開始します。
    // Starts the locker-entry operation.
    public bool TryEnter(FirstPersonController player)
    {
        if (isTransitioning ||
            isOccupied ||
            player == null ||
            (animateDoor && doorHinge == null) ||
            hidePoint == null)
        {
            return false;
        }

        CharacterController foundController =
            player.GetCharacterController();

        if (foundController == null)
        {
            return false;
        }

        currentPlayer = player;
        characterController = foundController;

        StartCoroutine(EnterRoutine());
        return true;
    }

    // Playerをロッカー正面へ退出させます。
    // Moves the Player directly outside the locker.
    public bool TryExit(FirstPersonController player)
    {
        if (isTransitioning ||
            !isOccupied ||
            player == null ||
            player != currentPlayer ||
            exitPoint == null)
        {
            return false;
        }

        ExitImmediately();
        return true;
    }

    // ドアを開き、Playerを収納して、ドアを閉じます。
    // Opens the door, moves the Player inside, and closes the door.
    private IEnumerator EnterRoutine()
    {
        isTransitioning = true;

        // 入る途中は移動と視点操作を停止します。
        // Disables movement and camera look during entry.
        currentPlayer.SetMovementEnabled(false);
        currentPlayer.SetLookEnabled(false);
        currentPlayer.ResetLook();

        // 移動中に壁やドアへ引っ掛からないようにします。
        // Disables collision during the transition.
        characterController.enabled = false;

        // ドアを外側へ開きます。
        // Opens the door outward.
        if (animateDoor)
        {
            yield return RotateDoor(openRotation);
        }

        // PlayerをHidePointへ移動させます。
        // Moves the Player to the HidePoint.
        yield return MovePlayerToHidePoint();

        // ドアを閉じます。
        // Closes the door.
        if (animateDoor)
        {
            yield return RotateDoor(closedRotation);
        }

        // 敵から見えない状態にします。
        // Marks the Player as hidden from enemies.
        currentPlayer.SetHidden(true);

        // ロッカー内では視点を正面に固定します。
        // Keeps the camera fixed forward while hidden.
        currentPlayer.ResetLook();
        currentPlayer.SetLookEnabled(false);

        isOccupied = true;
        isTransitioning = false;
    }

    public void ConfigureWithoutDoor(Transform hiddenPosition, Transform outsidePosition)
    {
        animateDoor = false;
        doorHinge = null;
        hidePoint = hiddenPosition;
        exitPoint = outsidePosition;
    }

    // 退出時はドアを動かさず、Playerだけを移動させます。
    // Exits without animating the door.
    private void ExitImmediately()
    {
        isTransitioning = true;

        currentPlayer.SetMovementEnabled(false);
        currentPlayer.SetLookEnabled(false);
        currentPlayer.ResetLook();

        // PlayerをExitPointへ直接移動させます。
        // Moves the Player directly to the ExitPoint.
        currentPlayer.transform.SetPositionAndRotation(
            exitPoint.position,
            exitPoint.rotation
        );

        // Playerの衝突判定を戻します。
        // Re-enables the Player collision.
        characterController.enabled = true;

        // 敵から見える状態へ戻します。
        // Makes the Player visible to enemies again.
        currentPlayer.SetHidden(false);

        // 通常操作を再開します。
        // Restores normal movement and camera look.
        currentPlayer.SetMovementEnabled(true);
        currentPlayer.SetLookEnabled(true);

        isOccupied = false;
        isTransitioning = false;

        currentPlayer = null;
        characterController = null;
    }

    // ドアを指定された角度まで滑らかに回転させます。
    // Smoothly rotates the door to the target rotation.
    private IEnumerator RotateDoor(
        Quaternion targetRotation)
    {
        Quaternion startRotation =
            doorHinge.localRotation;

        float duration =
            Mathf.Max(doorDuration, 0.01f);

        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;

            float progress =
                Mathf.Clamp01(elapsedTime / duration);

            // 開始時と終了時を滑らかにします。
            // Smooths acceleration and deceleration.
            float smoothProgress =
                progress * progress *
                (3f - 2f * progress);

            doorHinge.localRotation =
                Quaternion.Slerp(
                    startRotation,
                    targetRotation,
                    smoothProgress
                );

            yield return null;
        }

        doorHinge.localRotation = targetRotation;
    }

    // PlayerをHidePointまで滑らかに移動させます。
    // Smoothly moves the Player to the HidePoint.
    private IEnumerator MovePlayerToHidePoint()
    {
        Transform playerTransform =
            currentPlayer.transform;

        Vector3 startPosition =
            playerTransform.position;

        Quaternion startRotation =
            playerTransform.rotation;

        float duration =
            Mathf.Max(enterMoveDuration, 0.01f);

        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;

            float progress =
                Mathf.Clamp01(elapsedTime / duration);

            float smoothProgress =
                progress * progress *
                (3f - 2f * progress);

            playerTransform.position =
                Vector3.Lerp(
                    startPosition,
                    hidePoint.position,
                    smoothProgress
                );

            playerTransform.rotation =
                Quaternion.Slerp(
                    startRotation,
                    hidePoint.rotation,
                    smoothProgress
                );

            yield return null;
        }

        playerTransform.SetPositionAndRotation(
            hidePoint.position,
            hidePoint.rotation
        );
    }
}
