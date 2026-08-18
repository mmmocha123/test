using TMPro;
using UnityEngine;

/// <summary>
/// Builds the shared first-person support systems used by the other gameplay scenes.
/// HomeInterior deliberately does not install ApartmentLoop's story/enemy bootstrap.
/// </summary>
public sealed class HomeInteriorRuntimeSetup : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] private AudioClip footstepClip;
    [SerializeField] private AudioClip ambientClip;
    [SerializeField, Range(0f, 1f)] private float footstepVolume = 0.572f;
    [SerializeField, Range(0f, 1f)] private float ambientVolume = 0.152f;

    [Header("Flashlight")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private float flashlightIntensity = 15f;
    [SerializeField] private float flashlightRange = 20f;

    [Header("Pause Menu Visuals")]
    [SerializeField] private TMP_FontAsset settingsFont;
    [SerializeField] private Texture moveGuideTexture;
    [SerializeField] private Texture interactGuideTexture;

    private void Awake()
    {
        FirstPersonController playerController = GetComponent<FirstPersonController>();
        PlayerInteraction interactionController = GetComponent<PlayerInteraction>();
        FlashlightController flashlight = CreateFlashlight();

        ApartmentLoopFootstepAudio footsteps =
            gameObject.AddComponent<ApartmentLoopFootstepAudio>();
        footsteps.Configure(playerController, footstepClip, footstepVolume);

        AudioSource ambientSource = gameObject.AddComponent<AudioSource>();
        ambientSource.clip = ambientClip;
        ambientSource.loop = true;
        ambientSource.playOnAwake = true;
        ambientSource.spatialBlend = 0f;
        ambientSource.volume = ambientVolume;
        if (ambientClip != null) ambientSource.Play();

        ApartmentLoopPauseMenuController pauseController =
            gameObject.AddComponent<ApartmentLoopPauseMenuController>();
        pauseController.Configure(
            playerController,
            interactionController,
            flashlight,
            settingsFont,
            moveGuideTexture,
            interactGuideTexture);
    }

    private FlashlightController CreateFlashlight()
    {
        Transform cameraTransform = playerCamera != null
            ? playerCamera.transform
            : Camera.main != null ? Camera.main.transform : null;

        if (cameraTransform == null) return null;

        GameObject flashlightObject = new GameObject("Flashlight");
        flashlightObject.transform.SetParent(cameraTransform, false);
        flashlightObject.transform.localPosition = new Vector3(0.12f, -0.08f, 0.3f);

        Light flashlightLight = flashlightObject.AddComponent<Light>();
        flashlightLight.type = LightType.Spot;
        flashlightLight.intensity = flashlightIntensity;
        flashlightLight.range = flashlightRange;
        flashlightLight.spotAngle = 75f;
        flashlightLight.innerSpotAngle = 35f;
        flashlightLight.shadows = LightShadows.Soft;

        FlashlightController controller =
            flashlightObject.AddComponent<FlashlightController>();
        controller.Configure(flashlightLight, true);
        return controller;
    }
}
