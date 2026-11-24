
using UnityEngine;
using UnityEngine.AI;

public class NPCNavigationTest : MonoBehaviour
{
    [SerializeField] private Transform destination;
    [SerializeField] private float sampleRadius = 10f;

    private NavMeshAgent agent;

    private void Start()
    {
        agent = GetComponent<NavMeshAgent>();

        if (agent == null)
        {
            Debug.LogError("[NPC Test] No NavMeshAgent found!");
            return;
        }

        if (!agent.isOnNavMesh)
        {
            Debug.LogError("[NPC Test] NPC is not on a NavMesh!");
            return;
        }

        if (destination == null)
        {
            Debug.LogError("[NPC Test] No destination assigned!");
            return;
        }

        if (!NavMesh.SamplePosition(
            destination.position,
            out NavMeshHit hit,
            sampleRadius,
            agent.areaMask))
        {
            Debug.LogError("[NPC Test] Could not find NavMesh near destination!");
            return;
        }

        NavMeshPath path = new NavMeshPath();

        bool foundPath = agent.CalculatePath(hit.position, path);

        Debug.Log($"[NPC Test] Path found: {foundPath}");
        Debug.Log($"[NPC Test] Path status: {path.status}");

        if (foundPath && path.status == NavMeshPathStatus.PathComplete)
        {
            Debug.Log("[NPC Test] Complete path! Starting navigation.");
            agent.SetPath(path);
        }
        else
        {
            Debug.LogWarning("[NPC Test] No complete path to destination.");
        }
    }
}
