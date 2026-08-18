using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

public sealed class ApartmentLoopNavMeshRuntime : MonoBehaviour
{
    private FloorLoopManager floorLoop;
    private NavMeshSurface surface;
    private readonly List<NavMeshLink> floorLinks = new();
    private bool updating;
    public bool IsReady { get; private set; }
    public event Action<FloorRuntimeData> BeforeFloorMove;
    public event Action NavMeshUpdated;

    public FloorRuntimeData GetClosestPhysicalFloor(Vector3 position)
    {
        return floorLoop.FloorsByHeight
            .OrderBy(f => Mathf.Abs(
                position.y - (f.ExitPoint != null
                    ? f.ExitPoint.position.y
                    : f.transform.position.y)))
            .FirstOrDefault();
    }

    public int GetLogicalFloorAt(Vector3 position)
    {
        FloorRuntimeData floor = GetClosestPhysicalFloor(position);
        return floor != null ? floor.LogicalFloorIndex : 0;
    }

    public void Configure(FloorLoopManager manager)
    {
        floorLoop = manager;
        surface = gameObject.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.All;
        surface.useGeometry = NavMeshCollectGeometry.RenderMeshes;
        surface.layerMask = ~0;
        surface.ignoreNavMeshAgent = true;
        surface.ignoreNavMeshObstacle = true;
        surface.BuildNavMesh();
        RebuildFloorLinks();
        IsReady = surface.navMeshData != null;
        floorLoop.FloorRecycling += OnFloorRecycling;
        floorLoop.FloorRecycled += OnFloorRecycled;
        floorLoop.FloorsRestored += OnFloorsRestored;
    }

    private void OnFloorRecycling(FloorRuntimeData floor)
    {
        BeforeFloorMove?.Invoke(floor);
    }

    private void OnFloorRecycled(FloorRuntimeData floor)
    {
        if (!updating) StartCoroutine(RebuildAfterFloorMove());
    }

    private void OnFloorsRestored()
    {
        BeforeFloorMove?.Invoke(null);
        if (!updating) StartCoroutine(RebuildAfterFloorMove());
    }

    private IEnumerator RebuildAfterFloorMove()
    {
        updating = true;
        AsyncOperation operation = surface.UpdateNavMesh(surface.navMeshData);
        while (!operation.isDone) yield return null;
        updating = false;
        IsReady = true;
        RebuildFloorLinks();
        NavMeshUpdated?.Invoke();
    }

    private void RebuildFloorLinks()
    {
        IReadOnlyList<FloorRuntimeData> floors = floorLoop.FloorsByHeight;
        int required = Mathf.Max(0, floors.Count - 1);
        while (floorLinks.Count < required)
        {
            GameObject linkObject = new($"EnemyFloorLink_{floorLinks.Count}");
            linkObject.transform.SetParent(transform, false);
            NavMeshLink link = linkObject.AddComponent<NavMeshLink>();
            link.width = .8f;
            link.bidirectional = true;
            floorLinks.Add(link);
        }
        for (int i = 0; i < floorLinks.Count; i++)
        {
            bool active = i < required;
            floorLinks[i].gameObject.SetActive(active);
            if (!active) continue;
            EnemyWaypoint lowerStair = floors[i].Waypoints
                .Where(w => w.Kind == EnemyWaypointKind.Stair).LastOrDefault();
            EnemyWaypoint upperEntry = floors[i + 1].Waypoints
                .FirstOrDefault(w => w.Kind == EnemyWaypointKind.LowerPort);
            floorLinks[i].enabled = false;
            floorLinks[i].startTransform = lowerStair != null
                ? lowerStair.transform : floors[i].transform;
            floorLinks[i].endTransform = upperEntry != null
                ? upperEntry.transform : floors[i + 1].transform;
            floorLinks[i].enabled = true;
        }
    }
}

[RequireComponent(typeof(NavMeshAgent), typeof(AudioSource))]
public sealed class InvisibleEnemyController : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] private float fieldOfView = 70f;
    [SerializeField] private float viewDistance = 8f;
    [SerializeField] private LayerMask lineOfSightMask = ~0;
    [SerializeField] private float lostWaitDuration = 2f;
    [SerializeField] private float chaseSightMemory = 10f;
    [Header("Navigation")]
    [SerializeField] private float patrolRadius = 2f;
    [SerializeField, Range(0f, 1f)] private float crossFloorPatrolChance = .85f;
    [SerializeField] private float patrolMinimumTravelDistance = 5f;
    [SerializeField] private float destinationThreshold = .45f;
    [SerializeField] private float chaseUpdateInterval = .2f;
    [SerializeField] private float acceleration = 8f;
    [SerializeField] private float angularSpeed = 180f;
    [SerializeField] private float stoppingDistance = .2f;
    [Header("Blood")]
    [SerializeField] private float bloodPoolInterval = 5f;
    [SerializeField] private float bloodPoolLifetime = 20f;

    private FirstPersonController player;
    private EnemyWaypointGraph patrolAreas;
    private ApartmentLoopNavMeshRuntime runtimeNavMesh;
    private NavMeshAgent agent;
    private AudioSource footsteps;
    private float speed;
    private float stateTimer;
    private float chaseUpdateTimer;
    private float chaseSightTimer;
    private float stalledTimer;
    private float nextBloodPoolTime;
    private bool hideEndedChase;
    private bool waitingForNavMesh;
    private bool moveWithRecycledFloor;
    private FloorRuntimeData recycledFloor;
    private Vector3 positionBeforeFloorMove;
    private Vector3 floorPositionBeforeMove;
    private Vector3 destination;
    private Material bloodMaterial;
    private readonly List<GameObject> bloodPools = new();
    private readonly Queue<FloorRuntimeData> recentPatrolFloors = new();
    private Vector3 lastPatrolDestination;

    public InvisibleEnemyState State { get; private set; } = InvisibleEnemyState.Inactive;
    public bool IsDangerous => Time.timeScale > 0f &&
        State is InvisibleEnemyState.Patrol or InvisibleEnemyState.Chase or InvisibleEnemyState.LostWait;
    public float ContactRadius => agent != null ? agent.radius : .28f;
    public event Action FirstHideSurvived;
    public event Action<InvisibleEnemyState> StateChanged;

    public void Configure(FirstPersonController target,
        EnemyWaypointGraph areas, ApartmentLoopNavMeshRuntime navMesh,
        AudioClip clip, float volume, float minDistance, float maxDistance)
    {
        player = target;
        patrolAreas = areas;
        runtimeNavMesh = navMesh;
        speed = player.MoveSpeed;
        agent = GetComponent<NavMeshAgent>();
        agent.speed = speed;
        agent.acceleration = acceleration;
        agent.angularSpeed = angularSpeed;
        agent.stoppingDistance = stoppingDistance;
        agent.radius = .28f;
        agent.height = 1.7f;
        agent.autoRepath = true;
        agent.autoTraverseOffMeshLink = true;
        agent.enabled = false;

        footsteps = GetComponent<AudioSource>();
        footsteps.clip = clip;
        footsteps.loop = true;
        footsteps.playOnAwake = false;
        footsteps.volume = Mathf.Clamp01(volume);
        footsteps.spatialBlend = 1f;
        footsteps.minDistance = Mathf.Max(.01f, minDistance);
        footsteps.maxDistance = Mathf.Max(footsteps.minDistance, maxDistance);
        footsteps.rolloffMode = AudioRolloffMode.Logarithmic;

        SphereCollider contact = gameObject.AddComponent<SphereCollider>();
        contact.radius = .42f;
        contact.center = Vector3.up * .8f;
        contact.isTrigger = true;
        runtimeNavMesh.BeforeFloorMove += PrepareForFloorMove;
        runtimeNavMesh.NavMeshUpdated += RecoverAfterNavMeshUpdate;
        gameObject.SetActive(false);
    }

    public void ActivateAt(Transform spawn)
    {
        gameObject.SetActive(true);
        // NavMeshAgentはFloorの子にしない。階段で別Floorへ移動した後も
        // 古い親Floorに残ると、循環時に誤って一緒に飛ばされるため。
        transform.SetParent(null, true);
        if (!NavMesh.SamplePosition(spawn.position, out NavMeshHit hit, 4f, NavMesh.AllAreas))
        {
            Debug.LogError("Enemy spawn has no nearby NavMesh.", spawn);
            gameObject.SetActive(false);
            return;
        }
        transform.position = hit.position;
        agent.enabled = true;
        agent.Warp(hit.position);
        waitingForNavMesh = false;
        hideEndedChase = false;
        nextBloodPoolTime = Time.time + bloodPoolInterval;
        SetState(InvisibleEnemyState.Patrol);
        ChoosePatrolDestination();
    }

    public void ResetInactive()
    {
        StopMovement();
        foreach (GameObject pool in bloodPools) if (pool != null) Destroy(pool);
        bloodPools.Clear();
        if (agent != null) agent.enabled = false;
        SetState(InvisibleEnemyState.Inactive);
        gameObject.SetActive(false);
    }

    public void MakeSafe()
    {
        StopMovement();
        SetState(InvisibleEnemyState.SafeDisabled);
    }

    private void Update()
    {
        if (Time.timeScale <= 0f || waitingForNavMesh || !agent.enabled || !agent.isOnNavMesh)
        {
            StopFootsteps();
            return;
        }
        if (State is InvisibleEnemyState.Inactive or InvisibleEnemyState.SafeDisabled) return;
        SpawnBloodPoolWhenDue();
        bool sees = CanSeePlayer();

        switch (State)
        {
            case InvisibleEnemyState.Patrol:
                if (sees) BeginChase();
                else if (!agent.pathPending && (!agent.hasPath || agent.remainingDistance <= destinationThreshold))
                    ChoosePatrolDestination();
                break;
            case InvisibleEnemyState.Chase:
                if (player.IsHidden) BeginLostWait(true);
                else
                {
                    chaseSightTimer = sees
                        ? chaseSightMemory
                        : chaseSightTimer - Time.deltaTime;
                    if (chaseSightTimer <= 0f)
                    {
                        BeginLostWait(false);
                        break;
                    }
                    chaseUpdateTimer -= Time.deltaTime;
                    if (chaseUpdateTimer <= 0f)
                    {
                        chaseUpdateTimer = chaseUpdateInterval;
                        SetDestination(player.transform.position);
                    }
                }
                break;
            case InvisibleEnemyState.LostWait:
                if (!player.IsHidden && sees) BeginChase();
                else if ((stateTimer += Time.deltaTime) >= lostWaitDuration)
                {
                    SetState(InvisibleEnemyState.Patrol);
                    if (hideEndedChase && player.IsHidden) FirstHideSurvived?.Invoke();
                    hideEndedChase = false;
                    ChoosePatrolDestination();
                }
                break;
        }
        UpdateFootsteps();
        DetectAndRecoverStall();
    }

    private void BeginChase()
    {
        agent.isStopped = false;
        chaseUpdateTimer = 0f;
        chaseSightTimer = chaseSightMemory;
        SetState(InvisibleEnemyState.Chase);
    }

    private void BeginLostWait(bool byHide)
    {
        hideEndedChase = byHide;
        stateTimer = 0f;
        StopMovement();
        SetState(InvisibleEnemyState.LostWait);
    }

    private void ChoosePatrolDestination()
    {
        // 高確率で現在とは別のFloorを選ぶ。直近2回のFloorも避け、
        // 同じ廊下を短く往復し続けないようにする。
        if (UnityEngine.Random.value <= crossFloorPatrolChance &&
            TryChooseCrossFloorDestination())
        {
            return;
        }

        // 最初に現在と同じNavMesh領域内の自然なランダム地点を探す。
        // 別Floorの到達不能候補だけを引き続けて停止するのを防ぐ。
        float localRadius = Mathf.Max(6f, patrolRadius * 3f);
        for (int attempt = 0; attempt < 20; attempt++)
        {
            Vector2 random = UnityEngine.Random.insideUnitCircle * localRadius;
            Vector3 candidate = transform.position +
                new Vector3(random.x, 0f, random.y);
            if (NavMesh.SamplePosition(candidate, out NavMeshHit localHit,
                    2f, NavMesh.AllAreas) &&
                HasCompletePath(localHit.position) &&
                Vector3.Distance(transform.position, localHit.position) >
                    patrolMinimumTravelDistance &&
                Vector3.Distance(lastPatrolDestination, localHit.position) > 3f)
            {
                SetDestination(localHit.position);
                lastPatrolDestination = localHit.position;
                return;
            }
        }

        EnemyWaypoint[] choices = patrolAreas.AllWaypoints
            .Where(w => w != null).ToArray();
        for (int attempt = 0; attempt < 12; attempt++)
        {
            Vector3 center = choices.Length > 0
                ? choices[UnityEngine.Random.Range(0, choices.Length)].transform.position
                : transform.position;
            Vector2 random = UnityEngine.Random.insideUnitCircle * patrolRadius;
            Vector3 candidate = center + new Vector3(random.x, 0f, random.y);
            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, patrolRadius, NavMesh.AllAreas) &&
                HasCompletePath(hit.position))
            {
                SetDestination(hit.position);
                lastPatrolDestination = hit.position;
                return;
            }
        }
        // 一時的に候補が取れなくても永続停止させない。
        agent.isStopped = false;
        destination = transform.position;
    }

    private bool TryChooseCrossFloorDestination()
    {
        FloorRuntimeData currentFloor =
            runtimeNavMesh.GetClosestPhysicalFloor(transform.position);
        HashSet<FloorRuntimeData> recent = new(recentPatrolFloors);
        FloorRuntimeData[] candidates = patrolAreas.AllWaypoints
            .Where(w => w != null)
            .Select(w => w.GetComponentInParent<FloorRuntimeData>())
            .Where(f => f != null && f != currentFloor && !recent.Contains(f))
            .Distinct()
            .OrderBy(_ => UnityEngine.Random.value)
            .ToArray();

        // 全候補が履歴に含まれる場合だけ履歴制限を緩める。
        if (candidates.Length == 0)
        {
            candidates = patrolAreas.AllWaypoints
                .Where(w => w != null)
                .Select(w => w.GetComponentInParent<FloorRuntimeData>())
                .Where(f => f != null && f != currentFloor)
                .Distinct()
                .OrderBy(_ => UnityEngine.Random.value)
                .ToArray();
        }

        foreach (FloorRuntimeData floor in candidates)
        {
            EnemyWaypoint[] floorAreas = floor.Waypoints
                .Where(w => w.Kind is EnemyWaypointKind.Hallway or
                    EnemyWaypointKind.Landing)
                .OrderBy(_ => UnityEngine.Random.value)
                .ToArray();
            foreach (EnemyWaypoint area in floorAreas)
            {
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    Vector2 random = UnityEngine.Random.insideUnitCircle *
                        patrolRadius;
                    Vector3 candidate = area.transform.position +
                        new Vector3(random.x, 0f, random.y);
                    if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit,
                            patrolRadius, NavMesh.AllAreas) ||
                        !HasCompletePath(hit.position) ||
                        Vector3.Distance(transform.position, hit.position) <
                            patrolMinimumTravelDistance)
                    {
                        continue;
                    }

                    SetDestination(hit.position);
                    lastPatrolDestination = hit.position;
                    recentPatrolFloors.Enqueue(floor);
                    while (recentPatrolFloors.Count > 2)
                        recentPatrolFloors.Dequeue();
                    return true;
                }
            }
        }
        return false;
    }

    private bool SetDestination(Vector3 requested)
    {
        if (!NavMesh.SamplePosition(requested, out NavMeshHit hit, 2f, NavMesh.AllAreas)) return false;
        if (!HasCompletePath(hit.position)) return false;
        destination = hit.position;
        agent.isStopped = false;
        return agent.SetDestination(destination);
    }

    private bool HasCompletePath(Vector3 target)
    {
        NavMeshPath path = new();
        return NavMesh.CalculatePath(transform.position, target,
            NavMesh.AllAreas, path) &&
            path.status == NavMeshPathStatus.PathComplete;
    }

    private void DetectAndRecoverStall()
    {
        if (State is not (InvisibleEnemyState.Patrol or InvisibleEnemyState.Chase) ||
            agent.pathPending || !agent.hasPath ||
            agent.remainingDistance <= destinationThreshold)
        {
            stalledTimer = 0f;
            return;
        }
        stalledTimer = agent.velocity.sqrMagnitude < .0025f
            ? stalledTimer + Time.deltaTime : 0f;
        if (stalledTimer < 1.25f) return;
        stalledTimer = 0f;
        agent.ResetPath();
        if (State == InvisibleEnemyState.Chase)
            SetDestination(player.transform.position);
        else
            ChoosePatrolDestination();
    }

    private bool CanSeePlayer()
    {
        if (player.IsHidden) return false;
        Vector3 eye = transform.position + Vector3.up * 1.45f;
        Vector3 target = player.transform.position + Vector3.up * 1.1f;
        Vector3 delta = target - eye;
        if (delta.magnitude > viewDistance || Vector3.Angle(transform.forward, delta) > fieldOfView * .5f) return false;
        return Physics.Raycast(eye, delta.normalized, out RaycastHit hit,
            viewDistance, lineOfSightMask, QueryTriggerInteraction.Ignore) &&
            (hit.transform == player.transform || hit.transform.IsChildOf(player.transform));
    }

    private void PrepareForFloorMove(FloorRuntimeData movedFloor)
    {
        if (!gameObject.activeSelf || agent == null || !agent.enabled) return;
        positionBeforeFloorMove = transform.position;
        recycledFloor = movedFloor;
        moveWithRecycledFloor = movedFloor != null &&
            runtimeNavMesh.GetClosestPhysicalFloor(transform.position) == movedFloor;
        if (moveWithRecycledFloor)
            floorPositionBeforeMove = movedFloor.transform.position;
        transform.SetParent(null, true);
        waitingForNavMesh = true;
        agent.isStopped = true;
        agent.ResetPath();
        agent.enabled = false;
    }

    private void RecoverAfterNavMeshUpdate()
    {
        if (!gameObject.activeSelf || State == InvisibleEnemyState.Inactive) return;
        Vector3 desiredPosition = positionBeforeFloorMove;
        if (moveWithRecycledFloor && recycledFloor != null)
        {
            desiredPosition += recycledFloor.transform.position -
                floorPositionBeforeMove;
        }

        // 4m間隔の別Floorを拾わないよう検索半径を狭く固定する。
        if (!NavMesh.SamplePosition(desiredPosition, out NavMeshHit hit,
                .9f, NavMesh.AllAreas) ||
            Mathf.Abs(hit.position.y - desiredPosition.y) > .9f)
        {
            Debug.LogError("Enemy could not recover onto NavMesh after Floor recycle.", this);
            return;
        }
        transform.position = hit.position;
        agent.enabled = true;
        agent.Warp(hit.position);
        waitingForNavMesh = false;
        recycledFloor = null;
        moveWithRecycledFloor = false;
        if (State == InvisibleEnemyState.Chase) SetDestination(player.transform.position);
        else if (State == InvisibleEnemyState.Patrol) ChoosePatrolDestination();
        else StopMovement();
    }

    private void StopMovement()
    {
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }
        StopFootsteps();
    }

    private void UpdateFootsteps()
    {
        bool moving = State is InvisibleEnemyState.Patrol or InvisibleEnemyState.Chase &&
            agent.velocity.sqrMagnitude > .01f;
        if (moving && !footsteps.isPlaying) footsteps.Play();
        else if (!moving) StopFootsteps();
    }

    private void StopFootsteps()
    {
        if (footsteps != null && footsteps.isPlaying) footsteps.Stop();
    }

    private void SpawnBloodPoolWhenDue()
    {
        if (Time.time < nextBloodPoolTime) return;
        nextBloodPoolTime = Time.time + bloodPoolInterval;
        if (!Physics.Raycast(transform.position + Vector3.up, Vector3.down,
            out RaycastHit ground, 3f, lineOfSightMask, QueryTriggerInteraction.Ignore)) return;
        GameObject pool = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pool.name = "EnemyBloodPool";
        pool.transform.position = ground.point + ground.normal * .006f;
        pool.transform.rotation = Quaternion.FromToRotation(Vector3.up, ground.normal);
        pool.transform.localScale = new Vector3(.45f, .008f, .45f);
        Destroy(pool.GetComponent<Collider>());
        if (bloodMaterial == null)
        {
            bloodMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            Color color = new(.32f, .005f, .008f, 1f);
            bloodMaterial.SetColor("_BaseColor", color);
            bloodMaterial.color = color;
        }
        pool.GetComponent<Renderer>().sharedMaterial = bloodMaterial;
        FloorRuntimeData floor = runtimeNavMesh.GetClosestPhysicalFloor(
            transform.position);
        if (floor != null) pool.transform.SetParent(floor.transform, true);
        bloodPools.Add(pool);
        Destroy(pool, bloodPoolLifetime);
    }

    private void SetState(InvisibleEnemyState value)
    {
        if (State == value) return;
        State = value;
        StateChanged?.Invoke(State);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = State == InvisibleEnemyState.Chase ? Color.red :
            State == InvisibleEnemyState.LostWait ? Color.yellow : Color.cyan;
        Gizmos.DrawWireSphere(transform.position, viewDistance);
        Gizmos.DrawLine(transform.position, destination);
        Vector3 left = Quaternion.Euler(0f, -fieldOfView * .5f, 0f) * transform.forward;
        Vector3 right = Quaternion.Euler(0f, fieldOfView * .5f, 0f) * transform.forward;
        Gizmos.DrawRay(transform.position + Vector3.up * 1.45f, left * viewDistance);
        Gizmos.DrawRay(transform.position + Vector3.up * 1.45f, right * viewDistance);
    }
}
