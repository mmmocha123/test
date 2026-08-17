using UnityEngine;
using UnityEngine.InputSystem;

public class FlashlightController : MonoBehaviour
{
    [Header("Flashlight")]
    [SerializeField] private Light flashlightLight;

    [Header("Unlock")]
    [SerializeField] private bool flashlightUnlocked = false;

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
        // 懐中電灯をまだ取得していない場合は操作を受け付けない
        // Do not accept flashlight input until it has been unlocked
        if (!flashlightUnlocked)
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
}
