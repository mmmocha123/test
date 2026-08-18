using UnityEngine;
using UnityEngine.InputSystem;

public class FlashlightController : MonoBehaviour
{
    [Header("Flashlight")]
    [SerializeField] private Light flashlightLight;

    [Header("Unlock")]
    [SerializeField] private bool flashlightUnlocked = false;

    private bool inputEnabled = true;
    private bool warningBlinkActive;
    private bool lightStateBeforeWarning;
    private float warningBlinkInterval = 0.12f;
    private float nextWarningBlinkTime;
    private bool warningRedActive;
    private bool lightStateBeforeRedWarning;
    private Color lightColorBeforeRedWarning;

    public bool IsLightOn => flashlightLight != null && flashlightLight.enabled;

    private void Start()
    {
        // ゲーム開始時は懐中電灯を消灯しておく
        // Keep the flashlight turned off when the game starts
        if (flashlightLight != null)
        {
            flashlightLight.enabled = false;
        }
    }

    private void Update()
    {
        if (warningBlinkActive)
        {
            if (flashlightLight != null &&
                Time.unscaledTime >= nextWarningBlinkTime)
            {
                flashlightLight.enabled = !flashlightLight.enabled;
                nextWarningBlinkTime =
                    Time.unscaledTime + warningBlinkInterval;
            }

            return;
        }
        if (warningRedActive) return;

        // 懐中電灯をまだ取得していない場合は操作を受け付けない
        // Do not accept flashlight input until it has been unlocked
        if (!flashlightUnlocked || !inputEnabled)
        {
            return;
        }

        // キーボードが接続されていない場合は処理しない
        // Do nothing if no keyboard is available
        if (Keyboard.current == null)
        {
            return;
        }

        // Fキーが押された瞬間だけ懐中電灯を切り替える
        // Toggle the flashlight only when the F key is pressed
        if (Keyboard.current.fKey.wasPressedThisFrame)
        {
            ToggleFlashlight();
        }
    }

    public void UnlockFlashlight()
    {
        // カバン取得後に懐中電灯を使用可能にする
        // Unlock flashlight usage after the school bag is collected
        flashlightUnlocked = true;
    }

    public void Configure(Light lightSource, bool unlocked)
    {
        flashlightLight = lightSource;
        flashlightUnlocked = unlocked;

        if (flashlightLight != null)
        {
            flashlightLight.enabled = false;
        }
    }

    private void ToggleFlashlight()
    {
        // Lightが設定されていない場合は処理しない
        // Do nothing if the Light reference is not assigned
        if (flashlightLight == null)
        {
            return;
        }

        // 現在の点灯状態を反転する
        // Invert the current flashlight state
        flashlightLight.enabled =
            !flashlightLight.enabled;
    }

    public void SetInputEnabled(bool enabled)
    {
        inputEnabled = enabled;
    }

    public void SetLightOn(bool enabled)
    {
        if (warningRedActive)
        {
            lightStateBeforeRedWarning = enabled;
            return;
        }
        if (warningBlinkActive)
        {
            lightStateBeforeWarning = enabled;
            return;
        }

        if (flashlightLight != null) flashlightLight.enabled = enabled;
    }

    public void BeginWarningBlink(float interval)
    {
        if (flashlightLight == null || warningBlinkActive) return;
        lightStateBeforeWarning = flashlightLight.enabled;
        warningBlinkInterval = Mathf.Max(0.03f, interval);
        warningBlinkActive = true;
        flashlightLight.enabled = true;
        nextWarningBlinkTime =
            Time.unscaledTime + warningBlinkInterval;
    }

    public void BeginWarningRed()
    {
        if (flashlightLight == null || warningRedActive) return;
        EndWarningBlink();
        warningRedActive = true;
        lightStateBeforeRedWarning = flashlightLight.enabled;
        lightColorBeforeRedWarning = flashlightLight.color;
        flashlightLight.enabled = true;
        flashlightLight.color = Color.red;
    }

    public void EndWarningRed()
    {
        if (!warningRedActive) return;
        warningRedActive = false;
        if (flashlightLight == null) return;
        flashlightLight.color = lightColorBeforeRedWarning;
        flashlightLight.enabled = lightStateBeforeRedWarning;
    }

    public void EndWarningBlink()
    {
        if (!warningBlinkActive) return;
        warningBlinkActive = false;

        if (flashlightLight != null)
        {
            flashlightLight.enabled = lightStateBeforeWarning;
        }
    }
}
