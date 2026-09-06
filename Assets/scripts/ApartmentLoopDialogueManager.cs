using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class ApartmentLoopDialogueManager : MonoBehaviour
{
    [SerializeField] private float charactersPerSecond = 20f;
    private ApartmentLoopControlLockManager control;
    private GameObject panel;
    private GameObject centeredPanel;
    private TMP_Text text;
    private TMP_Text centeredText;
    private TMP_Text activeText;
    private string[] messages;
    private int index;
    private bool typing;
    private int startFrame;
    private Coroutine routine;
    private Action completed;
    public bool IsActive =>
        (panel != null && panel.activeSelf) ||
        (centeredPanel != null && centeredPanel.activeSelf);

    public void Configure(ApartmentLoopControlLockManager lockManager, TMP_FontAsset font)
    {
        control = lockManager;
        Canvas canvas = RuntimeUi.CreateCanvas("ApartmentLoopDialogueCanvas", 200);
        panel = RuntimeUi.CreatePanel(canvas.transform, "DialoguePanel", new Color(0,0,0,.72f), new Vector2(.08f,.04f), new Vector2(.92f,.25f));
        text = RuntimeUi.CreateText(panel.transform, "DialogueText", font, 32, TextAlignmentOptions.MidlineLeft);
        Canvas centeredCanvas = RuntimeUi.CreateCanvas("CenteredDialogueCanvas", 210);
        centeredPanel = RuntimeUi.CreatePanel(centeredCanvas.transform,
            "CenteredDialoguePanel", Color.clear,
            new Vector2(.13f, .18f), new Vector2(.87f, .82f));
        centeredText = RuntimeUi.CreateText(centeredPanel.transform,
            "CenteredDialogueText", font, 38, TextAlignmentOptions.Center);
        panel.SetActive(false);
        centeredPanel.SetActive(false);
    }

    public bool BeginDialogue(string[] lines, Action onCompleted = null)
    {
        return Begin(lines, false, onCompleted);
    }

    public bool BeginCenteredDialogue(string[] lines, Action onCompleted = null)
    {
        return Begin(lines, true, onCompleted);
    }

    private bool Begin(string[] lines, bool centered, Action onCompleted)
    {
        if (IsActive || lines == null || lines.Length == 0) return false;
        messages = lines; index = 0; completed = onCompleted; startFrame = Time.frameCount;
        panel.SetActive(!centered);
        centeredPanel.SetActive(centered);
        activeText = centered ? centeredText : text;
        control.Acquire(ApartmentLoopLockReason.Dialogue); Show(); return true;
    }

    private void Update()
    {
        if (!IsActive || Time.frameCount <= startFrame || Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;
        if (typing) { CompleteTyping(); return; }
        if (++index < messages.Length) Show(); else End();
    }

    private void Show()
    {
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(Type(messages[index]));
    }
    private IEnumerator Type(string value)
    {
        typing = true; activeText.text = value; activeText.maxVisibleCharacters = 0; activeText.ForceMeshUpdate();
        float delay = 1f / Mathf.Max(1, charactersPerSecond);
        for (int i = 1; i <= activeText.textInfo.characterCount; i++) { activeText.maxVisibleCharacters = i; yield return new WaitForSecondsRealtime(delay); }
        typing = false; routine = null;
    }
    private void CompleteTyping()
    {
        if (routine != null) StopCoroutine(routine); routine = null; activeText.ForceMeshUpdate(); activeText.maxVisibleCharacters = activeText.textInfo.characterCount; typing = false;
    }
    private void End()
    {
        panel.SetActive(false); centeredPanel.SetActive(false); messages = null; activeText = null; control.Release(ApartmentLoopLockReason.Dialogue); Action callback = completed; completed = null; callback?.Invoke();
    }
}

public static class RuntimeUi
{
    public static Canvas CreateCanvas(string name, int order)
    {
        GameObject go = new(name); Canvas c = go.AddComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = order; go.AddComponent<CanvasScaler>(); go.AddComponent<GraphicRaycaster>(); return c;
    }
    public static GameObject CreatePanel(Transform parent, string name, Color color, Vector2 min, Vector2 max)
    {
        GameObject go = new(name); go.transform.SetParent(parent, false); Image image = go.AddComponent<Image>(); image.color = color; RectTransform r = image.rectTransform; r.anchorMin = min; r.anchorMax = max; r.offsetMin = r.offsetMax = Vector2.zero; return go;
    }
    public static TMP_Text CreateText(Transform parent, string name, TMP_FontAsset font, float size, TextAlignmentOptions align)
    {
        GameObject go = new(name); go.transform.SetParent(parent, false); TMP_Text t = go.AddComponent<TextMeshProUGUI>(); t.font = font; t.fontSize = size; t.color = Color.white; t.alignment = align; t.textWrappingMode = TextWrappingModes.Normal; RectTransform r = t.rectTransform; r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = new Vector2(30,15); r.offsetMax = new Vector2(-30,-15); return t;
    }
}
