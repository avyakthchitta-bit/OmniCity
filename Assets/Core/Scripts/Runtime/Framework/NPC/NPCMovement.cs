
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class NPCMovement : MonoBehaviour
{
    [Header("Gap Traversal")]
    [SerializeField] private float maxHorizontalJumpDistance = 3f;
    [SerializeField] private float jumpDuration = 0.6f;
    [SerializeField] private float jumpArcHeight = 1.5f;
    [SerializeField] private float verticalLandingSearchDistance = 200f;
    [SerializeField] private float landingSearchStep = 0.5f;
    [SerializeField] private float landingTolerance = 1.5f;

    private NavMeshAgent agent;

    private Vector3 currentDestination;
    private bool hasDestination;
    private bool isTraversingGap;

    private Coroutine traversalCoroutine;

    public bool IsMoving =>
        isTraversingGap ||
        (agent != null && agent.velocity.sqrMagnitude > 0.01f);

    public bool IsPathPending =>
        agent != null && agent.pathPending;

    public bool HasPath =>
        agent != null && agent.hasPath;

    public bool IsTraversingGap => isTraversingGap;

    public bool HasReachedDestination =>
        agent != null &&
        agent.isOnNavMesh &&
        !isTraversingGap &&
        !agent.pathPending &&
        agent.hasPath &&
        agent.remainingDistance <= agent.stoppingDistance;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    private void Update()
    {
        if (agent == null || isTraversingGap || !hasDestination)
            return;

        if (!agent.isOnNavMesh || agent.pathPending)
            return;

        if (agent.pathStatus != NavMeshPathStatus.PathPartial)
            return;

        if (!agent.hasPath)
            return;

        if (agent.remainingDistance <= agent.stoppingDistance + 0.2f)
        {
            TryTraverseGap();
        }
    }

    public void MoveTo(Vector3 destination)
    {
        if (agent == null || !agent.isOnNavMesh)
        {
            Debug.LogWarning($"[NPC Movement] {name} is not on a NavMesh!");
            return;
        }

        currentDestination = destination;
        hasDestination = true;

        agent.isStopped = false;
        agent.SetDestination(destination);
    }

    public bool MoveToRandomPoint(Vector3 center, float radius)
    {
        if (agent == null || !agent.isOnNavMesh)
            return false;

        for (int i = 0; i < 15; i++)
        {
            Vector2 randomPoint = Random.insideUnitCircle * radius;

            Vector3 candidate = center +
                new Vector3(randomPoint.x, 0f, randomPoint.y);

            if (!NavMesh.SamplePosition(
                candidate,
                out NavMeshHit hit,
                3f,
                agent.areaMask))
                continue;

            NavMeshPath path = new NavMeshPath();

            if (!agent.CalculatePath(hit.position, path))
                continue;

            if (path.status != NavMeshPathStatus.PathComplete)
                continue;

            currentDestination = hit.position;
            hasDestination = true;

            agent.isStopped = false;
            return agent.SetPath(path);
        }

        return false;
    }

    private void TryTraverseGap()
    {
        Vector3 direction = currentDestination - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.01f)
            return;

        direction.Normalize();

        Vector3 startPosition = transform.position;

        // Search from the maximum distance backward.
        // This chooses the furthest valid landing point within range.
        for (float distance = maxHorizontalJumpDistance;
             distance >= landingSearchStep;
             distance -= landingSearchStep)
        {
            Vector3 candidate = startPosition + direction * distance;

            if (!NavMesh.SamplePosition(
                candidate,
                out NavMeshHit hit,
                verticalLandingSearchDistance,
                agent.areaMask))
                continue;

            Vector3 horizontalDifference = hit.position - startPosition;
            horizontalDifference.y = 0f;

            float horizontalDistance = horizontalDifference.magnitude;

            if (horizontalDistance > maxHorizontalJumpDistance)
                continue;

            if (horizontalDistance < 0.25f)
                continue;

            Vector3 progress = hit.position - startPosition;
            progress.y = 0f;

            if (Vector3.Dot(progress, direction) <= 0f)
                continue;

            Vector3 landingOffset = hit.position - candidate;
            landingOffset.y = 0f;

            if (landingOffset.magnitude > landingTolerance)
                continue;

            traversalCoroutine = StartCoroutine(
                TraverseGap(hit.position)
            );

            return;
        }

        Debug.LogWarning(
            $"[NPC Movement] {name} could not find a landing point within jump range!"
        );
    }

    private IEnumerator TraverseGap(Vector3 landingPosition)
    {
        isTraversingGap = true;

        agent.isStopped = true;
        agent.ResetPath();

        // Temporarily let this script control the NPC position.
        agent.updatePosition = false;

        Vector3 startPosition = transform.position;
        float elapsed = 0f;

        while (elapsed < jumpDuration)
        {
            elapsed += Time.deltaTime;

            float progress = Mathf.Clamp01(elapsed / jumpDuration);

            Vector3 position = Vector3.Lerp(
                startPosition,
                landingPosition,
                progress
            );

            // Add a small arc to the traversal.
            position.y += Mathf.Sin(progress * Mathf.PI) * jumpArcHeight;

            transform.position = position;

            yield return null;
        }

        transform.position = landingPosition;

        // Reconnect to the NavMesh.
        agent.updatePosition = true;

        if (agent.Warp(landingPosition))
        {
            agent.isStopped = false;

            // Recalculate the original destination.
            agent.SetDestination(currentDestination);
        }
        else
        {
            Debug.LogWarning(
                $"[NPC Movement] {name} failed to reconnect to the NavMesh!"
            );
        }

        isTraversingGap = false;
        traversalCoroutine = null;
    }

    public void Stop()
    {
        hasDestination = false;

        if (traversalCoroutine != null)
        {
            StopCoroutine(traversalCoroutine);
            traversalCoroutine = null;
        }

        isTraversingGap = false;

        if (agent == null)
            return;

        agent.updatePosition = true;
        agent.isStopped = true;
        agent.ResetPath();
    }
}
