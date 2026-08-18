using System.Collections.Generic;
using UnityEngine;

public enum ApartmentLoopLockReason { Dialogue, Cinematic, Pause, GameOver, SceneTransition, Reset }

public sealed class ApartmentLoopControlLockManager : MonoBehaviour
{
    private readonly HashSet<ApartmentLoopLockReason> locks = new();
    private FirstPersonController player;
    private PlayerInteraction interaction;
    private FlashlightController flashlight;
    public bool IsPaused => locks.Contains(ApartmentLoopLockReason.Pause);
    public bool HasBlockingLock => locks.Count > 0;
    public bool CanPause => locks.Count == 0;

    public void Configure(FirstPersonController p, PlayerInteraction i, FlashlightController f) { player = p; interaction = i; flashlight = f; Apply(); }
    public void Acquire(ApartmentLoopLockReason reason) { locks.Add(reason); Apply(); }
    public void Release(ApartmentLoopLockReason reason) { locks.Remove(reason); Apply(); }
    public void ClearAll() { locks.Clear(); Apply(); }

    private void Apply()
    {
        bool blocked = locks.Count > 0;
        if (player != null) { player.SetMovementEnabled(!blocked); player.SetLookEnabled(!blocked); }
        if (interaction != null) interaction.SetInteractionEnabled(!blocked);
        if (flashlight != null) flashlight.SetInputEnabled(!blocked);
        Time.timeScale = blocked ? 0f : 1f;
        bool cursor = locks.Contains(ApartmentLoopLockReason.Pause) || locks.Contains(ApartmentLoopLockReason.GameOver);
        Cursor.lockState = cursor ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = cursor;
    }

    private void OnDestroy() { Time.timeScale = 1f; }
}
