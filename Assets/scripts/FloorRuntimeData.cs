using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class FloorRuntimeData : MonoBehaviour
{
    public int LogicalFloorIndex { get; private set; }
    public LockerHideSpot Locker { get; private set; }
    public ApartmentLoopDoorRoleController Door { get; private set; }
    public IReadOnlyList<Light> CorridorLights => corridorLights;
    public IReadOnlyList<EnemyWaypoint> Waypoints => waypoints;
    public Light LeftmostCorridorLight { get; private set; }
    public float CorridorGroundY { get; private set; }
    public Transform ExitPoint { get; private set; }
    public Transform UpperFace { get; private set; }
    public Transform LowerFace { get; private set; }
    public Transform EnemySpawnPoint { get; private set; }
    public CrowKeyEventController CrowKeyEvent { get; private set; }
    private readonly List<Light> corridorLights = new();
    private readonly List<EnemyWaypoint> waypoints = new();

    public void SetLogicalFloor(int value) => LogicalFloorIndex = value;

    public void ConfigureRuntime()
    {
        Locker = GetComponentInChildren<LockerHideSpot>(true);
        corridorLights.Clear();
        corridorLights.AddRange(GetComponentsInChildren<Light>(true).OrderBy(l => l.transform.position.x));
        if (Locker != null)
        {
            Door = Locker.GetComponent<ApartmentLoopDoorRoleController>();
            if (Door == null) Door = Locker.gameObject.AddComponent<ApartmentLoopDoorRoleController>();

            Transform lockerRoot = Locker.transform;
            while (lockerRoot.parent != null && lockerRoot.name != "Locker")
            {
                lockerRoot = lockerRoot.parent;
            }

            LeftmostCorridorLight = corridorLights
                .Where(light => light != null)
                .OrderBy(light => HorizontalDistanceSquared(
                    light.transform.position,
                    lockerRoot.position))
                .FirstOrDefault();

            ExitPoint = lockerRoot.Find("ExitPoint");
            CorridorGroundY = ExitPoint != null
                ? ExitPoint.position.y
                : lockerRoot.position.y;
        }
        Transform eventRoot = FindOrCreate(transform, "ApartmentLoopRuntimePoints");
        UpperFace = CreateMarker(eventRoot, "UpperFaceObject", new Vector3(2.2f, 2.4f, 2.2f));
        LowerFace = CreateMarker(eventRoot, "LowerFaceObject", new Vector3(2.2f, -1.3f, 2.2f));
        Vector3 exitLocalPosition = ExitPoint != null
            ? transform.InverseTransformPoint(ExitPoint.position)
            : new Vector3(2.8f, -3.1f, 16f);
        EnemySpawnPoint = CreatePoint(
            eventRoot,
            "EnemySpawnPoint",
            exitLocalPosition + Vector3.up * 0.05f);
        GameObject crowRoot = new("CrowKeyEvent");
        crowRoot.transform.SetParent(eventRoot, false);
        CrowKeyEvent = crowRoot.AddComponent<CrowKeyEventController>();
        CrowKeyEvent.ConfigureRuntime(this);
        BuildWaypoints(eventRoot);
    }

    private void BuildWaypoints(Transform root)
    {
        Transform wr = FindOrCreate(root, "EnemyWaypoints");
        waypoints.Clear();
        Vector3 exit = ExitPoint != null
            ? transform.InverseTransformPoint(ExitPoint.position)
            : new Vector3(2.8f, -3.1f, 16f);
        Vector3 stairBase = new Vector3(2.8f, exit.y, 0f);
        Vector3[] positions =
        {
            exit,
            Vector3.Lerp(exit, stairBase, .25f),
            Vector3.Lerp(exit, stairBase, .5f),
            Vector3.Lerp(exit, stairBase, .75f),
            stairBase,
            new Vector3(0f, exit.y + 1f, -1.7f),
            new Vector3(-2.45f, exit.y + 2f, -.8f),
            new Vector3(0f, exit.y + 3f, 0f),
            new Vector3(2.8f, exit.y + 4f, 0f)
        };
        EnemyWaypointKind[] kinds =
        {
            EnemyWaypointKind.Hallway,
            EnemyWaypointKind.Hallway,
            EnemyWaypointKind.Hallway,
            EnemyWaypointKind.Hallway,
            EnemyWaypointKind.LowerPort,
            EnemyWaypointKind.Stair,
            EnemyWaypointKind.Landing,
            EnemyWaypointKind.Stair,
            EnemyWaypointKind.UpperPort
        };
        for (int i = 0; i < positions.Length; i++)
        {
            GameObject node = new($"Waypoint_{kinds[i]}_{i}");
            node.transform.SetParent(wr, false);
            node.transform.localPosition = positions[i];
            EnemyWaypoint waypoint = node.AddComponent<EnemyWaypoint>();
            waypoint.Kind = kinds[i];
            waypoints.Add(waypoint);
        }
        for (int i = 0; i < waypoints.Count - 1; i++) waypoints[i].ConnectBidirectional(waypoints[i + 1]);
    }

    private static Transform FindOrCreate(Transform parent, string name)
    {
        Transform found = parent.Find(name);
        if (found != null) return found;
        GameObject go = new(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }
    private static Transform CreatePoint(Transform parent, string name, Vector3 position)
    {
        GameObject go = new(name); go.transform.SetParent(parent, false); go.transform.localPosition = position; return go.transform;
    }
    private static Transform CreateMarker(Transform parent, string name, Vector3 position)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere); go.name = name; go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = new Vector3(.35f,.45f,.2f); Destroy(go.GetComponent<Collider>()); go.SetActive(false); return go.transform;
    }

    private static float HorizontalDistanceSquared(Vector3 a, Vector3 b)
    {
        float x = a.x - b.x;
        float z = a.z - b.z;
        return x * x + z * z;
    }
}
