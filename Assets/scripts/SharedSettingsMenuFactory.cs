using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class SharedSettingsMenuView
{
    public GameObject CanvasObject { get; set; }
    public GameObject PauseMenu { get; set; }
    public Slider MouseSlider { get; set; }
    public Slider BrightnessSlider { get; set; }
    public Slider VolumeSlider { get; set; }
}

public static class SharedSettingsMenuFactory
{
    public static SharedSettingsMenuView Create(TMP_FontAsset font, Texture moveGuide, Texture interactGuide)
    {
        if (font == null)
        {
            font = Resources.Load<TMP_FontAsset>(
                "SettingsUI/BIZUDMincho-Regular SDF");
        }

        if (moveGuide == null)
        {
            moveGuide = Resources.Load<Texture2D>("SettingsUI/MoveGuide");
        }

        if (interactGuide == null)
        {
            interactGuide = Resources.Load<Texture2D>("SettingsUI/InteractGuide");
        }

        GameObject canvasObject = new GameObject("SharedPauseCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GameObject pauseMenu = CreateUiObject("PauseMenu", canvasObject.transform);
        StretchToParent(pauseMenu.GetComponent<RectTransform>());

        GameObject screenDark = CreateUiObject("ScreenDark", pauseMenu.transform);
        StretchToParent(screenDark.GetComponent<RectTransform>());
        Image background = screenDark.AddComponent<Image>();
        background.color = new Color(0.103773594f, 0.103773594f, 0.103773594f, 0.7607843f);

        GameObject settingsPanel = CreateUiObject("SettingsPanel", pauseMenu.transform);
        RectTransform settingsRect = settingsPanel.GetComponent<RectTransform>();
        settingsRect.anchorMin = new Vector2(0f, 0.5f);
        settingsRect.anchorMax = new Vector2(0f, 0.5f);
        settingsRect.anchoredPosition = new Vector2(350f, -40f);
        settingsRect.sizeDelta = new Vector2(500f, 760f);

        CreateLabel(settingsPanel.transform, "設定", new Vector2(-160f, 480f), new Vector2(200f, 50f), 60f, font);
        Slider brightness = CreateSettingSlider(settingsPanel.transform, "明るさ", 260f, 0f, 1f, font);
        Slider mouse = CreateSettingSlider(settingsPanel.transform, "マウス感度", 80f, 0.25f, 2f, font);
        Slider volume = CreateSettingSlider(settingsPanel.transform, "音量", -100f, 0f, 1f, font);

        CreateGuideImage(settingsPanel.transform, "MoveImage", moveGuide, new Vector2(800f, -10f), new Vector2(250f, 200f));
        CreateGuideImage(settingsPanel.transform, "InteractImage", interactGuide, new Vector2(1100f, 0f), new Vector2(200f, 250f));
        pauseMenu.SetActive(false);

        return new SharedSettingsMenuView
        {
            CanvasObject = canvasObject,
            PauseMenu = pauseMenu,
            MouseSlider = mouse,
            BrightnessSlider = brightness,
            VolumeSlider = volume
        };
    }

    private static Slider CreateSettingSlider(Transform parent, string label, float y, float minimum, float maximum, TMP_FontAsset font)
    {
        GameObject group = CreateUiObject(label + "Setting", parent);
        RectTransform groupRect = group.GetComponent<RectTransform>();
        groupRect.anchorMin = groupRect.anchorMax = new Vector2(0.5f, 0.5f);
        groupRect.anchoredPosition = new Vector2(50f, y);
        groupRect.sizeDelta = new Vector2(460f, 130f);

        CreateLabel(group.transform, label, new Vector2(150f, 30f), new Vector2(300f, 50f), 36f, font);

        // Unity 6 no longer ships the legacy UI/Skin/*.psd built-in resources.
        // DefaultControls still builds a fully functional slider with null
        // sprites; its Image components render using Unity's white texture.
        DefaultControls.Resources resources = new DefaultControls.Resources();

        // Use Unity UI's own default Slider generator so the hierarchy,
        // sprites, fill area, handle slide area and transitions are identical
        // to GameObject > UI > Slider.
        GameObject sliderObject = DefaultControls.CreateSlider(resources);
        sliderObject.name = label + "Slider";
        sliderObject.transform.SetParent(group.transform, false);
        RectTransform sliderRect = sliderObject.GetComponent<RectTransform>();
        sliderRect.anchorMin = sliderRect.anchorMax = new Vector2(0.5f, 0.5f);
        sliderRect.anchoredPosition = new Vector2(160f, -30f);
        sliderRect.sizeDelta = new Vector2(320f, 20f);

        Slider slider = sliderObject.GetComponent<Slider>();
        slider.minValue = minimum;
        slider.maxValue = maximum;
        return slider;
    }

    private static void CreateLabel(Transform parent, string value, Vector2 position, Vector2 size, float fontSize, TMP_FontAsset font)
    {
        GameObject labelObject = CreateUiObject(value + "Label", parent);
        RectTransform rect = labelObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        TextMeshProUGUI text = labelObject.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.font = font;
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
    }

    private static void CreateGuideImage(Transform parent, string name, Texture texture, Vector2 position, Vector2 size)
    {
        GameObject imageObject = CreateUiObject(name, parent);
        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        RawImage image = imageObject.AddComponent<RawImage>();
        image.texture = texture;
    }

    private static GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject uiObject = new GameObject(name, typeof(RectTransform));
        uiObject.layer = 5;
        uiObject.transform.SetParent(parent, false);
        return uiObject;
    }

    private static void StretchToParent(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
