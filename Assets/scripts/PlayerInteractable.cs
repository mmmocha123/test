public interface IPlayerInteractable
{
    bool CanInteract(FirstPersonController player);
    void Interact(FirstPersonController player);
}
