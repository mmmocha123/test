using System.Collections.Generic;
using UnityEngine;

public class FloorLoopManager : MonoBehaviour
{
    // プレイヤー本体です。
    // The player Transform.
    [SerializeField] private Transform player;

    // 循環させるフロアの親オブジェクトです。
    // Floor root objects to recycle.
    [SerializeField] private List<Transform> floors =
        new List<Transform>();

    // 隣接するフロア間の高さです。
    // Vertical distance between adjacent floors.
    [SerializeField] private float floorHeight = 3f;

    // 階段へ何m入った時点で再配置するかを指定します。
    // Distance from the floor level at which recycling begins.
    [SerializeField] private float recycleTriggerOffset = 0.2f;

    // 次に上方向の再配置を行う高さです。
    // Next upward recycling threshold.
    private float nextUpTriggerY;

    // 次に下方向の再配置を行う高さです。
    // Next downward recycling threshold.
    private float nextDownTriggerY;

    // ゲーム開始時にフロアと判定位置を初期化します。
    // Initializes floors and recycling thresholds.
    private void Start()
    {
        if (player == null || floors.Count < 3)
        {
            return;
        }

        SortFloors();

        // プレイヤーに最も近いフロアのY座標を基準にします。
        // Uses the floor closest to the player as the starting reference.
        float startingFloorY = GetClosestFloorY();

        nextUpTriggerY =
            startingFloorY + recycleTriggerOffset;

        nextDownTriggerY =
            startingFloorY - recycleTriggerOffset;
    }

    // プレイヤー移動後にフロアの再配置を判定します。
    // Checks floor recycling after player movement.
    private void LateUpdate()
    {
        if (player == null ||
            floors.Count < 3 ||
            floorHeight <= 0f ||
            recycleTriggerOffset <= 0f)
        {
            return;
        }

        // 上方向の判定位置を越えた場合です。
        // Runs when the player crosses the upward threshold.
        if (player.position.y >= nextUpTriggerY)
        {
            MoveLowestFloorToTop();

            // 戻ってきた場合の下方向判定を、
            // 今回越えた階段の少し下へ設定します。
            // Places the downward threshold below the crossed boundary.
            nextDownTriggerY =
                nextUpTriggerY - recycleTriggerOffset * 2f;

            // 次の階の上方向判定へ進めます。
            // Advances the next upward threshold by one floor.
            nextUpTriggerY += floorHeight;
        }
        // 下方向の判定位置を越えた場合です。
        // Runs when the player crosses the downward threshold.
        else if (player.position.y <= nextDownTriggerY)
        {
            MoveHighestFloorToBottom();

            // 戻ってきた場合の上方向判定を、
            // 今回越えた階段の少し上へ設定します。
            // Places the upward threshold above the crossed boundary.
            nextUpTriggerY =
                nextDownTriggerY + recycleTriggerOffset * 2f;

            // 次の階の下方向判定へ進めます。
            // Advances the next downward threshold by one floor.
            nextDownTriggerY -= floorHeight;
        }
    }

    // プレイヤーに最も近いフロアのY座標を取得します。
    // Finds the floor Y position closest to the player.
    private float GetClosestFloorY()
    {
        float closestY = floors[0].position.y;
        float closestDistance =
            Mathf.Abs(player.position.y - closestY);

        foreach (Transform floor in floors)
        {
            float distance =
                Mathf.Abs(player.position.y - floor.position.y);

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestY = floor.position.y;
            }
        }

        return closestY;
    }

    // 最下段のフロアを最上段へ移動します。
    // Moves the lowest floor above the highest floor.
    private void MoveLowestFloorToTop()
    {
        SortFloors();

        Transform lowestFloor = floors[0];
        Transform highestFloor = floors[floors.Count - 1];

        lowestFloor.position =
            highestFloor.position + Vector3.up * floorHeight;

        // 移動した床・壁・ドアのCollider位置を即座に反映します。
        // Immediately synchronizes moved colliders with the physics engine.
        Physics.SyncTransforms();

        SortFloors();
    }

    // 最上段のフロアを最下段へ移動します。
    // Moves the highest floor below the lowest floor.
    private void MoveHighestFloorToBottom()
    {
        SortFloors();

        Transform lowestFloor = floors[0];
        Transform highestFloor = floors[floors.Count - 1];

        highestFloor.position =
            lowestFloor.position - Vector3.up * floorHeight;

        // 移動した床・壁・ドアのCollider位置を即座に反映します。
        // Immediately synchronizes moved colliders with the physics engine.
        Physics.SyncTransforms();

        SortFloors();
    }

    // フロアをワールドY座標の低い順に並べます。
    // Sorts floors by world-space Y position.
    private void SortFloors()
    {
        floors.RemoveAll(floor => floor == null);

        floors.Sort(
            (floorA, floorB) =>
                floorA.position.y.CompareTo(floorB.position.y)
        );
    }
}