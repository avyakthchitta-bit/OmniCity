using UnityEngine;
using System.Collections;
using Blocks.Gameplay.Core;

namespace Blocks.Gameplay.Platformer
{
    /// <summary>
    /// Grants the DoubleJumpAbility to an interactor.
    /// </summary>
    public class GrantDoubleJumpEffect : MonoBehaviour, IInteractionEffect
    {
        #region Fields & Properties

        [Header("Effect Settings")]
        [Tooltip("The priority of this effect in the interaction chain. Higher values are executed first.")]
        [SerializeField] private int priority = 0;

        [Header("Notification")]
        [Tooltip("The notification event to raise when double jump is granted.")]
        [SerializeField] private NotificationEvent notificationEvent;

        public int Priority => priority;

        #endregion

        #region Public Methods

        public IEnumerator ApplyEffect(
            GameObject interactor,
            GameObject interactable)
        {
            if (interactor.TryGetComponent<CoreMovement>(
                out var coreMovement))
            {
                if (!coreMovement.HasAbility<DoubleJumpAbility>())
                {
                    var newAbility =
                        interactor.AddComponent<DoubleJumpAbility>();

                    coreMovement.AddAbility(newAbility);

                    Debug.Log(
                        "Granted Double Jump ability.",
                        interactor);

                    if (notificationEvent != null)
                    {
                        notificationEvent.Raise(
                            new NotificationPayload
                            {
                                clientId = 0,
                                message = "Player unlocked Double Jump!"
                            });
                    }
                }
            }
            else
            {
                Debug.LogWarning(
                    $"Interactor '{interactor.name}' does not have a CoreMovement component. " +
                    "Cannot grant Double Jump ability.",
                    interactor);
            }

            yield return null;
        }

        public void CancelEffect(GameObject interactor)
        {
        }

        #endregion
    }
}