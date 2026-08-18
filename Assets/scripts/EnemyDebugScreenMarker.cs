using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class EnemyDebugScreenMarker : MonoBehaviour
{
    private InvisibleEnemyController enemy;
    private ApartmentLoopNavMeshRuntime runtimeNavMesh;
    private RectTransform marker;
    private TMP_Text pin;
    private TMP_Text floorLabel;
    private Camera playerCamera;
    private bool visible;
    private bool showLogicalFloor;
    private bool clampToScreen;
    private float screenMargin;
    private ApartmentLoopRuntimeSetup inspectorSettings;

    public void Configure(InvisibleEnemyController target,
        ApartmentLoopNavMeshRuntime navMesh, TMP_FontAsset font,
        Color color, bool show, float markerScale,
        bool displayLogicalFloor, bool clampMarkerToScreen,
        float markerScreenMargin)
    {
        enemy = target;
        runtimeNavMesh = navMesh;
        inspectorSettings = FindFirstObjectByType<ApartmentLoopRuntimeSetup>();
        visible = show;
        showLogicalFloor = displayLogicalFloor;
        clampToScreen = clampMarkerToScreen;
        screenMargin = Mathf.Max(0f, markerScreenMargin);
        playerCamera = Camera.main;

        GameObject canvasObject = new("EnemyDebugMarkerCanvas");
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        GameObject markerObject = new("EnemyPositionPin");
        markerObject.transform.SetParent(canvasObject.transform, false);
        marker = markerObject.AddComponent<RectTransform>();
        marker.sizeDelta = new Vector2(180f, 110f);
        marker.localScale = Vector3.one * Mathf.Clamp(markerScale, .5f, 3f);

        pin = CreateText(marker, "Pin", font, color, 34f);
        pin.text = "●\n▼";
        pin.alignment = TextAlignmentOptions.Center;
        pin.lineSpacing = -55f;
        RectTransform pinRect = pin.rectTransform;
        pinRect.anchorMin = new Vector2(.5f, .45f);
        pinRect.anchorMax = new Vector2(.5f, .45f);
        pinRect.pivot = new Vector2(.5f, .5f);
        pinRect.sizeDelta = new Vector2(70f, 85f);
        pinRect.anchoredPosition = Vector2.zero;

        floorLabel = CreateText(marker, "FloorLabel", font, color, 20f);
        floorLabel.alignment = TextAlignmentOptions.Center;
        floorLabel.rectTransform.anchorMin = new Vector2(.5f, 0f);
        floorLabel.rectTransform.anchorMax = new Vector2(.5f, 0f);
        floorLabel.rectTransform.pivot = new Vector2(.5f, 1f);
        floorLabel.rectTransform.sizeDelta = new Vector2(180f, 32f);
        floorLabel.rectTransform.anchoredPosition = new Vector2(0f, -2f);
        markerObject.SetActive(false);
    }

    private static TMP_Text CreateText(Transform parent, string name,
        TMP_FontAsset font, Color color, float size)
    {
        GameObject textObject = new(name);
        textObject.transform.SetParent(parent, false);
        TMP_Text text = textObject.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontSize = size;
        text.color = color;
        text.raycastTarget = false;
        return text;
    }

    private void LateUpdate()
    {
        RefreshInspectorSettings();
        bool shouldShow = visible && enemy != null &&
            enemy.State is not (InvisibleEnemyState.Inactive or
                InvisibleEnemyState.SafeDisabled);
        if (marker.gameObject.activeSelf != shouldShow)
            marker.gameObject.SetActive(shouldShow);
        if (!shouldShow) return;
        if (playerCamera == null) playerCamera = Camera.main;
        if (playerCamera == null) return;

        Vector3 screen = playerCamera.WorldToScreenPoint(
            enemy.transform.position + Vector3.up * 2.1f);
        if (screen.z < 0f)
        {
            screen.x = Screen.width - screen.x;
            screen.y = Screen.height - screen.y;
        }
        if (clampToScreen)
        {
            screen.x = Mathf.Clamp(screen.x, screenMargin,
                Screen.width - screenMargin);
            screen.y = Mathf.Clamp(screen.y, screenMargin,
                Screen.height - screenMargin);
        }
        marker.position = screen;
        int logicalFloor = runtimeNavMesh.GetLogicalFloorAt(
            enemy.transform.position);
        floorLabel.gameObject.SetActive(showLogicalFloor);
        if (showLogicalFloor)
            floorLabel.text = $"ENEMY  Logical {logicalFloor}F";
    }

    private void RefreshInspectorSettings()
    {
        if (inspectorSettings == null) return;
        visible = inspectorSettings.EnemyDebugMarkerVisible;
        showLogicalFloor =
            inspectorSettings.EnemyDebugShowLogicalFloor;
        clampToScreen = inspectorSettings.EnemyDebugClampToScreen;
        screenMargin = Mathf.Max(0f,
            inspectorSettings.EnemyDebugScreenMargin);
        if (marker != null)
        {
            marker.localScale = Vector3.one * Mathf.Clamp(
                inspectorSettings.EnemyDebugMarkerScale, .5f, 3f);
        }
        Color color = inspectorSettings.EnemyDebugMarkerColor;
        if (pin != null) pin.color = color;
        if (floorLabel != null) floorLabel.color = color;
    }
}
