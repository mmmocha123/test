using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class ApartmentLoopPauseMenuController : MonoBehaviour
{
    private FirstPersonController playerController;
    private PlayerInteraction interactionController;
    private FlashlightController flashlightController;
    private GameObject pauseMenu;
    private GameObject pauseCanvas;
    private Slider mouseSlider;
    private Slider volumeSlider;
    private Slider brightnessSlider;
    private bool isPaused;
    private ApartmentLoopControlLockManager controlLock;

    public void SetControlLock(ApartmentLoopControlLockManager value) => controlLock = value;

    public void Configure(FirstPersonController player, PlayerInteraction interaction, FlashlightController flashlight, TMP_FontAsset font, Texture moveGuide, Texture interactGuide)
    {
        playerController = player;
        interactionController = interaction;
        flashlightController = flashlight;

        SharedSettingsMenuView view = SharedSettingsMenuFactory.Create(font, moveGuide, interactGuide);
        pauseCanvas = view.CanvasObject;
        pauseMenu = view.PauseMenu;
        mouseSlider = view.MouseSlider;
        brightnessSlider = view.BrightnessSlider;
        volumeSlider = view.VolumeSlider;

        mouseSlider.onValueChanged.AddListener(SetMouseSensitivity);
        brightnessSlider.onValueChanged.AddListener(GameSettingsManager.SetBrightness);
        volumeSlider.onValueChanged.AddListener(GameSettingsManager.SetMasterVolume);
    }

    private void Update()
    {
        if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            return;
        }

        if (isPaused) ClosePauseMenu();
        else if (controlLock == null || controlLock.CanPause) OpenPauseMenu();
    }

    private void OpenPauseMenu()
    {
        isPaused = true;
        mouseSlider.SetValueWithoutNotify(GameSettingsManager.MouseSensitivity);
        brightnessSlider.SetValueWithoutNotify(GameSettingsManager.Brightness);
        volumeSlider.SetValueWithoutNotify(GameSettingsManager.MasterVolume);
        pauseMenu.SetActive(true);
        if (controlLock != null) controlLock.Acquire(ApartmentLoopLockReason.Pause);
        else { SetGameplayEnabled(false); Cursor.lockState = CursorLockMode.None; Cursor.visible = true; Time.timeScale = 0f; }
    }

    private void ClosePauseMenu()
    {
        isPaused = false;
        pauseMenu.SetActive(false);
        if (controlLock != null) controlLock.Release(ApartmentLoopLockReason.Pause);
        else { Time.timeScale = 1f; SetGameplayEnabled(true); Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
    }

    private void SetGameplayEnabled(bool enabledState)
    {
        if (playerController != null) playerController.enabled = enabledState;
        if (interactionController != null) interactionController.enabled = enabledState;
        if (flashlightController != null) flashlightController.enabled = enabledState;
    }

    private void SetMouseSensitivity(float value)
    {
        if (playerController != null) playerController.SetMouseSensitivityMultiplier(value);
    }

    private void OnDestroy()
    {
        Time.timeScale = 1f;
        if (pauseCanvas != null) Destroy(pauseCanvas);
    }
}
