using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class PauseMenuController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject pauseMenu;
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private Slider brightnessSlider;

    [Header("Player")]
    [SerializeField] private IntroOutdoorPlayerController playerController;
    [SerializeField] private IntroOutdoorInteraction interactionController;

    [Header("Other UI")]
    [SerializeField] private IntroOutdoorPickupUI pickupUI;

    private bool isPaused = false;

    private void Awake()
    {
        if (volumeSlider != null)
        {
            volumeSlider.onValueChanged.AddListener(SetMasterVolume);
        }

        if (brightnessSlider != null)
        {
            brightnessSlider.onValueChanged.AddListener(SetBrightness);
        }
    }

    private void Start()
    {
        // 開始時はポーズ画面を閉じておく
        // Keep the pause menu hidden at startup
        if (pauseMenu != null)
        {
            pauseMenu.SetActive(false);
        }

        // 念のため時間を通常状態に戻す
        // Ensure normal time scale at startup
        Time.timeScale = 1f;
    }

    private void Update()
    {
        // キーボードが存在しないなら処理しない
        // Do nothing if no keyboard is available
        if (Keyboard.current == null)
        {
            return;
        }

        // Escが押された瞬間だけ処理する
        // Process only when Escape is pressed this frame
        if (!Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            return;
        }

        // アイテム取得UIが開いている間はポーズを開かない
        // Do not open the pause menu while the pickup UI is open
        if (pickupUI != null && pickupUI.IsOpen)
        {
            return;
        }

        // 現在の状態に応じて開閉を切り替える
        // Toggle open/close depending on the current pause state
        if (isPaused)
        {
            ClosePauseMenu();
        }
        else
        {
            OpenPauseMenu();
        }
    }

    public void OpenPauseMenu()
    {
        // すでに開いているなら何もしない
        // Do nothing if the pause menu is already open
        if (isPaused)
        {
            return;
        }

        isPaused = true;

        // Reflect the persisted values without invoking the sliders' callbacks.
        RefreshSettingsSliders();

        // ポーズUIを表示する
        // Show the pause UI
        if (pauseMenu != null)
        {
            pauseMenu.SetActive(true);
        }

        // プレイヤー移動を止める
        // Disable player movement
        if (playerController != null)
        {
            playerController.enabled = false;
        }

        // インタラクトを止める
        // Disable interaction control
        if (interactionController != null)
        {
            interactionController.enabled = false;
        }

        // マウスカーソルを表示してロック解除する
        // Show the mouse cursor and unlock it
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // ゲーム時間を停止する
        // Pause in-game time
        Time.timeScale = 0f;
    }

    public void ClosePauseMenu()
    {
        // すでに閉じているなら何もしない
        // Do nothing if the pause menu is already closed
        if (!isPaused)
        {
            return;
        }

        isPaused = false;

        // 時間を元に戻す
        // Restore normal time scale
        Time.timeScale = 1f;

        // ポーズUIを閉じる
        // Hide the pause UI
        if (pauseMenu != null)
        {
            pauseMenu.SetActive(false);
        }

        // プレイヤー移動を再開する
        // Re-enable player movement
        if (playerController != null)
        {
            playerController.enabled = true;
        }

        // インタラクトを再開する
        // Re-enable interaction control
        if (interactionController != null)
        {
            interactionController.enabled = true;
        }

        // マウスカーソルを隠して再ロックする
        // Hide the mouse cursor and lock it again
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void SetMasterVolume(float value)
    {
        GameSettingsManager.SetMasterVolume(value);
    }

    public void SetBrightness(float value)
    {
        GameSettingsManager.SetBrightness(value);
    }

    private void RefreshSettingsSliders()
    {
        if (volumeSlider != null)
        {
            volumeSlider.SetValueWithoutNotify(GameSettingsManager.MasterVolume);
        }

        if (brightnessSlider != null)
        {
            brightnessSlider.SetValueWithoutNotify(GameSettingsManager.Brightness);
        }
    }

    private void OnDestroy()
    {
        if (volumeSlider != null)
        {
            volumeSlider.onValueChanged.RemoveListener(SetMasterVolume);
        }

        if (brightnessSlider != null)
        {
            brightnessSlider.onValueChanged.RemoveListener(SetBrightness);
        }
    }
}
