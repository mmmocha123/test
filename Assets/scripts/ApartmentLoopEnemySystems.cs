using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class EnemyWaypointGraph : MonoBehaviour
{
    private FloorLoopManager floorLoop;
    private readonly List<(EnemyWaypoint, EnemyWaypoint)> crossLinks = new();
    public IReadOnlyList<EnemyWaypoint> AllWaypoints => floorLoop == null ? Array.Empty<EnemyWaypoint>() : floorLoop.FloorsByHeight.SelectMany(f => f.Waypoints).ToArray();
    public void Configure(FloorLoopManager manager) { floorLoop = manager; floorLoop.FloorRecycled += _ => RebuildCrossFloorLinks(); RebuildCrossFloorLinks(); }
    public void RebuildCrossFloorLinks()
    {
        foreach (var link in crossLinks) link.Item1.Disconnect(link.Item2); crossLinks.Clear();
        if (floorLoop == null) return;
        var floors = floorLoop.FloorsByHeight;
        for (int i = 0; i < floors.Count - 1; i++)
        {
            EnemyWaypoint lower = floors[i].Waypoints.FirstOrDefault(w => w.Kind == EnemyWaypointKind.UpperPort);
            EnemyWaypoint upper = floors[i + 1].Waypoints.FirstOrDefault(w => w.Kind == EnemyWaypointKind.LowerPort);
            if (lower == null || upper == null) continue; lower.ConnectBidirectional(upper); crossLinks.Add((lower, upper));
        }
    }
    public EnemyWaypoint Closest(Vector3 position) => AllWaypoints.OrderBy(w => (w.transform.position - position).sqrMagnitude).FirstOrDefault();
}

public enum InvisibleEnemyState { Inactive, Patrol, Chase, LostWait, SafeDisabled }

[RequireComponent(typeof(CharacterController), typeof(AudioSource))]
public sealed class LegacyInvisibleEnemyController : MonoBehaviour
{
    [SerializeField] private float fieldOfView = 70f;
    [SerializeField] private float viewDistance = 8f;
    [SerializeField] private float lostWaitDuration = 2f;
    [SerializeField] private float chaseMemoryDuration = 5f;
    [SerializeField] private float chaseSpeedMultiplier = 1.3f;
    [SerializeField] private float bloodPoolInterval = 5f;
    [SerializeField] private float bloodPoolLifetime = 20f;
    [SerializeField] private float patrolBoundaryDistance = 2.5f;
    [SerializeField] private LayerMask lineOfSightMask = ~0;
    private FirstPersonController player;
    private EnemyWaypointGraph graph;
    private CharacterController controller;
    private AudioSource footsteps;
    private EnemyWaypoint current;
    private EnemyWaypoint previous;
    private float speed;
    private float lostTimer;
    private float stuckTimer;
    private float chaseMemoryRemaining;
    private float roamTimer;
    private float nextBloodPoolTime;
    private Vector3 roamDirection;
    private Vector3 lastKnownPlayerPosition;
    private Material bloodMaterial;
    private readonly List<GameObject> spawnedBloodPools = new();
    private bool hideEndedChase;
    public InvisibleEnemyState State { get; private set; } = InvisibleEnemyState.Inactive;
    public bool IsDangerous => Time.timeScale > 0f && (State is InvisibleEnemyState.Patrol or InvisibleEnemyState.Chase or InvisibleEnemyState.LostWait);
    public event Action FirstHideSurvived;
    public event Action<InvisibleEnemyState> StateChanged;

    public void Configure(FirstPersonController target, EnemyWaypointGraph waypointGraph, AudioClip clip, float volume, float minDistance, float maxDistance)
    {
        player = target; graph = waypointGraph; speed = player.MoveSpeed; controller = GetComponent<CharacterController>(); controller.radius = .28f; controller.height = 1.7f;
        footsteps = GetComponent<AudioSource>(); footsteps.clip = clip; footsteps.loop = true; footsteps.playOnAwake = false; footsteps.volume = Mathf.Clamp01(volume); footsteps.spatialBlend = 1f; footsteps.minDistance = Mathf.Max(0.01f, minDistance); footsteps.maxDistance = Mathf.Max(footsteps.minDistance, maxDistance); footsteps.rolloffMode = AudioRolloffMode.Logarithmic;
        SphereCollider contact = gameObject.AddComponent<SphereCollider>(); contact.radius = .42f; contact.center = Vector3.up * .8f; contact.isTrigger = true;
        gameObject.SetActive(false);
    }

    public void ActivateAt(Transform spawn)
    {
        gameObject.SetActive(true);
        controller.enabled = false;
        FloorRuntimeData spawnFloor =
            spawn.GetComponentInParent<FloorRuntimeData>();
        transform.SetParent(
            spawnFloor != null ? spawnFloor.transform : spawn.parent,
            true);
        transform.position = spawn.position;
        controller.enabled = true;
        current = null;
        previous = null;
        hideEndedChase = false;
        roamTimer = 0f;
        nextBloodPoolTime = Time.time + bloodPoolInterval;
        SetState(InvisibleEnemyState.Patrol);
    }
    public void ResetInactive()
    {
        if (footsteps != null) footsteps.Stop();
        foreach (GameObject pool in spawnedBloodPools)
        {
            if (pool != null) Destroy(pool);
        }
        spawnedBloodPools.Clear();
        SetState(InvisibleEnemyState.Inactive);
        hideEndedChase = false;
        gameObject.SetActive(false);
    }
    public void MakeSafe() { SetState(InvisibleEnemyState.SafeDisabled); if (footsteps != null) footsteps.Stop(); }

    private void Update()
    {
        if (Time.timeScale <= 0f) { if (footsteps != null) footsteps.Stop(); return; }
        if (player == null || graph == null || State is InvisibleEnemyState.Inactive or InvisibleEnemyState.SafeDisabled) return;
        SpawnBloodPoolWhenDue();
        bool sees = CanSeePlayer();

        if (State == InvisibleEnemyState.Patrol)
        {
            if (sees)
            {
                BeginChase();
            }
            else
            {
                MoveWandering();
            }
            return;
        }

        if (State == InvisibleEnemyState.Chase)
        {
            if (player.IsHidden)
            {
                BeginLostWait(true);
                return;
            }

            if (sees)
            {
                chaseMemoryRemaining = chaseMemoryDuration;
                lastKnownPlayerPosition = player.transform.position;
            }
            else
            {
                chaseMemoryRemaining -= Time.deltaTime;
                if (chaseMemoryRemaining <= 0f)
                {
                    BeginLostWait(false);
                    return;
                }
            }

            if (sees)
            {
                MoveDirectlyTowards(
                    player.transform.position,
                    speed * chaseSpeedMultiplier);
            }
            else
            {
                MoveAlongGraph(true, speed * chaseSpeedMultiplier);
            }
            return;
        }

        if (State == InvisibleEnemyState.LostWait)
        {
            footsteps.Stop();
            if (!player.IsHidden && sees) { BeginChase(); hideEndedChase = false; }
            else { lostTimer += Time.deltaTime; if (lostTimer >= lostWaitDuration) { SetState(InvisibleEnemyState.Patrol); if (hideEndedChase && player.IsHidden) FirstHideSurvived?.Invoke(); hideEndedChase = false; } }
            return;
        }
    }

    private void BeginChase()
    {
        chaseMemoryRemaining = chaseMemoryDuration;
        lastKnownPlayerPosition = player.transform.position;
        current = graph.Closest(transform.position);
        previous = null;
        SetState(InvisibleEnemyState.Chase);
    }

    private void BeginLostWait(bool byHide) { SetState(InvisibleEnemyState.LostWait); lostTimer = 0f; hideEndedChase = byHide; if (footsteps != null) footsteps.Stop(); }

    private void SetState(InvisibleEnemyState value)
    {
        if (State == value) return;
        State = value;
        StateChanged?.Invoke(State);
    }
    private bool CanSeePlayer()
    {
        return SharedEnemyPerception.CanSeePlayer(transform, player,
            viewDistance, fieldOfView, lineOfSightMask);
    }

    private void MoveWandering()
    {
        roamTimer -= Time.deltaTime;
        Vector3 eye = transform.position + Vector3.up * .5f;
        Vector3 proposedPosition = transform.position +
            roamDirection.normalized * .9f;
        bool obstacleAhead = roamDirection.sqrMagnitude > .01f &&
            Physics.Raycast(
                eye,
                roamDirection,
                .8f,
                lineOfSightMask,
                QueryTriggerInteraction.Ignore);
        bool hasGroundAhead = Physics.Raycast(
            proposedPosition + Vector3.up * 1.2f,
            Vector3.down,
            out _,
            2.5f,
            lineOfSightMask,
            QueryTriggerInteraction.Ignore);

        EnemyWaypoint nearestBoundaryGuide = graph.Closest(proposedPosition);
        bool outsidePatrolArea = nearestBoundaryGuide == null ||
            HorizontalSqrDistance(
                proposedPosition,
                nearestBoundaryGuide.transform.position) >
            patrolBoundaryDistance * patrolBoundaryDistance;

        if (!hasGroundAhead || outsidePatrolArea)
        {
            EnemyWaypoint returnGuide = graph.Closest(transform.position);
            if (returnGuide != null)
            {
                roamDirection = returnGuide.transform.position -
                    transform.position;
                roamDirection.y = 0f;
            }
            roamTimer = UnityEngine.Random.Range(1.5f, 3f);
        }
        else if (roamTimer <= 0f || obstacleAhead)
        {
            float yaw = UnityEngine.Random.Range(-180f, 180f);
            roamDirection = Quaternion.Euler(0f, yaw, 0f) *
                Vector3.forward;
            roamTimer = UnityEngine.Random.Range(2f, 5f);
        }

        MoveContinuous(roamDirection, speed);
    }

    private static float HorizontalSqrDistance(Vector3 a, Vector3 b)
    {
        float x = a.x - b.x;
        float z = a.z - b.z;
        return x * x + z * z;
    }

    private void MoveDirectlyTowards(Vector3 target, float moveSpeed)
    {
        Vector3 direction = target - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < .01f)
        {
            StopFootsteps();
            return;
        }

        MoveContinuous(direction.normalized, moveSpeed);
    }

    private void MoveAlongGraph(bool chase, float moveSpeed)
    {
        if (current == null) current = graph.Closest(transform.position); if (current == null) return;
        Vector3 delta = current.transform.position - transform.position; delta.y = Mathf.Clamp(delta.y, -1f, 1f);
        if (delta.magnitude < .25f)
        {
            // Waypointへ実際に到着してから所属Floorを切り替える。
            // 移動先を選んだ瞬間に親変更すると、Floor循環時に
            // 元Floorから敵だけ取り残されるため禁止する。
            FloorRuntimeData arrivedFloor =
                current.GetComponentInParent<FloorRuntimeData>();
            if (arrivedFloor != null &&
                transform.parent != arrivedFloor.transform)
            {
                transform.SetParent(arrivedFloor.transform, true);
            }

            stuckTimer = 0f;
            SelectNext(chase);
        }
        else
        {
            Vector3 beforeMove = transform.position;
            Vector3 motion = delta.normalized * moveSpeed;
            controller.Move(motion * Time.deltaTime);
            float movedDistance = Vector3.Distance(
                beforeMove,
                transform.position);

            if (movedDistance < 0.002f)
            {
                stuckTimer += Time.deltaTime;
            }
            else
            {
                stuckTimer = 0f;
            }

            // 瞬間移動は行わない。停止時は現在位置から経路を
            // 再評価し、連続移動のまま別の接続へ向かわせる。
            if (stuckTimer >= .75f)
            {
                current = graph.Closest(transform.position);
                previous = null;
                if (current != null) SelectNext(true);
                stuckTimer = 0f;
            }

            Vector3 flat = new(motion.x, 0, motion.z);
            if (flat.sqrMagnitude > .01f)
            {
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(flat),
                    Time.deltaTime * 8f);
            }

            if (movedDistance > 0.002f)
            {
                if (!footsteps.isPlaying) footsteps.Play();
            }
            else if (footsteps.isPlaying)
            {
                footsteps.Stop();
            }
        }
    }

    private void MoveContinuous(Vector3 direction, float moveSpeed)
    {
        Vector3 beforeMove = transform.position;
        controller.Move(direction.normalized * moveSpeed * Time.deltaTime);
        float movedDistance = Vector3.Distance(beforeMove, transform.position);

        if (direction.sqrMagnitude > .01f)
        {
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(new Vector3(
                    direction.x, 0f, direction.z)),
                Time.deltaTime * 6f);
        }

        UpdateFootsteps(movedDistance);
    }

    private void UpdateFootsteps(float movedDistance)
    {
        if (movedDistance > .002f)
        {
            if (!footsteps.isPlaying) footsteps.Play();
        }
        else
        {
            StopFootsteps();
        }
    }

    private void StopFootsteps()
    {
        if (footsteps != null && footsteps.isPlaying) footsteps.Stop();
    }

    private void SpawnBloodPoolWhenDue()
    {
        if (Time.time < nextBloodPoolTime) return;
        nextBloodPoolTime = Time.time + bloodPoolInterval;

        Vector3 rayOrigin = transform.position + Vector3.up * 1f;
        if (!Physics.Raycast(
                rayOrigin,
                Vector3.down,
                out RaycastHit groundHit,
                3f,
                lineOfSightMask,
                QueryTriggerInteraction.Ignore))
        {
            return;
        }

        GameObject pool = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pool.name = "EnemyBloodPool";
        pool.transform.position = groundHit.point + groundHit.normal * .006f;
        pool.transform.rotation = Quaternion.FromToRotation(
            Vector3.up,
            groundHit.normal);
        pool.transform.localScale = new Vector3(.45f, .008f, .45f);
        Destroy(pool.GetComponent<Collider>());

        if (bloodMaterial == null)
        {
            bloodMaterial = new Material(
                Shader.Find("Universal Render Pipeline/Lit"));
            Color bloodColor = new Color(.32f, .005f, .008f, 1f);
            bloodMaterial.SetColor("_BaseColor", bloodColor);
            bloodMaterial.color = bloodColor;
            bloodMaterial.SetFloat("_Smoothness", .2f);
        }

        pool.GetComponent<Renderer>().material = bloodMaterial;
        FloorRuntimeData floor =
            transform.GetComponentInParent<FloorRuntimeData>();
        if (floor != null)
        {
            pool.transform.SetParent(floor.transform, true);
        }
        spawnedBloodPools.Add(pool);
        Destroy(pool, bloodPoolLifetime);
    }
    private void SelectNext(bool chase)
    {
        var candidates = current.Connections.Where(c => c != null).ToList(); if (candidates.Count == 0) { current = graph.Closest(transform.position); return; }
        EnemyWaypoint next;
        if (chase) next = candidates.OrderBy(c => (c.transform.position - player.transform.position).sqrMagnitude).First();
        else { var preferred = candidates.Where(c => c != previous).ToList(); if (preferred.Count == 0) preferred = candidates; next = preferred[UnityEngine.Random.Range(0, preferred.Count)]; }
        previous = current; current = next;
    }
}

public sealed class EnemyContactGameOver : MonoBehaviour
{
    private InvisibleEnemyController enemy; private FirstPersonController player; private ApartmentLoopGameOverManager gameOver;
    public void Configure(InvisibleEnemyController e, FirstPersonController p, ApartmentLoopGameOverManager g) { enemy = e; player = p; gameOver = g; }
    private void OnControllerColliderHit(ControllerColliderHit hit) { if (enemy != null && enemy.IsDangerous && player != null && !player.IsHidden && (hit.transform == player.transform || hit.transform.IsChildOf(player.transform))) gameOver.TriggerGameOver(); }
    private void OnTriggerEnter(Collider other) { if (enemy != null && enemy.IsDangerous && player != null && !player.IsHidden && (other.transform == player.transform || other.transform.IsChildOf(player.transform))) gameOver.TriggerGameOver(); }
    private void Update()
    {
        if (enemy == null || !enemy.IsDangerous ||
            player == null || player.IsHidden) return;

        CharacterController playerCollider =
            player.GetCharacterController();
        if (playerCollider == null) return;

        Vector3 delta = player.transform.position - enemy.transform.position;
        float horizontalDistance = new Vector2(delta.x, delta.z).magnitude;
        float contactDistance = playerCollider.radius +
            enemy.ContactRadius + .12f;

        if (horizontalDistance <= contactDistance &&
            Mathf.Abs(delta.y) <= 1.5f)
        {
            gameOver.TriggerGameOver();
        }
    }
}
