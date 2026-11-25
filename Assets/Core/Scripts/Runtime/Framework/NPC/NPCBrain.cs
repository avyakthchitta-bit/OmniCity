
using System.Collections;
using UnityEngine;

public class NPCBrain : MonoBehaviour
{
    public enum Behavior
    {
        Idle,
        Wander,
        Chase,
        Return
    }

    [Header("Behavior")]
    [SerializeField] private Behavior startingBehavior = Behavior.Idle;

    [Header("Wandering")]
    [SerializeField] private float wanderRadius = 15f;
    [SerializeField] private float minWaitTime = 2f;
    [SerializeField] private float maxWaitTime = 5f;

    [Header("Chasing")]
    [SerializeField] private float chaseDetectionRadius = 20f;
    [SerializeField] private float chaseUpdateInterval = 0.25f;

    public Behavior CurrentBehavior { get; private set; }

    private NPCMovement movement;
    private Transform player;

    private Vector3 wanderCenter;

    private float nextWanderTime;
    private float nextChaseUpdateTime;

    private bool returnDestinationSet;

    private void Awake()
    {
        movement = GetComponent<NPCMovement>();
        wanderCenter = transform.position;
    }

    private void Start()
    {
        SetBehavior(startingBehavior);
        StartCoroutine(FindPlayer());
    }

    private IEnumerator FindPlayer()
    {
        while (player == null)
        {
            GameObject playerObject =
                GameObject.FindGameObjectWithTag("Player");

            if (playerObject != null)
            {
                player = playerObject.transform;

                Debug.Log(
                    $"[NPC Brain] Found player: {player.name}"
                );

                yield break;
            }

            yield return new WaitForSeconds(0.25f);
        }
    }

    private void Update()
    {
        switch (CurrentBehavior)
        {
            case Behavior.Wander:
                HandleWander();
                break;

            case Behavior.Chase:
                HandleChase();
                break;

            case Behavior.Return:
                HandleReturn();
                break;

            case Behavior.Idle:
                break;
        }
    }

    public void SetBehavior(Behavior newBehavior)
    {
        if (CurrentBehavior == newBehavior)
            return;

        if (movement != null)
            movement.Stop();

        CurrentBehavior = newBehavior;

        nextWanderTime = Time.time;
        nextChaseUpdateTime = Time.time;
        returnDestinationSet = false;

        Debug.Log(
            $"[NPC Brain] {name} behavior changed to {CurrentBehavior}"
        );
    }

    private void HandleWander()
    {
        if (movement == null)
            return;

        if (movement.IsPathPending || movement.IsMoving)
            return;

        if (movement.HasPath)
        {
            if (!movement.HasReachedDestination)
                return;

            movement.Stop();

            nextWanderTime = Time.time +
                Random.Range(minWaitTime, maxWaitTime);

            return;
        }

        if (Time.time < nextWanderTime)
            return;

        if (!movement.MoveToRandomPoint(wanderCenter, wanderRadius))
        {
            nextWanderTime = Time.time + 2f;
        }
    }

    private void HandleChase()
    {
        if (movement == null || player == null)
            return;

        float distance = Vector3.Distance(
            transform.position,
            player.position
        );

        if (distance > chaseDetectionRadius)
        {
            SetBehavior(Behavior.Return);
            return;
        }

        if (Time.time < nextChaseUpdateTime)
            return;

        if (!movement.IsTraversingGap)
            movement.MoveTo(player.position);

        nextChaseUpdateTime =
            Time.time + chaseUpdateInterval;
    }

    private void HandleReturn()
    {
        if (movement == null)
            return;

        if (!returnDestinationSet)
        {
            movement.MoveTo(wanderCenter);
            returnDestinationSet = true;
            return;
        }

        if (movement.IsTraversingGap ||
            movement.IsPathPending ||
            movement.IsMoving)
            return;

        if (movement.HasReachedDestination)
        {
            SetBehavior(Behavior.Wander);
        }
    }
}
