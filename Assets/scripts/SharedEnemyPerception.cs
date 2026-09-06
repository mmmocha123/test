using UnityEngine;

public static class SharedEnemyPerception
{
    public static bool CanSeePlayer(Transform observer, FirstPersonController player,
        float viewDistance, float fieldOfView, LayerMask lineOfSightMask,
        float eyeHeight = 1.45f, float targetHeight = 1.1f)
    {
        if (observer == null || player == null || player.IsHidden) return false;
        Vector3 eye = observer.position + Vector3.up * eyeHeight;
        Vector3 target = player.transform.position + Vector3.up * targetHeight;
        Vector3 delta = target - eye;
        if (delta.magnitude > viewDistance ||
            Vector3.Angle(observer.forward, delta) > fieldOfView * .5f) return false;
        return Physics.Raycast(eye, delta.normalized, out RaycastHit hit,
            viewDistance, lineOfSightMask, QueryTriggerInteraction.Ignore) &&
            (hit.transform == player.transform || hit.transform.IsChildOf(player.transform));
    }
}
