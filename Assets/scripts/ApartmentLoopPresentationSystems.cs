using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class ApartmentLoopLightingController : MonoBehaviour
{
    [SerializeField] private float flickerDuration = 3f;
    private FloorLoopManager floors;
    public bool Blackout { get; private set; }
    public void Configure(FloorLoopManager manager) { floors = manager; }
    public IEnumerator PlayFlicker(FloorRuntimeData eventFloor, Action completed)
    {
        float startTime = Time.unscaledTime;
        var lights = floors.FloorsByHeight.SelectMany(f => f.CorridorLights).ToArray();
        while (Time.unscaledTime - startTime < flickerDuration) { foreach (Light l in lights) if (l != null) l.enabled = UnityEngine.Random.value > .5f; yield return new WaitForSecondsRealtime(UnityEngine.Random.Range(.05f,.18f)); }
        foreach (Light l in lights) if (l != null) l.enabled = false;
        Light left = eventFloor.LeftmostCorridorLight;
        if (left != null) left.enabled = true;
        completed?.Invoke();
    }
    public void SetBlackout(bool value) { Blackout = value; if (value && floors != null) foreach (Light l in floors.FloorsByHeight.SelectMany(f => f.CorridorLights)) if (l != null) l.enabled = false; }
    private void LateUpdate() { if (Blackout) SetBlackout(true); }
}

public sealed class ApartmentLoopCameraDirector : MonoBehaviour
{
    private Transform body; private Transform cameraTransform;
    public void Configure(Transform playerBody, Transform camera) { body = playerBody; cameraTransform = camera; }
    public IEnumerator LookAt(Transform target, float duration = .45f)
    {
        if (target == null || body == null || cameraTransform == null) yield break;
        Quaternion bodyStart = body.rotation; Quaternion cameraStart = cameraTransform.localRotation; Vector3 direction = target.position - cameraTransform.position;
        Quaternion bodyEnd = Quaternion.LookRotation(new Vector3(direction.x,0,direction.z)); float pitch = -Mathf.Asin(direction.normalized.y) * Mathf.Rad2Deg; Quaternion cameraEnd = Quaternion.Euler(pitch,0,0); float elapsed = 0;
        while (elapsed < duration) { elapsed += Time.unscaledDeltaTime; float t = Mathf.SmoothStep(0,1,elapsed/Mathf.Max(.01f,duration)); body.rotation = Quaternion.Slerp(bodyStart,bodyEnd,t); cameraTransform.localRotation = Quaternion.Slerp(cameraStart,cameraEnd,t); yield return null; }
    }
}

public sealed class FacePeekEventController : MonoBehaviour
{
    [SerializeField] private float displayDuration = 1f;
    private ApartmentLoopControlLockManager control; private ApartmentLoopCameraDirector cameraDirector;
    public void Configure(ApartmentLoopControlLockManager c, ApartmentLoopCameraDirector d) { control = c; cameraDirector = d; }
    public IEnumerator Play(Transform face, Action completed)
    {
        control.Acquire(ApartmentLoopLockReason.Cinematic); face.gameObject.SetActive(true); yield return cameraDirector.LookAt(face); yield return new WaitForSecondsRealtime(displayDuration); face.gameObject.SetActive(false); control.Release(ApartmentLoopLockReason.Cinematic); completed?.Invoke();
    }
}

public sealed class SceneFadeTransition : MonoBehaviour
{
    private CanvasGroup fade; private AudioSource audioSource; private AudioClip doorClip;
    public void Configure(CanvasGroup group, AudioSource source, AudioClip clip) { fade = group; audioSource = source; doorClip = clip; fade.alpha = 0; fade.blocksRaycasts = false; }
    public void Begin(string sceneName, ApartmentLoopControlLockManager control) { StartCoroutine(Run(sceneName, control)); }
    private IEnumerator Run(string sceneName, ApartmentLoopControlLockManager control)
    {
        control?.Acquire(ApartmentLoopLockReason.SceneTransition); fade.blocksRaycasts = true; if (doorClip != null) audioSource.PlayOneShot(doorClip); float e=0; while(e<.5f){e+=Time.unscaledDeltaTime;fade.alpha=Mathf.Clamp01(e/.5f);yield return null;} yield return new WaitForSecondsRealtime(Mathf.Max(2f,doorClip != null ? doorClip.length-.5f : 0)); Time.timeScale=1f; SceneManager.LoadScene(sceneName);
    }
}
