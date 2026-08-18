using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class HomeInteriorDoorInteractor : MonoBehaviour
{
    [SerializeField] private Camera playerCamera;
    [SerializeField] private float interactionDistance = 2.5f;
    [SerializeField] private GameObject interactionPoint;

    public void Configure(Camera camera)
    {
        playerCamera = camera;
        interactionPoint = CreateInteractionPoint();
    }

    private void Start() => SetInteractionPointVisible(false);

    private void OnDisable() => SetInteractionPointVisible(false);

    private void Update()
    {
        HomeInteriorDoor target = FindDoorTarget();
        SetInteractionPointVisible(target != null);

        bool clicked = Mouse.current != null &&
            Mouse.current.leftButton.wasPressedThisFrame;
        bool pressedInteract = Keyboard.current != null &&
            Keyboard.current.eKey.wasPressedThisFrame;

        if (target == null || (!clicked && !pressedInteract))
        {
            return;
        }

        target.Toggle();
    }

    private HomeInteriorDoor FindDoorTarget()
    {
        if (playerCamera == null || Cursor.lockState != CursorLockMode.Locked)
            return null;

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        RaycastHit[] hits = Physics.SphereCastAll(
            ray,
            0.12f,
            interactionDistance,
            ~0,
            QueryTriggerInteraction.Ignore);

        System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
        foreach (RaycastHit hit in hits)
        {
            HomeInteriorDoor door = hit.collider.GetComponentInParent<HomeInteriorDoor>();
            if (door == null) continue;
            return door;
        }
        return null;
    }

    private GameObject CreateInteractionPoint()
    {
        GameObject canvasObject = new GameObject(
            "HomeInteriorInteractionCanvas",
            typeof(Canvas),
            typeof(CanvasScaler));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        GameObject point = new GameObject("InteractionPoint", typeof(RectTransform), typeof(Image));
        point.transform.SetParent(canvasObject.transform, false);
        RectTransform rect = point.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(8f, 8f);
        Image image = point.GetComponent<Image>();
        image.color = Color.white;
        image.raycastTarget = false;
        image.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/Knob.psd");
        return point;
    }

    private void SetInteractionPointVisible(bool visible)
    {
        if (interactionPoint != null && interactionPoint.activeSelf != visible)
            interactionPoint.SetActive(visible);
    }

    private void OnDestroy()
    {
        if (interactionPoint != null)
            Destroy(interactionPoint.transform.parent.gameObject);
    }
}
