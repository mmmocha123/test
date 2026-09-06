using UnityEngine;

public sealed class HomeInteriorLightSwitch : MonoBehaviour, IPlayerInteractable
{
    [SerializeField] private Light targetLight;

    public bool CanInteract => targetLight != null;

    public void Configure(Light lightSource)
    {
        targetLight = lightSource;
        if (targetLight != null) targetLight.enabled = false;
    }

    public void Interact()
    {
        if (targetLight != null)
            targetLight.enabled = !targetLight.enabled;
    }

    bool IPlayerInteractable.CanInteract(FirstPersonController player) => CanInteract;
    void IPlayerInteractable.Interact(FirstPersonController player) => Interact();
}
