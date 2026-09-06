using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public enum HomeInteriorEnemyState { Inactive, Patrol, Chase }

[RequireComponent(typeof(NavMeshAgent))]
public sealed class HomeInteriorEnemyController : MonoBehaviour
{
    [SerializeField] private float viewDistance = 9f;
    [SerializeField, Range(1f, 179f)] private float fieldOfView = 90f;
    [SerializeField] private float patrolSpeed = 1.35f;
    [SerializeField] private float chaseSpeed = 3.7f;
    [SerializeField] private float contactRadius = .45f;
    [SerializeField] private LayerMask lineOfSightMask = ~0;
    private FirstPersonController player;
    private ApartmentLoopGameOverManager gameOver;
    private NavMeshAgent agent;
    private readonly List<Transform> waypoints = new();
    private Vector3 spawnPosition;
    private Quaternion spawnRotation;
    private int currentWaypoint = -1;
    private int previousWaypoint = -1;
    private float nextChaseUpdate;

    public HomeInteriorEnemyState State { get; private set; }
    public bool IsActive => State != HomeInteriorEnemyState.Inactive;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.radius = .32f;
        agent.height = 1.7f;
        agent.angularSpeed = 420f;
        agent.acceleration = 12f;
        agent.stoppingDistance = .15f;
    }

    public void Configure(FirstPersonController target,
        ApartmentLoopGameOverManager gameOverManager,
        IEnumerable<Transform> patrolPoints)
    {
        player = target;
        gameOver = gameOverManager;
        waypoints.Clear();
        if (patrolPoints != null)
            foreach (Transform point in patrolPoints) if (point != null) waypoints.Add(point);
        spawnPosition = transform.position;
        spawnRotation = transform.rotation;
    }

    public void SetActive(bool active)
    {
        if (!active)
        {
            State = HomeInteriorEnemyState.Inactive;
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);
        State = HomeInteriorEnemyState.Patrol;
        StartCoroutine(PlaceAndPatrol());
    }

    private System.Collections.IEnumerator PlaceAndPatrol()
    {
        yield return null;
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 3f, NavMesh.AllAreas))
        {
            agent.Warp(hit.position);
            spawnPosition = hit.position;
        }
        ChooseWaypoint();
    }

    private void Update()
    {
        if (State == HomeInteriorEnemyState.Inactive || player == null ||
            agent == null || !agent.enabled || !agent.isOnNavMesh) return;

        if (player.IsHidden)
        {
            if (State == HomeInteriorEnemyState.Chase)
            {
                State = HomeInteriorEnemyState.Patrol;
                ChooseWaypoint();
            }
            return;
        }

        if (CanSeePlayer()) State = HomeInteriorEnemyState.Chase;
        if (State == HomeInteriorEnemyState.Chase)
        {
            agent.speed = chaseSpeed;
            if (Time.time >= nextChaseUpdate)
            {
                nextChaseUpdate = Time.time + .12f;
                agent.SetDestination(player.transform.position);
            }
        }
        else
        {
            agent.speed = patrolSpeed;
            if (!agent.pathPending && agent.remainingDistance <= .35f) ChooseWaypoint();
        }

        Vector3 delta = player.transform.position - transform.position;
        CharacterController controller = player.GetCharacterController();
        float playerRadius = controller != null ? controller.radius : .25f;
        if (!player.IsHidden && new Vector2(delta.x, delta.z).magnitude <=
            contactRadius + playerRadius + .12f && Mathf.Abs(delta.y) < 1.5f)
            gameOver?.TriggerGameOver();
    }

    private bool CanSeePlayer()
    {
        return SharedEnemyPerception.CanSeePlayer(transform, player,
            viewDistance, fieldOfView, lineOfSightMask);
    }

    private void ChooseWaypoint()
    {
        if (waypoints.Count == 0) return;
        List<int> candidates = new();
        for (int i = 0; i < waypoints.Count; i++)
            if (i != currentWaypoint && i != previousWaypoint) candidates.Add(i);
        if (candidates.Count == 0)
            for (int i = 0; i < waypoints.Count; i++) if (i != currentWaypoint) candidates.Add(i);
        if (candidates.Count == 0) candidates.Add(0);
        previousWaypoint = currentWaypoint;
        currentWaypoint = candidates[Random.Range(0, candidates.Count)];
        agent.speed = patrolSpeed;
        agent.SetDestination(waypoints[currentWaypoint].position);
    }

    public void ResetToSpawn()
    {
        if (!IsActive) return;
        agent.ResetPath();
        transform.SetPositionAndRotation(spawnPosition, spawnRotation);
        if (NavMesh.SamplePosition(spawnPosition, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            agent.Warp(hit.position);
        State = HomeInteriorEnemyState.Patrol;
        ChooseWaypoint();
    }
}
