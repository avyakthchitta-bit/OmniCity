
using UnityEngine;

namespace Blocks.Gameplay.Core
{
    public class DashAbility : MonoBehaviour, IMovementAbility
    {
        [Header("Dash Settings")]
        [SerializeField] private float dashForce = 12f;
        [SerializeField] private float dashCooldown = 1f;
        [SerializeField] private float staminaCost = 15f;

        private CoreMovement m_Motor;
        private float m_NextDashTime;

        public int Priority => 20;
        public float StaminaCost => staminaCost;

        public void Initialize(CoreMovement movementController)
        {
            m_Motor = movementController;
        }

        public MovementModifier Process()
        {
            return new MovementModifier();
        }

        public bool TryActivate()
        {
            if (m_Motor == null || Time.time < m_NextDashTime)
                return false;

            Vector3 direction = Camera.main != null
                ? Camera.main.transform.forward
                : transform.forward;

            if (direction.sqrMagnitude < 0.001f)
                return false;

            direction.Normalize();

            m_Motor.ApplyExternalForce(
                direction * dashForce,
                ForceMode.Impulse
            );

            m_NextDashTime = Time.time + dashCooldown;
            return true;
        }
    }
}
