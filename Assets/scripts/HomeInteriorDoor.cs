using System.Collections;
using UnityEngine;

public sealed class HomeInteriorDoor : MonoBehaviour
{
    [SerializeField] private Transform movingPart;
    [SerializeField] private bool sliding;
    [SerializeField] private Vector3 openLocalPosition;
    [SerializeField] private Quaternion openLocalRotation = Quaternion.identity;
    [SerializeField, Min(0.05f)] private float duration = 0.45f;

    private Vector3 closedLocalPosition;
    private Quaternion closedLocalRotation;
    private bool isOpen;
    private Coroutine animationRoutine;

    public void Configure(Transform part, bool isSliding, Vector3 openPosition, Quaternion openRotation)
    {
        movingPart = part;
        sliding = isSliding;
        openLocalPosition = openPosition;
        openLocalRotation = openRotation;
        closedLocalPosition = movingPart.localPosition;
        closedLocalRotation = movingPart.localRotation;
    }

    private void Awake()
    {
        if (movingPart == null) movingPart = transform;
        closedLocalPosition = movingPart.localPosition;
        closedLocalRotation = movingPart.localRotation;
    }

    public void Toggle()
    {
        isOpen = !isOpen;
        if (animationRoutine != null) StopCoroutine(animationRoutine);
        animationRoutine = StartCoroutine(Animate(isOpen));
    }

    private IEnumerator Animate(bool opening)
    {
        Vector3 startPosition = movingPart.localPosition;
        Quaternion startRotation = movingPart.localRotation;
        Vector3 targetPosition = opening && sliding ? openLocalPosition : closedLocalPosition;
        Quaternion targetRotation = opening && !sliding ? openLocalRotation : closedLocalRotation;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            movingPart.localPosition = Vector3.Lerp(startPosition, targetPosition, t);
            movingPart.localRotation = Quaternion.Slerp(startRotation, targetRotation, t);
            yield return null;
        }

        movingPart.localPosition = targetPosition;
        movingPart.localRotation = targetRotation;
        animationRoutine = null;
    }
}
