using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class IntroOutdoorPickupUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject pickupPanel;
    [SerializeField] private RawImage itemImage;
    [SerializeField] private TMP_Text descriptionText;

    [Header("Player")]
    [SerializeField]
    private IntroOutdoorPlayerController playerController;

    [SerializeField]
    private IntroOutdoorInteraction playerInteraction;

    [SerializeField]
    private GameObject interactionPoint;

    private bool isOpen;
    private int openedFrame;

    private bool previousPlayerControllerState;
    private bool previousPlayerInteractionState;

    private Action onClosed;

    public bool IsOpen => isOpen;

    private void Start()
    {
        // ゲーム開始時は取得画面を非表示にする
        // Hide the pickup screen when the game starts
        if (pickupPanel != null)
        {
            pickupPanel.SetActive(false);
        }
    }

    private void Update()
    {
        // 取得画面が開いていなければ処理しない
        // Do nothing while the pickup screen is closed
        if (!isOpen)
        {
            return;
        }

        // 取得に使ったクリックと同じフレームでは閉じない
        // Do not close on the same click that opened the screen
        if (Time.frameCount <= openedFrame)
        {
            return;
        }

        // マウスが存在しなければ処理しない
        // Do nothing if no mouse device exists
        if (Mouse.current == null)
        {
            return;
        }

        // 左クリックされた瞬間だけ処理する
        // Process only a new left-click
        if (!Mouse.current.leftButton.wasPressedThisFrame)
        {
            return;
        }

        ClosePickup();
    }

    public bool OpenPickup(
        Texture texture,
        string description,
        Action closeAction)
    {
        // 既に取得画面が開いていれば新しく開かない
        // Do not open another pickup screen while one is active
        if (isOpen)
        {
            return false;
        }

        // 必須UIが未設定なら処理しない
        // Do nothing if required UI references are missing
        if (
            pickupPanel == null ||
            itemImage == null ||
            descriptionText == null
        )
        {
            return false;
        }

        // 開いたフレームを記録する
        // Store the frame on which the screen opened
        openedFrame = Time.frameCount;

        // 取得画面が開いている状態にする
        // Mark the pickup screen as open
        isOpen = true;

        // 閉じた後に実行する処理を保存する
        // Store the action to invoke after closing
        onClosed = closeAction;

        // アイテム画像を設定する
        // Assign the item image
        itemImage.texture = texture;

        // 説明文を設定する
        // Assign the item description
        descriptionText.text = description;

        // 現在のPlayerController状態を保存して停止する
        // Store and disable the PlayerController
        if (playerController != null)
        {
            previousPlayerControllerState =
                playerController.enabled;

            playerController.enabled = false;
        }

        // 現在のInteraction状態を保存して停止する
        // Store and disable world interaction
        if (playerInteraction != null)
        {
            previousPlayerInteractionState =
                playerInteraction.enabled;

            playerInteraction.enabled = false;
        }

        // 白点を消す
        // Hide the interaction point
        if (interactionPoint != null)
        {
            interactionPoint.SetActive(false);
        }

        // 取得画面を表示する
        // Show the pickup screen
        pickupPanel.SetActive(true);

        return true;
    }

    private void ClosePickup()
    {
        // 取得画面を閉じた状態にする
        // Mark the pickup screen as closed
        isOpen = false;

        // 取得画面を非表示にする
        // Hide the pickup screen
        pickupPanel.SetActive(false);

        // アイテム側の取得完了処理を実行する
        // Invoke the item's completion action
        onClosed?.Invoke();

        onClosed = null;

        // 閉じるクリックがワールドへ貫通しないよう次フレームで操作を戻す
        // Restore controls next frame to prevent click-through
        StartCoroutine(
            RestoreGameplayNextFrame()
        );
    }

    private IEnumerator RestoreGameplayNextFrame()
    {
        // 次のフレームまで待つ
        // Wait until the next frame
        yield return null;

        // PlayerControllerを以前の状態へ戻す
        // Restore the previous PlayerController state
        if (playerController != null)
        {
            playerController.enabled =
                previousPlayerControllerState;
        }

        // Interactionを以前の状態へ戻す
        // Restore the previous interaction state
        if (playerInteraction != null)
        {
            playerInteraction.enabled =
                previousPlayerInteractionState;
        }
    }
}