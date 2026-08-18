using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum FloorMoveDirection { Up = 1, Down = -1 }

[Serializable]
public sealed class FloorLoopSnapshot
{
    public int currentLogicalFloor;
    public float nextUpTriggerY;
    public float nextDownTriggerY;
    public List<FloorState> floors = new List<FloorState>();

    [Serializable]
    public sealed class FloorState
    {
        public FloorRuntimeData floor;
        public Vector3 position;
        public Quaternion rotation;
        public int logicalFloor;
    }
}

public class FloorLoopManager : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private List<Transform> floors = new List<Transform>();
    [SerializeField] private float floorHeight = 3f;
    [SerializeField] private float recycleTriggerOffset = 0.2f;
    [SerializeField] private int initialLogicalFloor = 4;
    private float nextUpTriggerY;
    private float nextDownTriggerY;
    private bool allowUp = true;
    private bool allowDown = true;
    private const float StoryArrivalTolerance = 0.65f;

    public int CurrentLogicalFloorIndex { get; private set; }
    public float NextUpTriggerY => nextUpTriggerY;
    public float NextDownTriggerY => nextDownTriggerY;
    public event Action<int, int, FloorMoveDirection> FloorChanged;
    public event Action<FloorRuntimeData> FloorRecycled;
    public event Action<FloorRuntimeData> FloorRecycling;
    public event Action FloorsRestored;
    public event Action<FloorMoveDirection> BlockedDirectionAttempt;
    public IReadOnlyList<FloorRuntimeData> FloorsByHeight => floors.Where(f => f != null).OrderBy(f => f.position.y).Select(EnsureRuntimeData).ToArray();

    private void Start()
    {
        if (player == null || floors.Count < 3) return;
        SortFloors();
        float startingFloorY = GetClosestFloorY();
        nextUpTriggerY = startingFloorY + floorHeight + recycleTriggerOffset;
        nextDownTriggerY = startingFloorY - recycleTriggerOffset;
        CurrentLogicalFloorIndex = initialLogicalFloor;
        int storyFloorIndex = FindStartingStoryFloorIndex();
        for (int i = 0; i < floors.Count; i++) EnsureRuntimeData(floors[i]).SetLogicalFloor(initialLogicalFloor + i - storyFloorIndex);
    }

    private void LateUpdate()
    {
        if (player == null || floors.Count < 3 || floorHeight <= 0f || recycleTriggerOffset <= 0f) return;
        if (player.position.y >= nextUpTriggerY)
        {
            if (!allowUp)
            {
                BlockPlayerAtBoundary(FloorMoveDirection.Up);
                return;
            }

            MoveLowestFloorToTop();
            nextDownTriggerY = nextUpTriggerY - recycleTriggerOffset * 2f;
            nextUpTriggerY += floorHeight;
        }
        else if (player.position.y <= nextDownTriggerY)
        {
            if (!allowDown)
            {
                BlockPlayerAtBoundary(FloorMoveDirection.Down);
                return;
            }

            MoveHighestFloorToBottom();
            nextUpTriggerY = nextDownTriggerY + recycleTriggerOffset * 2f;
            nextDownTriggerY -= floorHeight;
        }


        UpdateLogicalFloorAtCorridor();
    }

    private void UpdateLogicalFloorAtCorridor()
    {
        FloorRuntimeData arrivedFloor = null;
        float closestDistance = float.PositiveInfinity;

        foreach (FloorRuntimeData floor in FloorsByHeight)
        {
            Transform exitPoint = FindExitPoint(floor.transform);
            if (exitPoint == null) continue;

            float distance = Mathf.Abs(
                player.position.y - exitPoint.position.y);

            if (distance <= StoryArrivalTolerance &&
                distance < closestDistance)
            {
                closestDistance = distance;
                arrivedFloor = floor;
            }
        }

        if (arrivedFloor == null ||
            arrivedFloor.LogicalFloorIndex == CurrentLogicalFloorIndex)
        {
            return;
        }

        int difference =
            arrivedFloor.LogicalFloorIndex - CurrentLogicalFloorIndex;

        // 通常移動では隣接階だけを通知する。Checkpoint等による
        // Transform復元をStory進行として誤検出しない。
        if (Mathf.Abs(difference) != 1) return;

        int previous = CurrentLogicalFloorIndex;
        CurrentLogicalFloorIndex = arrivedFloor.LogicalFloorIndex;
        FloorMoveDirection direction = difference > 0
            ? FloorMoveDirection.Up
            : FloorMoveDirection.Down;
        FloorChanged?.Invoke(previous, CurrentLogicalFloorIndex, direction);
    }

    private void BlockPlayerAtBoundary(FloorMoveDirection direction)
    {
        float safeY = direction == FloorMoveDirection.Up
            ? nextUpTriggerY - 0.18f
            : nextDownTriggerY + 0.18f;
        Vector3 safePosition = player.position;
        safePosition.y = safeY;

        FirstPersonController controller =
            player.GetComponent<FirstPersonController>();

        if (controller != null)
        {
            controller.RestorePose(safePosition, player.rotation);
        }
        else
        {
            player.position = safePosition;
        }

        Physics.SyncTransforms();
        BlockedDirectionAttempt?.Invoke(direction);
    }

    public void SetMovementPermissions(bool up, bool down)
    {
        allowUp = up;
        allowDown = down;
    }

    private float GetClosestFloorY() => floors.OrderBy(f => Mathf.Abs(player.position.y - f.position.y)).First().position.y;

    private int FindStartingStoryFloorIndex()
    {
        int bestIndex = 0;
        float bestDistance = float.PositiveInfinity;

        for (int i = 0; i < floors.Count; i++)
        {
            Transform exitPoint = FindExitPoint(floors[i]);
            float referenceY = exitPoint != null
                ? exitPoint.position.y
                : floors[i].position.y;
            float distance = Mathf.Abs(player.position.y - referenceY);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private static Transform FindExitPoint(Transform floor)
    {
        LockerHideSpot hideSpot =
            floor.GetComponentInChildren<LockerHideSpot>(true);

        if (hideSpot == null) return null;

        Transform current = hideSpot.transform;
        while (current != null && current != floor)
        {
            Transform exit = current.Find("ExitPoint");
            if (exit != null) return exit;
            current = current.parent;
        }

        return null;
    }

    private void MoveLowestFloorToTop()
    {
        SortFloors();
        Transform moved = floors[0];
        FloorRuntimeData data = EnsureRuntimeData(moved);
        FloorRecycling?.Invoke(data);
        int logical = EnsureRuntimeData(floors[^1]).LogicalFloorIndex + 1;
        moved.position = floors[^1].position + Vector3.up * floorHeight;
        data.SetLogicalFloor(logical);
        Physics.SyncTransforms();
        SortFloors();
        FloorRecycled?.Invoke(data);
    }

    private void MoveHighestFloorToBottom()
    {
        SortFloors();
        Transform moved = floors[^1];
        FloorRuntimeData data = EnsureRuntimeData(moved);
        FloorRecycling?.Invoke(data);
        int logical = EnsureRuntimeData(floors[0]).LogicalFloorIndex - 1;
        moved.position = floors[0].position - Vector3.up * floorHeight;
        data.SetLogicalFloor(logical);
        Physics.SyncTransforms();
        SortFloors();
        FloorRecycled?.Invoke(data);
    }

    private FloorRuntimeData EnsureRuntimeData(Transform floor)
    {
        FloorRuntimeData data = floor.GetComponent<FloorRuntimeData>();
        return data != null ? data : floor.gameObject.AddComponent<FloorRuntimeData>();
    }

    private void SortFloors()
    {
        floors.RemoveAll(f => f == null);
        floors.Sort((a, b) => a.position.y.CompareTo(b.position.y));
    }

    public FloorRuntimeData GetCurrentFloor()
    {
        FloorRuntimeData logicalMatch = FloorsByHeight.FirstOrDefault(f => f.LogicalFloorIndex == CurrentLogicalFloorIndex);
        return logicalMatch != null ? logicalMatch : FloorsByHeight.OrderBy(f => Mathf.Abs(player.position.y - f.transform.position.y)).FirstOrDefault();
    }

    public FloorLoopSnapshot CaptureState()
    {
        var snapshot = new FloorLoopSnapshot { currentLogicalFloor = CurrentLogicalFloorIndex, nextUpTriggerY = nextUpTriggerY, nextDownTriggerY = nextDownTriggerY };
        foreach (FloorRuntimeData floor in FloorsByHeight)
            snapshot.floors.Add(new FloorLoopSnapshot.FloorState { floor = floor, position = floor.transform.position, rotation = floor.transform.rotation, logicalFloor = floor.LogicalFloorIndex });
        return snapshot;
    }

    public void RestoreState(FloorLoopSnapshot snapshot)
    {
        if (snapshot == null) return;
        foreach (var state in snapshot.floors)
        {
            if (state.floor == null) continue;
            state.floor.transform.SetPositionAndRotation(state.position, state.rotation);
            state.floor.SetLogicalFloor(state.logicalFloor);
        }
        CurrentLogicalFloorIndex = snapshot.currentLogicalFloor;
        nextUpTriggerY = snapshot.nextUpTriggerY;
        nextDownTriggerY = snapshot.nextDownTriggerY;
        SortFloors();
        Physics.SyncTransforms();
        FloorsRestored?.Invoke();
    }
}
