using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class CrowKeyEventController : MonoBehaviour
{
    [SerializeField] private float approachDistance = 2.5f;
    [SerializeField] private float flightDuration = 1.5f;
    [SerializeField] private float flightHeight = 4.5f;

    private GameObject crow;
    private GameObject key;
    private FloorRuntimeData floor;
    private bool keyEnabled;
    private bool flightStarted;

    public event Action KeyCollected;
    public Transform KeyTransform => key != null ? key.transform : null;

    public void ConfigureRuntime(FloorRuntimeData owner)
    {
        floor = owner;
        crow = CreateCrowPlaceholder();
        key = CreateKeyPlaceholder();
        gameObject.SetActive(false);
    }

    public void ActivateEvent()
    {
        if (floor == null || floor.LeftmostCorridorLight == null)
        {
            Debug.LogError(
                "Crow/Key event requires a left corridor light.",
                this);
            return;
        }

        StopAllCoroutines();
        Vector3 lightPosition =
            floor.LeftmostCorridorLight.transform.position;
        Vector3 groundPosition = new Vector3(
            lightPosition.x,
            floor.CorridorGroundY,
            lightPosition.z);

        // 両方とも選択した左端廊下灯の真下へ配置する。
        crow.transform.position =
            groundPosition + Vector3.up * 0.55f;
        crow.transform.rotation = Quaternion.identity;
        key.transform.position =
            groundPosition + new Vector3(0.28f, 0.12f, 0.08f);
        key.transform.rotation =
            Quaternion.Euler(0f, 0f, 90f);

        keyEnabled = false;
        flightStarted = false;
        crow.SetActive(true);
        key.SetActive(true);
        gameObject.SetActive(true);
        StartCoroutine(WaitForApproachAndFly());
    }

    private IEnumerator WaitForApproachAndFly()
    {
        while (Camera.main == null ||
            Vector3.Distance(
                Camera.main.transform.position,
                crow.transform.position) > approachDistance)
        {
            yield return null;
        }

        flightStarted = true;
        keyEnabled = true;

        Vector3 startPosition = crow.transform.position;
        float startTime = Time.time;

        while (Time.time - startTime < flightDuration)
        {
            float progress = Mathf.Clamp01(
                (Time.time - startTime) /
                Mathf.Max(0.01f, flightDuration));
            float eased = progress * progress *
                (3f - 2f * progress);
            crow.transform.position =
                startPosition + Vector3.up *
                flightHeight * eased;
            crow.transform.Rotate(
                Vector3.up,
                240f * Time.deltaTime,
                Space.World);
            yield return null;
        }

        crow.SetActive(false);
    }

    private void Update()
    {
        if (!flightStarted || !keyEnabled ||
            key == null || !key.activeSelf ||
            Camera.main == null || Mouse.current == null)
        {
            return;
        }

        if (Vector3.Distance(
                Camera.main.transform.position,
                key.transform.position) <= 2f &&
            Mouse.current.leftButton.wasPressedThisFrame)
        {
            key.SetActive(false);
            keyEnabled = false;
            KeyCollected?.Invoke();
        }
    }

    public void ResetCompleted()
    {
        StopAllCoroutines();
        keyEnabled = false;
        flightStarted = false;
        gameObject.SetActive(false);
    }

    private GameObject CreateCrowPlaceholder()
    {
        GameObject root = new GameObject("CrowPlaceholder");
        root.transform.SetParent(transform, false);

        Material black = CreateMaterial(
            new Color(0.015f, 0.018f, 0.025f));
        CreatePart(root.transform, "Body", PrimitiveType.Sphere,
            Vector3.zero, new Vector3(0.45f, 0.3f, 0.65f), black);
        CreatePart(root.transform, "Head", PrimitiveType.Sphere,
            new Vector3(0f, 0.18f, 0.32f),
            new Vector3(0.28f, 0.28f, 0.28f), black);
        CreatePart(root.transform, "LeftWing", PrimitiveType.Cube,
            new Vector3(-0.34f, 0f, 0f),
            new Vector3(0.55f, 0.05f, 0.35f), black);
        CreatePart(root.transform, "RightWing", PrimitiveType.Cube,
            new Vector3(0.34f, 0f, 0f),
            new Vector3(0.55f, 0.05f, 0.35f), black);
        CreatePart(root.transform, "Beak", PrimitiveType.Cube,
            new Vector3(0f, 0.16f, 0.55f),
            new Vector3(0.09f, 0.07f, 0.25f),
            CreateMaterial(new Color(0.2f, 0.18f, 0.08f)));
        return root;
    }

    private GameObject CreateKeyPlaceholder()
    {
        GameObject root = new GameObject("KeyPlaceholder");
        root.transform.SetParent(transform, false);
        Material gold = CreateMaterial(
            new Color(0.85f, 0.62f, 0.08f));

        CreatePart(root.transform, "Shaft", PrimitiveType.Cube,
            Vector3.zero, new Vector3(0.08f, 0.08f, 0.48f), gold);
        CreatePart(root.transform, "KeyToothA", PrimitiveType.Cube,
            new Vector3(0.09f, 0f, -0.19f),
            new Vector3(0.18f, 0.08f, 0.09f), gold);
        CreatePart(root.transform, "KeyToothB", PrimitiveType.Cube,
            new Vector3(0.08f, 0f, -0.08f),
            new Vector3(0.14f, 0.08f, 0.08f), gold);
        CreatePart(root.transform, "KeyBow", PrimitiveType.Cylinder,
            new Vector3(0f, 0f, 0.29f),
            new Vector3(0.22f, 0.04f, 0.22f), gold);

        SphereCollider interactionCollider =
            root.AddComponent<SphereCollider>();
        interactionCollider.radius = 0.35f;
        interactionCollider.isTrigger = true;
        return root;
    }

    private static void CreatePart(
        Transform parent,
        string name,
        PrimitiveType type,
        Vector3 localPosition,
        Vector3 localScale,
        Material material)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localScale = localScale;
        Destroy(part.GetComponent<Collider>());
        part.GetComponent<Renderer>().material = material;
    }

    private static Material CreateMaterial(Color color)
    {
        Material material = new Material(
            Shader.Find("Universal Render Pipeline/Lit"));
        material.SetColor("_BaseColor", color);
        material.color = color;
        return material;
    }
}
