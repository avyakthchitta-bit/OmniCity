
using UnityEngine;
using Blocks.Gameplay.Core;

namespace Blocks.Gameplay.Platformer
{
    /// <summary>
    /// Handles double, triple, and quadruple jumping.
    /// </summary>
    public class DoubleJumpAbility : MonoBehaviour, IMovementAbility
    {
        [Header("Jump Settings")]
        [SerializeField] private float doubleJumpHeight = 2f;

        [Header("Extra Jump Options")]
        [SerializeField] private bool enableTripleJump = false;
        [SerializeField] private bool enableQuadrupleJump = false;

        private CoreMovement m_Motor;
        private int m_AirJumpsUsed;

        public int Priority => 15;
        public float StaminaCost => 10f;

        private int MaxAirJumps
        {
            get
            {
                if (enableQuadrupleJump)
                    return 3;

                if (enableTripleJump)
                    return 2;

                return 1;
            }
        }

        public void Initialize(CoreMovement motor)
        {
            if (m_Motor != null)
                m_Motor.OnGroundedStateChanged -= HandleGroundedStateChanged;

            m_Motor = motor;
            m_AirJumpsUsed = 0;

            if (m_Motor != null)
                m_Motor.OnGroundedStateChanged += HandleGroundedStateChanged;
        }

        public MovementModifier Process()
        {
            if (m_Motor == null)
                return new MovementModifier();

            if (m_Motor.JumpRequested &&
                !m_Motor.IsGrounded &&
                m_AirJumpsUsed < MaxAirJumps)
            {
                m_AirJumpsUsed++;

                float jumpVelocity = Mathf.Sqrt(
                    doubleJumpHeight * -2f * m_Motor.gravity
                );

                m_Motor.SetVerticalVelocity(jumpVelocity);

                Debug.Log(
                    $"Air jump {m_AirJumpsUsed} of {MaxAirJumps}!"
                );

                return new MovementModifier
                {
                    OverrideGravity = true
                };
            }

            return new MovementModifier();
        }

        public bool TryActivate()
        {
            return false;
        }

        private void HandleGroundedStateChanged(bool isGrounded)
        {
            if (isGrounded)
                m_AirJumpsUsed = 0;
        }

        private void OnDestroy()
        {
            if (m_Motor != null)
                m_Motor.OnGroundedStateChanged -= HandleGroundedStateChanged;
        }
    }
}
