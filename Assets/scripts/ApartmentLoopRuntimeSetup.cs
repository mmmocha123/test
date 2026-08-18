using UnityEngine;
using TMPro;

public sealed class ApartmentLoopRuntimeSetup : MonoBehaviour
{
    public bool EnemyDebugMarkerVisible => enemyDebugVisible;
    public Color EnemyDebugMarkerColor => enemyDebugColor;
    public float EnemyDebugMarkerScale => enemyDebugMarkerScale;
    public bool EnemyDebugShowLogicalFloor => enemyDebugShowLogicalFloor;
    public bool EnemyDebugClampToScreen => enemyDebugClampToScreen;
    public float EnemyDebugScreenMargin => enemyDebugScreenMargin;

    [Header("Audio")]
    [SerializeField] private AudioClip footstepClip;
    [SerializeField] private AudioClip ambientClip;
    [SerializeField, Range(0f, 1f)] private float footstepVolume = 0.7f;
    [SerializeField, Range(0f, 1f)] private float ambientVolume = 0.35f;

    [Header("Enemy Footstep Audio")]
    [SerializeField] private AudioClip enemyFootstepClip;
    [SerializeField, Range(0f, 1f)] private float enemyFootstepVolume = 0.85f;
    [SerializeField, Min(0.01f)] private float enemyFootstepMinDistance = 1f;
    [SerializeField, Min(0.1f)] private float enemyFootstepMaxDistance = 14f;

    [Header("Flashlight")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private float flashlightIntensity = 15f;
    [SerializeField] private float flashlightRange = 20f;
    [SerializeField, Min(0.03f)] private float chaseBlinkInterval = 0.12f;

    [Header("Enemy Debug Screen Marker")]
    [SerializeField] private bool enemyDebugVisible = false;
    [SerializeField] private Color enemyDebugColor = Color.magenta;
    [SerializeField, Range(0.5f, 3f)] private float enemyDebugMarkerScale = 1f;
    [SerializeField] private bool enemyDebugShowLogicalFloor = true;
    [SerializeField] private bool enemyDebugClampToScreen = true;
    [SerializeField, Min(0f)] private float enemyDebugScreenMargin = 65f;

    [Header("Pause Menu Visuals")]
    [SerializeField] private TMP_FontAsset settingsFont;
    [SerializeField] private Texture moveGuideTexture;
    [SerializeField] private Texture interactGuideTexture;

    [Header("ApartmentLoop Story")]
    [SerializeField] private AudioClip doorOpenClip;

    private void Awake()
    {
        FirstPersonController playerController =
            GetComponent<FirstPersonController>();
        PlayerInteraction interactionController =
            GetComponent<PlayerInteraction>();

        FlashlightController flashlight = CreateFlashlight();
        CreateFootsteps(playerController);
        CreateAmbientAudio();

        ApartmentLoopPauseMenuController pauseController =
            gameObject.AddComponent<ApartmentLoopPauseMenuController>();
        pauseController.Configure(
            playerController,
            interactionController,
            flashlight,
            settingsFont,
            moveGuideTexture,
            interactGuideTexture);

        ApartmentLoopBootstrap bootstrap = gameObject.AddComponent<ApartmentLoopBootstrap>();
        bootstrap.Configure(
            playerController,
            interactionController,
            flashlight,
            pauseController,
            settingsFont,
            enemyFootstepClip,
            enemyFootstepVolume,
            enemyFootstepMinDistance,
            enemyFootstepMaxDistance,
            enemyDebugVisible,
            enemyDebugColor,
            enemyDebugMarkerScale,
            enemyDebugShowLogicalFloor,
            enemyDebugClampToScreen,
            enemyDebugScreenMargin,
            chaseBlinkInterval,
            doorOpenClip);
    }

    private FlashlightController CreateFlashlight()
    {
        Transform cameraTransform = playerCamera != null
            ? playerCamera.transform
            : Camera.main != null ? Camera.main.transform : null;

        if (cameraTransform == null)
        {
            return null;
        }

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

    private void CreateFootsteps(FirstPersonController playerController)
    {
        ApartmentLoopFootstepAudio footsteps =
            gameObject.AddComponent<ApartmentLoopFootstepAudio>();
        footsteps.Configure(
            playerController,
            footstepClip,
            footstepVolume);
    }

    private void CreateAmbientAudio()
    {
        AudioSource ambientSource = gameObject.AddComponent<AudioSource>();
        ambientSource.clip = ambientClip;
        ambientSource.loop = true;
        ambientSource.playOnAwake = true;
        ambientSource.spatialBlend = 0f;
        ambientSource.volume = ambientVolume;

        if (ambientClip != null)
        {
            ambientSource.Play();
        }
    }
}
