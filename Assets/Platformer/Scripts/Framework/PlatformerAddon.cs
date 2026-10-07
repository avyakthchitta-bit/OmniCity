using UnityEngine;
using Blocks.Gameplay.Core;

namespace Blocks.Gameplay.Platformer
{
    /// <summary>
    /// Manages platformer-specific player functionality including jump input
    /// handling and stamina consumption.
    /// </summary>
    public class PlatformerAddon : MonoBehaviour, IPlayerAddon
    {
        #region Fields & Properties

        [Header("Platformer Components")]
        [Tooltip("The locomotion ability component that handles platformer movement parameters.")]
        [SerializeField] private PlatformerLocomotionAbility locomotionAbility;

        [Header("Input Events")]
        [Tooltip("Game event triggered when the jump button is pressed.")]
        [SerializeField] private GameEvent onJumpPressed;

        private CorePlayerManager m_PlayerManager;

        public PlatformerLocomotionAbility LocomotionAbility =>
            locomotionAbility;

        #endregion

        #region Public Methods

        public void Initialize(CorePlayerManager playerManager)
        {
            m_PlayerManager = playerManager;

            if (locomotionAbility == null)
            {
                locomotionAbility =
                    GetComponentInChildren<PlatformerLocomotionAbility>();
            }
        }

        public void OnPlayerSpawn()
        {
            onJumpPressed?.RegisterListener(HandleJump);
        }

        public void OnPlayerDespawn()
        {
            onJumpPressed?.UnregisterListener(HandleJump);
        }

        public void OnLifeStateChanged(
            PlayerLifeState previousState,
            PlayerLifeState newState)
        {
            if (newState == PlayerLifeState.Eliminated)
            {
                if (locomotionAbility != null)
                {
                    locomotionAbility.enabled = false;
                }
            }
            else if (
                previousState == PlayerLifeState.Eliminated &&
                newState == PlayerLifeState.Respawned)
            {
                if (locomotionAbility != null)
                {
                    locomotionAbility.enabled = true;
                }
            }
        }

        #endregion

        #region Unity Methods

        private void Update()
        {
            HandleSprintingStamina();
        }

        #endregion

        #region Private Methods

        private void HandleJump()
        {
            if (m_PlayerManager?.CoreMovement == null ||
                m_PlayerManager?.CoreStats == null)
            {
                return;
            }

            if (locomotionAbility == null)
            {
                return;
            }

            var movement = m_PlayerManager.CoreMovement;

            if (!movement.IsGrounded)
            {
                return;
            }

            float jumpStaminaCost =
                locomotionAbility.JumpStaminaCost;

            if (m_PlayerManager.CoreStats.TryConsumeStat(
                StatKeys.Stamina,
                jumpStaminaCost,
                0))
            {
                movement.PerformJump();
            }
        }

        private void HandleSprintingStamina()
        {
            if (m_PlayerManager?.CoreMovement == null ||
                m_PlayerManager?.CoreStats == null)
            {
                return;
            }

            if (locomotionAbility == null)
            {
                return;
            }

            var movement = m_PlayerManager.CoreMovement;

            if (!movement.IsSprinting ||
                movement.CurrentSpeed <= 0.1f ||
                !movement.IsGrounded)
            {
                return;
            }

            float staminaCost =
                locomotionAbility.StaminaCost * Time.deltaTime;

            if (!m_PlayerManager.CoreStats.TryConsumeStat(
                StatKeys.Stamina,
                staminaCost,
                0))
            {
                movement.SetSprintState(false);
            }
        }

        #endregion
    }
}