using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

public sealed class ApartmentLoopBootstrap : MonoBehaviour
{
    private FirstPersonController player; private PlayerInteraction interaction; private FlashlightController flashlight; private ApartmentLoopPauseMenuController pause; private TMP_FontAsset font; private AudioClip enemyFootsteps; private float enemyFootstepVolume; private float enemyFootstepMinDistance; private float enemyFootstepMaxDistance; private bool enemyDebugVisible; private Color enemyDebugColor; private float enemyDebugMarkerScale; private bool enemyDebugShowLogicalFloor; private bool enemyDebugClampToScreen; private float enemyDebugScreenMargin; private float chaseBlinkInterval; private AudioClip doorClip;
    public void Configure(FirstPersonController p, PlayerInteraction i, FlashlightController f, ApartmentLoopPauseMenuController pm, TMP_FontAsset uiFont, AudioClip enemyClip, float enemyVolume, float enemyMinDistance, float enemyMaxDistance, bool debugVisible, Color debugColor, float debugMarkerScale, bool debugShowLogicalFloor, bool debugClampToScreen, float debugScreenMargin, float blinkInterval, AudioClip doorOpenClip) { player=p;interaction=i;flashlight=f;pause=pm;font=uiFont;enemyFootsteps=enemyClip;enemyFootstepVolume=enemyVolume;enemyFootstepMinDistance=enemyMinDistance;enemyFootstepMaxDistance=enemyMaxDistance;enemyDebugVisible=debugVisible;enemyDebugColor=debugColor;enemyDebugMarkerScale=debugMarkerScale;enemyDebugShowLogicalFloor=debugShowLogicalFloor;enemyDebugClampToScreen=debugClampToScreen;enemyDebugScreenMargin=debugScreenMargin;chaseBlinkInterval=blinkInterval;doorClip=doorOpenClip;StartCoroutine(Build()); }
    private IEnumerator Build()
    {
        yield return null;
        FloorLoopManager floorLoop=FindFirstObjectByType<FloorLoopManager>(); if(floorLoop==null){Debug.LogError("ApartmentLoop requires FloorLoopManager.");yield break;}
        if(font==null)font=Resources.Load<TMP_FontAsset>("SettingsUI/BIZUDMincho-Regular SDF");
        GameObject managers=new("ApartmentLoopManagers");
        var control=managers.AddComponent<ApartmentLoopControlLockManager>();control.Configure(player,interaction,flashlight);pause.SetControlLock(control);
        var dialogue=managers.AddComponent<ApartmentLoopDialogueManager>();dialogue.Configure(control,font);
        var lighting=managers.AddComponent<ApartmentLoopLightingController>();lighting.Configure(floorLoop);
        var cameraDirector=managers.AddComponent<ApartmentLoopCameraDirector>();cameraDirector.Configure(player.transform,Camera.main.transform);
        var face=managers.AddComponent<FacePeekEventController>();face.Configure(control,cameraDirector);
        var graph=managers.AddComponent<EnemyWaypointGraph>();graph.Configure(floorLoop);
        var runtimeNavMesh=managers.AddComponent<ApartmentLoopNavMeshRuntime>();runtimeNavMesh.Configure(floorLoop);
        var stairs=managers.AddComponent<ApartmentLoopStairAccessController>();stairs.Configure(player,floorLoop,dialogue);
        var transition=CreateTransition(managers.transform);
        GameObject enemyObject=new("InvisibleEnemy");var enemy=enemyObject.AddComponent<InvisibleEnemyController>();enemy.Configure(player,graph,runtimeNavMesh,enemyFootsteps,enemyFootstepVolume,enemyFootstepMinDistance,enemyFootstepMaxDistance);enemy.StateChanged += OnEnemyStateChanged;
        var debugMarker=managers.AddComponent<EnemyDebugScreenMarker>();debugMarker.Configure(enemy,runtimeNavMesh,font,enemyDebugColor,enemyDebugVisible,enemyDebugMarkerScale,enemyDebugShowLogicalFloor,enemyDebugClampToScreen,enemyDebugScreenMargin);
        var contact=enemyObject.AddComponent<EnemyContactGameOver>();
        var progress=managers.AddComponent<ApartmentLoopProgressManager>();
        var gameOver=managers.AddComponent<ApartmentLoopGameOverManager>();gameOver.Configure(control,font,progress.ContinueFromCheckpoint);contact.Configure(enemy,player,gameOver);
        progress.Configure(floorLoop,player,interaction,flashlight,control,dialogue,lighting,cameraDirector,face,graph,enemy,gameOver,stairs,transition);
        yield return progress.BeginAfterSceneReady();
    }
    private SceneFadeTransition CreateTransition(Transform parent)
    {
        Canvas canvas=RuntimeUi.CreateCanvas("ApartmentLoopFadeCanvas",500);GameObject imageObject=RuntimeUi.CreatePanel(canvas.transform,"TransitionFade",Color.black,Vector2.zero,Vector2.one);CanvasGroup group=imageObject.AddComponent<CanvasGroup>();Image image=imageObject.GetComponent<Image>();image.raycastTarget=true;AudioSource source=imageObject.AddComponent<AudioSource>();source.playOnAwake=false;SceneFadeTransition transition=imageObject.AddComponent<SceneFadeTransition>();transition.Configure(group,source,doorClip);return transition;
    }

    private void OnEnemyStateChanged(InvisibleEnemyState state)
    {
        if (state == InvisibleEnemyState.Chase)
        {
            flashlight.BeginWarningRed();
        }
        else
        {
            flashlight.EndWarningRed();
        }
    }

    private void CreateEnemyDebugVisual(Transform parent)
    {
        GameObject marker = new("EnemyDebugMapPin");
        marker.transform.SetParent(parent, false);
        Material material = CreateDebugOverlayMaterial();

        GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = "PinHead";
        head.transform.SetParent(marker.transform, false);
        head.transform.localPosition = Vector3.up * 2.15f;
        head.transform.localScale = Vector3.one * .9f;
        Destroy(head.GetComponent<Collider>());
        head.GetComponent<Renderer>().sharedMaterial = material;

        GameObject tip = new("PinTip");
        tip.transform.SetParent(marker.transform, false);
        tip.transform.localPosition = Vector3.up * .45f;
        MeshFilter filter = tip.AddComponent<MeshFilter>();
        filter.sharedMesh = CreatePinMesh();
        tip.AddComponent<MeshRenderer>().sharedMaterial = material;
        marker.SetActive(enemyDebugVisible);
    }

    private Material CreateDebugOverlayMaterial()
    {
        Material material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        material.SetColor("_BaseColor", enemyDebugColor);
        material.color = enemyDebugColor;
        material.SetInt("_ZTest", (int)CompareFunction.Always);
        material.SetInt("_ZWrite", 0);
        material.renderQueue = 5000;
        return material;
    }

    private static Mesh CreatePinMesh()
    {
        const int segments = 20;
        Vector3[] vertices = new Vector3[segments + 2];
        int[] triangles = new int[segments * 6];
        vertices[0] = Vector3.zero;
        vertices[1] = Vector3.up * 1.35f;
        for (int i = 0; i < segments; i++)
        {
            float angle = Mathf.PI * 2f * i / segments;
            vertices[i + 2] = new Vector3(
                Mathf.Cos(angle) * .42f, 1.35f,
                Mathf.Sin(angle) * .42f);
            int next = (i + 1) % segments + 2;
            int index = i * 6;
            triangles[index] = 0;
            triangles[index + 1] = i + 2;
            triangles[index + 2] = next;
            triangles[index + 3] = 1;
            triangles[index + 4] = next;
            triangles[index + 5] = i + 2;
        }
        Mesh mesh = new() { name = "EnemyDebugPinMesh" };
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
