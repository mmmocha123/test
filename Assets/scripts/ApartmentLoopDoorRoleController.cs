using System;
using UnityEngine;

public enum ApartmentLoopDoorRole
{
    UnavailableHideDoor,
    HomeDoorLocked,
    ReturnedHomeDoor,
    HideDoor,
    FinalHomeDoor
}

public sealed class ApartmentLoopDoorRoleController : MonoBehaviour
{
    public ApartmentLoopDoorRole Role { get; private set; } = ApartmentLoopDoorRole.HideDoor;
    public bool HandlesStoryInteraction =>
        Role is ApartmentLoopDoorRole.HomeDoorLocked or
            ApartmentLoopDoorRole.ReturnedHomeDoor or
            ApartmentLoopDoorRole.FinalHomeDoor;
    public event Action<ApartmentLoopDoorRoleController> StoryInteracted;

    private Renderer[] highlightRenderers;
    private MaterialPropertyBlock propertyBlock;
    private GameObject flowerPot;

    public void ConfigureRuntime()
    {
        Transform lockerRoot = transform;
        while (lockerRoot.parent != null && lockerRoot.name != "Locker")
        {
            lockerRoot = lockerRoot.parent;
        }

        highlightRenderers =
            lockerRoot.GetComponentsInChildren<Renderer>(true);
        propertyBlock = new MaterialPropertyBlock();

        flowerPot = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        flowerPot.name = "FlowerPot";
        flowerPot.transform.SetParent(lockerRoot, false);
        flowerPot.transform.localPosition =
            new Vector3(-0.65f, -1.55f, 0.4f);
        flowerPot.transform.localScale =
            new Vector3(0.25f, 0.25f, 0.25f);
        Destroy(flowerPot.GetComponent<Collider>());
        flowerPot.SetActive(false);
    }

    public void SetRole(ApartmentLoopDoorRole role)
    {
        Role = role;
        if (flowerPot != null)
        {
            flowerPot.SetActive(
                role == ApartmentLoopDoorRole.FinalHomeDoor);
        }
    }

    public void SetHighlight(bool enabled)
    {
        if (highlightRenderers == null) return;

        foreach (Renderer targetRenderer in highlightRenderers)
        {
            if (targetRenderer == null) continue;

            if (!enabled)
            {
                targetRenderer.SetPropertyBlock(null);
                continue;
            }

            targetRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(
                "_BaseColor",
                new Color(0.55f, 0.78f, 1f, 1f));
            propertyBlock.SetColor(
                "_EmissionColor",
                new Color(0.08f, 0.22f, 0.4f));
            targetRenderer.SetPropertyBlock(propertyBlock);
        }
    }

    public void Interact()
    {
        if (HandlesStoryInteraction)
        {
            StoryInteracted?.Invoke(this);
        }
    }
}
