using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class ApartmentLoopCheckpoint
{
    public Vector3 playerPosition;
    public Quaternion playerRotation;
    public float playerLookPitch;
    public FloorLoopSnapshot floors;
}

public sealed class ApartmentLoopGameOverManager : MonoBehaviour
{
    private ApartmentLoopControlLockManager control;
    private GameObject panel;
    private Action continueAction;
    private bool active;
    public bool IsActive => active;

    public void Configure(ApartmentLoopControlLockManager lockManager, TMP_FontAsset font, Action onContinue)
    {
        control = lockManager; continueAction = onContinue;
        Canvas canvas = RuntimeUi.CreateCanvas("ApartmentLoopGameOverCanvas", 400);
        panel = RuntimeUi.CreatePanel(canvas.transform, "GameOverPanel", new Color(0,0,0,.88f), Vector2.zero, Vector2.one);
        TMP_Text title = RuntimeUi.CreateText(panel.transform, "GameOver", font, 58, TextAlignmentOptions.Center); title.text = "GameOver"; title.rectTransform.anchorMin = new Vector2(.25f,.55f); title.rectTransform.anchorMax = new Vector2(.75f,.75f);
        CreateButton(panel.transform, "Continue", "続ける", font, new Vector2(.38f,.38f), new Vector2(.62f,.48f), Continue);
        CreateButton(panel.transform, "Title", "タイトルに戻る", font, new Vector2(.38f,.24f), new Vector2(.62f,.34f), ReturnTitle);
        panel.SetActive(false);
    }
    public void TriggerGameOver() { if (active) return; active = true; panel.SetActive(true); control.Acquire(ApartmentLoopLockReason.GameOver); }
    private void Continue() { active = false; panel.SetActive(false); control.Release(ApartmentLoopLockReason.GameOver); continueAction?.Invoke(); }
    private void ReturnTitle() { Time.timeScale = 1f; SceneManager.LoadScene("Title"); }
    private static void CreateButton(Transform parent, string name, string label, TMP_FontAsset font, Vector2 min, Vector2 max, Action clicked)
    {
        GameObject go = RuntimeUi.CreatePanel(parent,name,new Color(.18f,.18f,.18f,.95f),min,max); Button button = go.AddComponent<Button>(); button.onClick.AddListener(() => clicked()); TMP_Text text = RuntimeUi.CreateText(go.transform,"Label",font,28,TextAlignmentOptions.Center); text.text=label;
    }
}
