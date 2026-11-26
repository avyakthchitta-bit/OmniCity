
using System.Collections;
using UnityEngine;

public class MoveTrigger : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform targetPoint;
    [SerializeField] private string targetTag = "Player";

    [Header("Movement")]
    [SerializeField] private float moveDuration = 1f;
    [SerializeField] private AnimationCurve movementCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Trigger Settings")]
    [SerializeField] private bool triggerOnce = true;

    private bool hasTriggered;
    private Coroutine moveCoroutine;

    private void OnTriggerEnter(Collider other)
    {
        if (triggerOnce && hasTriggered)
            return;

        if (!other.CompareTag(targetTag))
            return;

        if (targetPoint == null)
            return;

        Transform targetObject = other.transform;

        hasTriggered = true;

        if (moveCoroutine != null)
            StopCoroutine(moveCoroutine);

        moveCoroutine = StartCoroutine(
            MoveObject(targetObject)
        );
    }

    private IEnumerator MoveObject(Transform targetObject)
    {
        Vector3 startPosition = targetObject.position;
        Vector3 endPosition = targetPoint.position;

        float elapsed = 0f;

        while (elapsed < moveDuration)
        {
            if (targetObject == null)
                yield break;

            elapsed += Time.deltaTime;

            float progress = Mathf.Clamp01(elapsed / moveDuration);
            float easedProgress = movementCurve.Evaluate(progress);

            targetObject.position = Vector3.Lerp(
                startPosition,
                endPosition,
                easedProgress
            );

            yield return null;
        }

        if (targetObject != null)
            targetObject.position = endPosition;

        moveCoroutine = null;
    }
}
