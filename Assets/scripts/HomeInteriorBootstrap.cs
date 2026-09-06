using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

public sealed class HomeInteriorBootstrap : MonoBehaviour
{
    private FirstPersonController player;
    private PlayerInteraction interaction;
    private FlashlightController flashlight;
    private ApartmentLoopPauseMenuController pause;
    private TMP_FontAsset font;
    private AudioClip doorClip;

    public void Configure(FirstPersonController playerController,
        PlayerInteraction interactionController, FlashlightController flashlightController,
        ApartmentLoopPauseMenuController pauseController, TMP_FontAsset dialogueFont,
        AudioClip entranceDoorClip)
    {
        player = playerController;
        interaction = interactionController;
        flashlight = flashlightController;
        pause = pauseController;
        font = dialogueFont;
        doorClip = entranceDoorClip;
        StartCoroutine(Build());
    }

    private IEnumerator Build()
    {
        yield return null;
        if (font == null) font = Resources.Load<TMP_FontAsset>("SettingsUI/BIZUDMincho-Regular SDF");
        Transform environment = GameObject.Find("Home Environment")?.transform;
        if (environment == null)
        {
            Debug.LogError("HomeInterior story requires Home Environment.");
            yield break;
        }

        GameObject managers = new("HomeInterior Managers");
        ApartmentLoopControlLockManager control = managers.AddComponent<ApartmentLoopControlLockManager>();
        control.Configure(player, interaction, flashlight);
        pause?.SetControlLock(control);
        ApartmentLoopDialogueManager dialogue = managers.AddComponent<ApartmentLoopDialogueManager>();
        dialogue.Configure(control, font);

        Canvas fadeCanvas = RuntimeUi.CreateCanvas("HomeInterior Fade Canvas", 500);
        GameObject fadeObject = RuntimeUi.CreatePanel(fadeCanvas.transform,
            "Transition Fade", Color.black, Vector2.zero, Vector2.one);
        CanvasGroup fadeGroup = fadeObject.AddComponent<CanvasGroup>();
        fadeObject.GetComponent<Image>().raycastTarget = true;
        AudioSource transitionAudio = fadeObject.AddComponent<AudioSource>();
        SceneFadeTransition transition = fadeObject.AddComponent<SceneFadeTransition>();
        transition.Configure(fadeGroup, transitionAudio, doorClip);

        HomeInteriorProgressManager progress = managers.AddComponent<HomeInteriorProgressManager>();
        ApartmentLoopGameOverManager gameOver = managers.AddComponent<ApartmentLoopGameOverManager>();
        gameOver.Configure(control, font, progress.ContinueAfterGameOver);

        NavMeshSurface surface = environment.GetComponent<NavMeshSurface>();
        if (surface == null) surface = environment.gameObject.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.All;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.BuildNavMesh();

        Transform entrance = GameObject.Find("Entrance Spawn")?.transform ?? player.transform;
        List<Transform> waypoints = CreateWaypoints(environment, entrance);
        HomeInteriorEnemyController enemy = CreateEnemy(entrance, gameOver, waypoints);
        GameObject key = CreatePlaceholderAtFurniture(environment, "HomeInterior Key", "table",
            new Vector3(.12f, .035f, .035f), new Color(.92f, .72f, .12f),
            new Vector3(.28f, 0f, 0f));
        HomeInteriorInteractable keyInteraction = key.AddComponent<HomeInteriorInteractable>();

        AudioSource storyAudio = managers.AddComponent<AudioSource>();
        storyAudio.playOnAwake = false;
        Transform closetExit = FindExact(environment, "ExitPoint");
        Transform checkpoint = CreatePoint(managers.transform, "Enemy Event Retry Point",
            closetExit != null ? closetExit.position : player.transform.position,
            closetExit != null ? closetExit.rotation : player.transform.rotation);
        progress.Configure(player, control, dialogue, enemy, transition, key,
            storyAudio, doorClip, checkpoint);
        enemy.Configure(player, gameOver, waypoints);
        keyInteraction.Configure(progress, HomeInteriorInteractableKind.Key);

        ConfigureEnvironmentInteractions(environment, progress);
        CreateDiaries(environment, progress);
        CreatePlayerRoomTrigger(environment, progress);
        StartCoroutine(progress.Begin());
    }

    private static void ConfigureEnvironmentInteractions(Transform environment,
        HomeInteriorProgressManager progress)
    {
        AddInteractable(FindExact(environment, "Trash_Bag (10)"), progress,
            HomeInteriorInteractableKind.Examine, "ゴミ、そろそろ捨てないと......");
        AddInteractable(FindExact(environment, "fridge"), progress,
            HomeInteriorInteractableKind.Examine, "期限切れのお茶と弁当が入っている");
        Transform entranceDoor = FindExact(environment, "front door") ??
            FindExact(environment, "door front");
        AddInteractable(entranceDoor, progress, HomeInteriorInteractableKind.Entrance);
    }

    private static void CreateDiaries(Transform environment,
        HomeInteriorProgressManager progress)
    {
        (string name, string furniture, HomeInteriorInteractableKind kind, string[] lines)[] specs =
        {
            ("Diary 01", "table", HomeInteriorInteractableKind.Diary1, new[]
            {
                "お母さんは今日もかえってこない\n\nお仕事がたいへんだから\nわたしががまんしないと\n\nいい子にしてたら\nわたしのことを見てくれるかな......"
            }),
            ("Diary 02", "desk.002", HomeInteriorInteractableKind.Diary2, new[]
            {
                "隣のマンションで男の子が落ちたらしい\n\nその子のお母さんは泣いていた\n顔もあげずに、ずっと叫んでいた"
            }),
            ("Diary 03", "bed", HomeInteriorInteractableKind.Diary3, new[]
            {
                "テスト100点だったのに\nお手伝いもできるのに\n\nいい子にしてても\nお母さんの目にわたしは映らない"
            })
        };
        foreach (var spec in specs)
        {
            GameObject diary = CreatePlaceholderAtFurniture(environment, spec.name,
                spec.furniture, new Vector3(.24f, .012f, .18f), new Color(.82f, .78f, .64f),
                spec.kind == HomeInteriorInteractableKind.Diary1
                    ? new Vector3(-.28f, 0f, 0f) : Vector3.zero);
            diary.AddComponent<HomeInteriorInteractable>().Configure(progress, spec.kind, spec.lines);
        }
    }

    private static void CreatePlayerRoomTrigger(Transform environment,
        HomeInteriorProgressManager progress)
    {
        Transform bed = FindExact(environment, "bed");
        GameObject trigger = new("Player Room Entrance Trigger");
        trigger.transform.SetParent(environment, true);
        trigger.transform.position = bed != null ? bed.position : environment.TransformPoint(new Vector3(-1.5f, .8f, -1.5f));
        BoxCollider collider = trigger.AddComponent<BoxCollider>();
        collider.isTrigger = true;
        collider.size = new Vector3(3f, 2.2f, 3f);
        trigger.AddComponent<HomeInteriorPlayerRoomTrigger>().Configure(progress);
    }

    private static HomeInteriorEnemyController CreateEnemy(Transform entrance,
        ApartmentLoopGameOverManager gameOver, IEnumerable<Transform> waypoints)
    {
        GameObject root = new("HomeInterior Enemy (Placeholder)");
        root.transform.position = entrance.position + entrance.forward * 1.2f;
        NavMeshAgent agent = root.AddComponent<NavMeshAgent>();
        CapsuleCollider contact = root.AddComponent<CapsuleCollider>();
        contact.isTrigger = true;
        contact.radius = .42f;
        contact.height = 1.7f;
        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        visual.name = "Visual Placeholder - Replace This Child";
        visual.transform.SetParent(root.transform, false);
        visual.transform.localPosition = Vector3.up * .85f;
        visual.transform.localScale = new Vector3(.65f, .85f, .65f);
        Object.Destroy(visual.GetComponent<Collider>());
        Material material = new(Shader.Find("Universal Render Pipeline/Lit"));
        material.SetColor("_BaseColor", new Color(.12f, .01f, .015f));
        visual.GetComponent<Renderer>().sharedMaterial = material;
        HomeInteriorEnemyController enemy = root.AddComponent<HomeInteriorEnemyController>();
        enemy.Configure(null, gameOver, waypoints);
        return enemy;
    }

    private static List<Transform> CreateWaypoints(Transform environment, Transform entrance)
    {
        GameObject root = new("HomeInterior Enemy Waypoints");
        string[] anchors = { "fridge", "table", "desk.002", "bed", "closet", "bathroom" };
        List<Transform> points = new() { CreatePoint(root.transform, "Entrance", entrance.position, entrance.rotation) };
        foreach (string anchorName in anchors)
        {
            Transform anchor = FindExact(environment, anchorName);
            if (anchor == null) continue;
            Vector3 candidate = anchor.position;
            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2.5f, NavMesh.AllAreas)) candidate = hit.position;
            points.Add(CreatePoint(root.transform, anchorName, candidate, Quaternion.identity));
        }
        return points;
    }

    private static Transform CreatePoint(Transform parent, string name,
        Vector3 position, Quaternion rotation)
    {
        GameObject point = new(name);
        point.transform.SetParent(parent, true);
        point.transform.SetPositionAndRotation(position, rotation);
        return point.transform;
    }

    private static GameObject CreatePlaceholderAtFurniture(Transform environment,
        string objectName, string furnitureName, Vector3 scale, Color color,
        Vector3 worldOffset)
    {
        Transform furniture = FindExact(environment, furnitureName);
        GameObject anchor = new(objectName + " Anchor");
        anchor.transform.SetParent(environment, true);
        Bounds bounds = GetBounds(furniture);
        anchor.transform.position = bounds.size.sqrMagnitude > 0f
            ? new Vector3(bounds.center.x, bounds.max.y + .025f, bounds.center.z)
            : environment.TransformPoint(Vector3.up);
        anchor.transform.position += worldOffset;
        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visual.name = "Visual Placeholder - Replace This Child";
        visual.transform.SetParent(anchor.transform, false);
        visual.transform.localScale = scale;
        Material material = new(Shader.Find("Universal Render Pipeline/Lit"));
        material.SetColor("_BaseColor", color);
        visual.GetComponent<Renderer>().sharedMaterial = material;
        return anchor;
    }

    private static void AddInteractable(Transform target,
        HomeInteriorProgressManager progress, HomeInteriorInteractableKind kind,
        params string[] lines)
    {
        if (target == null)
        {
            Debug.LogError($"HomeInterior interactable target was not found: {kind}");
            return;
        }
        HomeInteriorInteractable interactable = target.GetComponent<HomeInteriorInteractable>();
        if (interactable == null) interactable = target.gameObject.AddComponent<HomeInteriorInteractable>();
        interactable.Configure(progress, kind, lines);
        if (target.GetComponentsInChildren<Collider>(true).Length == 0)
        {
            Bounds bounds = GetBounds(target);
            BoxCollider box = target.gameObject.AddComponent<BoxCollider>();
            box.center = target.InverseTransformPoint(bounds.center);
            Vector3 scale = target.lossyScale;
            box.size = new Vector3(bounds.size.x / Mathf.Max(.001f, Mathf.Abs(scale.x)),
                bounds.size.y / Mathf.Max(.001f, Mathf.Abs(scale.y)),
                bounds.size.z / Mathf.Max(.001f, Mathf.Abs(scale.z)));
        }
    }

    private static Bounds GetBounds(Transform target)
    {
        if (target == null) return default;
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return new Bounds(target.position, Vector3.one * .3f);
        Bounds bounds = renderers[0].bounds;
        foreach (Renderer renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
        return bounds;
    }

    private static Transform FindExact(Transform root, string name)
    {
        return root.GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(child => child.name == name);
    }
}
