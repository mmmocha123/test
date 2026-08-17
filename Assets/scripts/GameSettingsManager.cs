using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public sealed class GameSettingsManager : MonoBehaviour
{
    private const string MasterVolumeKey = "Settings.MasterVolume";
    private const string BrightnessKey = "Settings.Brightness";
    private const string MouseSensitivityKey = "Settings.MouseSensitivity";
    private const float DefaultSetting = 0.5f;
    private const float MinimumExposure = -2f;
    private const float MaximumExposure = 2f;

    private static GameSettingsManager instance;
    private static float masterVolume = DefaultSetting;
    private static float brightness = DefaultSetting;
    private static float mouseSensitivity = 1f;

    private Volume settingsVolume;
    private VolumeProfile settingsProfile;
    private ColorAdjustments colorAdjustments;

    public static float MasterVolume
    {
        get
        {
            EnsureInstance();
            return masterVolume;
        }
    }

    public static float Brightness
    {
        get
        {
            EnsureInstance();
            return brightness;
        }
    }

    public static float MouseSensitivity
    {
        get
        {
            EnsureInstance();
            return mouseSensitivity;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    public static void SetMasterVolume(float value)
    {
        EnsureInstance();
        masterVolume = Mathf.Clamp01(value);
        AudioListener.volume = masterVolume;
        PlayerPrefs.SetFloat(MasterVolumeKey, masterVolume);
        PlayerPrefs.Save();
    }

    public static void SetBrightness(float value)
    {
        EnsureInstance();
        brightness = Mathf.Clamp01(value);
        instance.ApplyBrightness();
        PlayerPrefs.SetFloat(BrightnessKey, brightness);
        PlayerPrefs.Save();
    }

    public static void SetMouseSensitivity(float value)
    {
        EnsureInstance();
        mouseSensitivity = Mathf.Clamp(value, 0.25f, 2f);
        PlayerPrefs.SetFloat(MouseSensitivityKey, mouseSensitivity);
        PlayerPrefs.Save();
    }

    private static void EnsureInstance()
    {
        if (instance != null)
        {
            return;
        }

        GameObject managerObject = new GameObject(nameof(GameSettingsManager));
        instance = managerObject.AddComponent<GameSettingsManager>();
        DontDestroyOnLoad(managerObject);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        masterVolume = Mathf.Clamp01(
            PlayerPrefs.GetFloat(MasterVolumeKey, DefaultSetting));
        brightness = Mathf.Clamp01(
            PlayerPrefs.GetFloat(BrightnessKey, DefaultSetting));
        mouseSensitivity = Mathf.Clamp(
            PlayerPrefs.GetFloat(MouseSensitivityKey, 1f),
            0.25f,
            2f);

        CreateBrightnessVolume();
        AudioListener.volume = masterVolume;
        ApplyBrightness();

        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void CreateBrightnessVolume()
    {
        settingsProfile = ScriptableObject.CreateInstance<VolumeProfile>();
        colorAdjustments = settingsProfile.Add<ColorAdjustments>(true);
        colorAdjustments.postExposure.overrideState = true;

        settingsVolume = gameObject.AddComponent<Volume>();
        settingsVolume.isGlobal = true;
        settingsVolume.priority = 1000f;
        settingsVolume.sharedProfile = settingsProfile;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnablePostProcessingOnSceneCameras();
        AudioListener.volume = masterVolume;
        ApplyBrightness();
    }

    private static void EnablePostProcessingOnSceneCameras()
    {
        Camera[] cameras = FindObjectsByType<Camera>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (Camera sceneCamera in cameras)
        {
            UniversalAdditionalCameraData cameraData =
                sceneCamera.GetUniversalAdditionalCameraData();

            if (cameraData.renderType == CameraRenderType.Base)
            {
                cameraData.renderPostProcessing = true;
            }
        }
    }

    private void ApplyBrightness()
    {
        if (colorAdjustments == null)
        {
            return;
        }

        colorAdjustments.postExposure.value = Mathf.Lerp(
            MinimumExposure,
            MaximumExposure,
            brightness);
    }

    private void OnDestroy()
    {
        if (instance != this)
        {
            return;
        }

        SceneManager.sceneLoaded -= HandleSceneLoaded;

        if (settingsProfile != null)
        {
            Destroy(settingsProfile);
        }

        instance = null;
    }
}
