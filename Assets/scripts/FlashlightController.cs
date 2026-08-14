using UnityEngine;
using UnityEngine.InputSystem;

public class FlashlightController : MonoBehaviour
{
    [SerializeField] private Light flashlightLight;

    private void Update()
    {
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
        flashlightLight.enabled = !flashlightLight.enabled;
    }
}