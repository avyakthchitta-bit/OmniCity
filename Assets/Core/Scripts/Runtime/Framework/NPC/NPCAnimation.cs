
using UnityEngine;
using UnityEngine.AI;

public class NPCAnimation : MonoBehaviour
{
    [SerializeField] private Animator animator;

    private NavMeshAgent agent;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();
    }

    private void Update()
    {
        if (agent == null || animator == null)
            return;

        float speed = agent.velocity.magnitude;
        animator.SetFloat("Speed", speed);
    }
}
