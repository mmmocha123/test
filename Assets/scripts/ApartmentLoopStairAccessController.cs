using UnityEngine;

public enum StairAccessMode { None, UpOnly, DownOnly, Both }
public sealed class ApartmentLoopStairAccessController : MonoBehaviour
{
    private FloorLoopManager floorLoop; private ApartmentLoopDialogueManager dialogue; private float messageCooldown; private StairAccessMode mode;
    public StairAccessMode Mode
    {
        get => mode;
        set
        {
            mode = value;
            if (floorLoop != null)
            {
                floorLoop.SetMovementPermissions(
                    mode is StairAccessMode.UpOnly or StairAccessMode.Both,
                    mode is StairAccessMode.DownOnly or StairAccessMode.Both);
            }
        }
    }
    public bool SilentBlock { get; set; }
    public void Configure(FirstPersonController p, FloorLoopManager f, ApartmentLoopDialogueManager d)
    {
        floorLoop = f;
        dialogue = d;
        floorLoop.BlockedDirectionAttempt += OnBlockedDirectionAttempt;
        Mode = mode;
    }

    private void OnBlockedDirectionAttempt(FloorMoveDirection direction)
    {
        if (SilentBlock || Time.unscaledTime < messageCooldown) return;
        messageCooldown = Time.unscaledTime + 1.5f;
        dialogue.BeginDialogue(ApartmentLoopDialogueLines.WrongWay);
    }
}
