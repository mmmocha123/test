using TMPro;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class HomeInteriorSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/HomeInterior.unity";
    private const string BuildSessionKey = "HomeInteriorSceneBuilder.Completed.v1";

    static HomeInteriorSceneBuilder()
    {
        EditorApplication.delayCall += TryAutomaticBuild;
    }

    [MenuItem("Tools/Home Interior/Rebuild Scene")]
    public static void RebuildFromMenu()
    {
        Build(true);
    }

    private static void TryAutomaticBuild()
    {
        if (SessionState.GetBool(BuildSessionKey, false) ||
            EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.path != ScenePath || activeScene.rootCount != 0)
        {
            return;
        }

        Build(false);
        SessionState.SetBool(BuildSessionKey, true);
    }

    public static void BuildBatch()
    {
        Build(true);
    }

    private static void Build(bool openTargetScene)
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        GameObject buildingAsset =
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/HomeInterior/building.fbx");
        GameObject objectsAsset =
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/HomeInterior/objects.fbx");

        if (buildingAsset == null || objectsAsset == null)
        {
            Debug.LogError("HomeInterior: FBX assets have not finished importing.");
            return;
        }

        ConfigureTextureFiltering();

        Scene scene;
        if (openTargetScene)
        {
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
        else
        {
            scene = SceneManager.GetActiveScene();
        }

        GameObject environment = new GameObject("Home Environment");
        environment.transform.localScale = Vector3.one * 1.3f;
        GameObject building = (GameObject)PrefabUtility.InstantiatePrefab(buildingAsset, scene);
        building.name = "Building";
        building.transform.SetParent(environment.transform, false);
        ConvertMaterialsToUrp(building);
        AddMeshColliders(building, true);

        GameObject furnishings = (GameObject)PrefabUtility.InstantiatePrefab(objectsAsset, scene);
        furnishings.name = "Furnishings";
        furnishings.transform.SetParent(environment.transform, false);
        ConvertMaterialsToUrp(furnishings);
        AddMeshColliders(furnishings, false);

        PlayerInteraction playerInteraction = CreatePlayer();
        CreateClosetHideSpot(environment.transform, playerInteraction);
        CreateLighting();
        ConfigureLightSwitches(environment.transform);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log("HomeInterior: scene built with entrance spawn and shared player systems.");
    }

    private static void ConfigureTextureFiltering()
    {
        string[] textureGuids = AssetDatabase.FindAssets("t:Texture2D", new[]
        {
            "Assets/HomeInterior/textures"
        });

        foreach (string guid in textureGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null || importer.filterMode == FilterMode.Point) continue;
            importer.filterMode = FilterMode.Point;
            importer.SaveAndReimport();
        }
    }

    private static void ConvertMaterialsToUrp(GameObject root)
    {
        const string materialFolder = "Assets/HomeInterior/Materials";
        if (!AssetDatabase.IsValidFolder(materialFolder))
        {
            AssetDatabase.CreateFolder("Assets/HomeInterior", "Materials");
        }

        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null)
        {
            Debug.LogWarning("HomeInterior: URP/Lit shader was not found; keeping imported materials.");
            return;
        }

        Dictionary<Material, Material> converted = new Dictionary<Material, Material>();
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                Material source = materials[i];
                if (source == null) continue;

                if (!converted.TryGetValue(source, out Material target))
                {
                    string safeName = SanitizeFileName(source.name);
                    string path = materialFolder + "/" + safeName + ".mat";
                    target = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (target == null)
                    {
                        target = new Material(urpLit) { name = source.name };
                        AssetDatabase.CreateAsset(target, path);
                    }

                    target.shader = urpLit;
                    if (source.HasProperty("_Color") && target.HasProperty("_BaseColor"))
                        target.SetColor("_BaseColor", source.GetColor("_Color"));
                    Texture texture = FindTextureForMaterial(source.name);
                    if (texture == null) texture = source.mainTexture;
                    if (texture != null && target.HasProperty("_BaseMap"))
                        target.SetTexture("_BaseMap", texture);
                    EditorUtility.SetDirty(target);
                    converted.Add(source, target);
                }

                materials[i] = target;
            }
            renderer.sharedMaterials = materials;
        }
    }

    private static Texture FindTextureForMaterial(string materialName)
    {
        string key = materialName.Trim().ToLowerInvariant();
        Dictionary<string, string> aliases = new Dictionary<string, string>
        {
            { "walls outside", "wall outside v2" }, { "walls outside edge", "wall outside v2" },
            { "outside", "concrete outside" }, { "door front", "front door" },
            { "outside windows", "view1 fade" }, { "inside windows", "view1 fade" },
            { "windows outside", "view2 fade" }, { "wall closet", "wall2" },
            { "doors misc", "closet door" }, { "floor2", "bathroom" },
            { "floor3", "concrete squares" }, { "floor entrance", "floor outside tiling" },
            { "table and chair", "table-chair" }, { "shelves1", "shelves" },
            { "shelves2", "shelves" }, { "shelves3", "shelves" },
            { "clothes blue", "clothes" }, { "clothes blue 2", "clothes2" },
            { "clothes gray", "clothes2" }, { "clothes brown", "clothes2" },
            { "laptop screen", "TV screen" }, { "tv display", "TV screen" },
            { "kitchen shiny", "kitchen" }, { "kitchen-glass", "kitchen" },
            { "appliance light", "appliance" }, { "outside lit", "concrete outside" },
            { "sliding door hall glow", "sliding door hall" },
            { "view1", "view1" }, { "view2", "view2" }
        };
        if (aliases.TryGetValue(key, out string alias)) key = alias.ToLowerInvariant();

        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/HomeInterior/textures" });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(path).ToLowerInvariant() == key)
                return AssetDatabase.LoadAssetAtPath<Texture>(path);
        }
        return null;
    }

    private static string SanitizeFileName(string value)
    {
        foreach (char invalid in Path.GetInvalidFileNameChars())
            value = value.Replace(invalid, '_');
        return string.IsNullOrWhiteSpace(value) ? "HomeMaterial" : value;
    }

    private static void AddMeshColliders(GameObject root, bool skipBackdropMeshes)
    {
        foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null) continue;

            string lowerName = filter.name.ToLowerInvariant();
            if (skipBackdropMeshes &&
                (lowerName.Contains("skybox") ||
                 lowerName.Contains("view ") ||
                 lowerName.Contains("background") ||
                 lowerName.Contains("shadow cover") ||
                 lowerName.Contains("windows")))
            {
                continue;
            }

            if (filter.GetComponent<Collider>() == null)
            {
                MeshCollider collider = filter.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = filter.sharedMesh;
            }

            GameObjectUtility.SetStaticEditorFlags(
                filter.gameObject,
                StaticEditorFlags.BatchingStatic |
                StaticEditorFlags.OccluderStatic |
                StaticEditorFlags.OccludeeStatic |
                StaticEditorFlags.ReflectionProbeStatic);
        }
    }

    private static PlayerInteraction CreatePlayer()
    {
        // The front door is centred at x=-0.45 and the hall extends inward from it.
        // This point is just inside the genkan, facing into the home.
        Vector3 entrancePosition = new Vector3(-0.585f, 0.08f, -4.134f);

        GameObject spawn = new GameObject("Entrance Spawn");
        spawn.transform.SetPositionAndRotation(entrancePosition, Quaternion.identity);

        GameObject player = new GameObject("Player");
        player.tag = "Player";
        player.transform.SetPositionAndRotation(entrancePosition, Quaternion.identity);

        CharacterController character = player.AddComponent<CharacterController>();
        character.height = 1.5f;
        character.radius = 0.25f;
        character.center = new Vector3(0f, 0.75f, 0f);
        character.stepOffset = 0.5f;
        character.skinWidth = 0.025f;

        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.SetParent(player.transform, false);
        cameraObject.transform.localPosition = new Vector3(0f, 1.35f, 0f);
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = 1000f;
        camera.fieldOfView = 60f;
        cameraObject.AddComponent<AudioListener>();

        FirstPersonController controller = player.AddComponent<FirstPersonController>();
        PlayerInteraction interaction = player.AddComponent<PlayerInteraction>();
        HomeInteriorRuntimeSetup runtime = player.AddComponent<HomeInteriorRuntimeSetup>();

        SetObjectReference(controller, "cameraTransform", cameraObject.transform);
        SetObjectReference(interaction, "playerCamera", camera);
        SetObjectReference(runtime, "playerCamera", camera);
        SetObjectReference(runtime, "footstepClip",
            AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/革靴で歩く.mp3"));
        SetObjectReference(runtime, "ambientClip",
            AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/atmos01.mp3"));
        SetObjectReference(runtime, "settingsFont",
            AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/Resources/SettingsUI/BIZUDMincho-Regular SDF.asset"));
        SetObjectReference(runtime, "moveGuideTexture",
            AssetDatabase.LoadAssetAtPath<Texture>("Assets/Resources/SettingsUI/MoveGuide.png"));
        SetObjectReference(runtime, "interactGuideTexture",
            AssetDatabase.LoadAssetAtPath<Texture>("Assets/Resources/SettingsUI/InteractGuide.png"));
        SetObjectReference(runtime, "doorOpenClip",
            AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/ドアを開ける3.mp3"));
        return interaction;
    }

    [MenuItem("Tools/Home Interior/Install Closet Hide Spot In Existing Scene")]
    public static void InstallClosetHideSpotInExistingScene()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject environment = GameObject.Find("Home Environment");
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        PlayerInteraction interaction =
            player != null ? player.GetComponent<PlayerInteraction>() : null;

        if (environment == null || interaction == null)
        {
            Debug.LogError("HomeInterior: environment or PlayerInteraction was not found.");
            return;
        }

        CreateClosetHideSpot(environment.transform, interaction);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log("HomeInterior: closet hide spot installed without changing door transforms.");
    }

    public static void InstallClosetHideSpotBatch()
    {
        InstallClosetHideSpotInExistingScene();
    }

    [MenuItem("Tools/Home Interior/Install Light Switches In Existing Scene")]
    public static void InstallLightSwitchesInExistingScene()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject environment = GameObject.Find("Home Environment");
        if (environment == null)
        {
            Debug.LogError("HomeInterior: environment was not found.");
            return;
        }

        ConfigureLightSwitches(environment.transform);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log("HomeInterior: three light switches installed.");
    }

    public static void InstallLightSwitchesBatch()
    {
        InstallLightSwitchesInExistingScene();
    }

    private static void ConfigureLightSwitches(Transform environment)
    {
        foreach (HomeInteriorLightSwitch existing in
                 Object.FindObjectsByType<HomeInteriorLightSwitch>(FindObjectsSortMode.None))
        {
            Object.DestroyImmediate(existing);
        }

        ConfigureLightSwitch(environment, "light switch.001", "Room Light 2");
        ConfigureLightSwitch(environment, "light switch.002", "Room Light 3");
        ConfigureLightSwitch(environment, "light switch.006", "Room Light 1");
    }

    private static void ConfigureLightSwitch(
        Transform environment,
        string switchName,
        string lightName)
    {
        Transform switchTransform = FindChildByName(environment, switchName);
        GameObject lightObject = GameObject.Find(lightName);
        Light targetLight = lightObject != null ? lightObject.GetComponent<Light>() : null;
        if (switchTransform == null || targetLight == null)
        {
            Debug.LogError("HomeInterior: could not connect " + switchName + " to " + lightName + ".");
            return;
        }

        HomeInteriorLightSwitch lightSwitch =
            switchTransform.gameObject.AddComponent<HomeInteriorLightSwitch>();
        lightSwitch.Configure(targetLight);
    }

    private static void CreateClosetHideSpot(
        Transform environment,
        PlayerInteraction interaction)
    {
        GameObject previous = GameObject.Find("HomeInterior Closet Hide Spot");
        if (previous != null) Object.DestroyImmediate(previous);

        GameObject previousPoints = GameObject.Find("HomeInterior Closet Hide Points");
        if (previousPoints != null) Object.DestroyImmediate(previousPoints);

        GameObject previousCanvas = GameObject.Find("HomeInterior HUD");
        if (previousCanvas != null) Object.DestroyImmediate(previousCanvas);

        Transform closetDoor = FindChildByName(environment, "closet door.001");
        if (closetDoor == null)
        {
            Debug.LogError("HomeInterior: closet door.001 was not found.");
            return;
        }

        GameObject pointsObject = new GameObject("HomeInterior Closet Hide Points");
        pointsObject.transform.SetParent(environment, false);

        GameObject hidePointObject = new GameObject("HidePoint");
        hidePointObject.transform.SetParent(pointsObject.transform, false);
        hidePointObject.transform.localPosition = new Vector3(-1.5f, 0.08f, -0.55f);
        hidePointObject.transform.localRotation = Quaternion.identity;

        GameObject exitPointObject = new GameObject("ExitPoint");
        exitPointObject.transform.SetParent(pointsObject.transform, false);
        exitPointObject.transform.localPosition = new Vector3(-1.5f, 0.08f, 0.65f);
        exitPointObject.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

        LockerHideSpot hideSpot = closetDoor.GetComponent<LockerHideSpot>();
        if (hideSpot == null) hideSpot = closetDoor.gameObject.AddComponent<LockerHideSpot>();
        hideSpot.ConfigureWithoutDoor(
            hidePointObject.transform,
            exitPointObject.transform);

        GameObject interactionPoint = CreateInteractionPoint();
        SetObjectReference(interaction, "interactionPoint", interactionPoint);
    }

    private static Transform FindChildByName(Transform root, string objectName)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == objectName) return child;
        }
        return null;
    }

    private static GameObject CreateInteractionPoint()
    {
        GameObject canvasObject = new GameObject(
            "HomeInterior HUD",
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GameObject point = new GameObject(
            "InteractionPoint",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        point.layer = 5;
        point.transform.SetParent(canvasObject.transform, false);
        RectTransform rect = point.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(6f, 6f);
        Image image = point.GetComponent<Image>();
        image.sprite = null;
        image.color = Color.white;
        image.raycastTarget = false;
        point.SetActive(false);
        return point;
    }

    private static void CreateLighting()
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.22f, 0.22f, 0.24f);

        GameObject sunObject = new GameObject("Interior Ambient Light");
        Light sun = sunObject.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 0.45f;
        sun.color = new Color(1f, 0.92f, 0.82f);
        sun.shadows = LightShadows.Soft;
        sunObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        Vector3[] roomLights =
        {
            new Vector3(-0.78f, 2.925f, -2.08f),
            new Vector3(1.625f, 2.925f, 0.26f),
            new Vector3(-1.82f, 2.925f, 1.82f)
        };

        for (int i = 0; i < roomLights.Length; i++)
        {
            GameObject lightObject = new GameObject("Room Light " + (i + 1));
            lightObject.transform.position = roomLights[i];
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 5f;
            light.intensity = 1.2f;
            light.color = new Color(1f, 0.84f, 0.67f);
            light.shadows = LightShadows.None;
            light.enabled = false;
        }
    }

    private static void SetObjectReference(Object target, string propertyName, Object value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null) return;
        property.objectReferenceValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }
}
