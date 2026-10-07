
using System;
using UnityEngine;
using System.Collections.Generic;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Handles player movement, including walking, sprinting, jumping, and gravity.
    /// This component uses a CharacterController and modular movement abilities.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class CoreMovement : MonoBehaviour
    {
        #region Enums

        public enum CouplingMode
        {
            Coupled,
            CoupledWhenMoving,
            Decoupled
        }

        public enum MovementDirectionMode
        {
            World,
            CharacterRelative,
            CameraRelative
        }

        #endregion

        #region Fields & Properties

        [Header("Movement Settings")]
        [Tooltip("Determines the directional context for movement input.")]
        public MovementDirectionMode directionMode = MovementDirectionMode.CameraRelative;

        [Tooltip("Base movement speed.")]
        public float moveSpeed = 4.0f;

        [Tooltip("Movement speed when sprinting.")]
        public float sprintSpeed = 6.0f;

        [Tooltip("How quickly the character rotates to the target direction. Lower values mean faster rotation.")]
        public float rotationSmoothTime = 0.1f;

        [Tooltip("The rate of acceleration and deceleration.")]
        public float speedChangeRate = 10.0f;

        [Tooltip("How quickly external forces decay.")]
        public float forceDecayRate = 5f;

        [Header("Gravity and Jump Settings")]
        public float gravity = -15.0f;
        public float jumpHeight = 1.2f;

        [Header("Ground Check")]
        [SerializeField] private float groundedOffset = -0.14f;
        [SerializeField] private float groundedRadius = 0.28f;
        public LayerMask groundLayers;

        [Header("Slope Handling")]
        [SerializeField] private float slopeForce = 5f;
        [SerializeField] private float slopeForceRayLength = 1.5f;

        [Header("Movement Control")]
        [SerializeField] private bool isMovementEnabled = true;
        [SerializeField] private Transform rotationTransform;

        public Func<Quaternion> RotationOverride { get; set; }

        public Func<Vector3, Vector3> FinalMoveCalculationOverride { get; set; }

        public bool IsGrounded { get; private set; }

        public bool IsOnSlope { get; private set; }

        public float TimeLastGrounded => m_TimeLastGrounded;

        public float TimeSinceLanded => m_TimeSinceLanded;

        public float VerticalVelocity => m_VerticalVelocity;

        public float CurrentSpeed =>
            new Vector3(m_ArealVelocity.x, 0.0f, m_ArealVelocity.z).magnitude;

        public float InputMagnitude => MoveInput.magnitude;

        public Vector2 MoveInput { get; private set; }

        public bool IsSprinting { get; private set; }

        public float TargetRotationY { get; private set; }

        public bool JumpRequested { get; private set; }

        public bool IsMovementEnabled
        {
            get => isMovementEnabled;
            set => isMovementEnabled = value;
        }

        public bool VariableActionReleasedThisFrame { get; private set; }

        public CouplingMode PlayerRotationMode { get; set; } =
            CouplingMode.Decoupled;

        public Vector3 LastMoveDirection { get; private set; }

        public Transform RotationTransform => rotationTransform;

        private CharacterController m_CharacterController;
        private List<IMovementAbility> m_Abilities =
            new List<IMovementAbility>();

        private float m_VerticalVelocity;
        private float m_RotationVelocity;
        private Vector3 m_ArealVelocity;
        private readonly float m_TerminalVelocity = 53.0f;
        private Vector3 m_ExternalForce;
        private float m_TimeLastGrounded;
        private float m_TimeSinceLanded;
        private float m_InitialGravity;

        #endregion

        #region Events

        public event Action<Vector3> OnLanded;

        public event Action<bool> OnGroundedStateChanged;

        #endregion

        #region Unity Methods

        private void Awake()
        {
            m_CharacterController = GetComponent<CharacterController>();

            m_InitialGravity = gravity;

            m_Abilities =
                new List<IMovementAbility>(
                    GetComponents<IMovementAbility>()
                );

            foreach (var ability in m_Abilities)
            {
                ability.Initialize(this);
            }

            m_Abilities.Sort(
                (a, b) => b.Priority.CompareTo(a.Priority)
            );
        }

        private void Update()
        {
            if (IsGrounded)
            {
                m_TimeSinceLanded += Time.deltaTime;
            }

            if (!isMovementEnabled)
            {
                GroundedCheck();

                if (m_VerticalVelocity < m_TerminalVelocity)
                {
                    m_VerticalVelocity += gravity * Time.deltaTime;
                }

                if (m_CharacterController.enabled)
                {
                    Vector3 verticalMove =
                        new Vector3(
                            0,
                            m_VerticalVelocity,
                            0
                        ) * Time.deltaTime;

                    if (!float.IsNaN(verticalMove.x) &&
                        !float.IsNaN(verticalMove.y) &&
                        !float.IsNaN(verticalMove.z))
                    {
                        m_CharacterController.Move(verticalMove);
                    }
                }

                return;
            }

            GroundedCheck();

            ProcessAbilities();
        }

        private void LateUpdate()
        {
            JumpRequested = false;
            VariableActionReleasedThisFrame = false;
        }

        #endregion

        #region Public Methods

        public void SetMoveInput(Vector2 direction)
        {
            MoveInput = direction;
        }

        public void SetSprintState(bool isSprinting)
        {
            IsSprinting = isSprinting;
        }

        public void SetTargetRotation(float yAngle)
        {
            TargetRotationY = yAngle;
        }

        public void PerformJump()
        {
            JumpRequested = true;
        }

        public void SetVerticalVelocity(float newVerticalVelocity)
        {
            m_VerticalVelocity = newVerticalVelocity;
        }

        public void ApplyExternalForce(
            Vector3 force,
            ForceMode forceMode
        )
        {
            if (forceMode == ForceMode.Impulse)
            {
                m_ExternalForce += force;
            }
            else
            {
                m_ExternalForce += force * Time.deltaTime;
            }
        }

        public void SetPosition(
            Vector3 position,
            bool teleport = true
        )
        {
            if (m_CharacterController != null &&
                m_CharacterController.enabled)
            {
                m_CharacterController.enabled = false;

                transform.position = position;

                m_CharacterController.enabled = true;
            }
            else
            {
                transform.position = position;
            }
        }

        public void AddAbility(
            IMovementAbility newAbility
        )
        {
            if (newAbility == null ||
                m_Abilities.Contains(newAbility))
            {
                return;
            }

            m_Abilities.Add(newAbility);

            newAbility.Initialize(this);

            m_Abilities.Sort(
                (a, b) => b.Priority.CompareTo(a.Priority)
            );
        }

        public bool HasAbility<T>()
            where T : class, IMovementAbility
        {
            for (int i = 0; i < m_Abilities.Count; i++)
            {
                if (m_Abilities[i] is T)
                {
                    return true;
                }
            }

            return false;
        }

        public bool RemoveAbility<T>()
            where T : class, IMovementAbility
        {
            IMovementAbility abilityToRemove = null;

            for (int i = 0; i < m_Abilities.Count; i++)
            {
                if (m_Abilities[i] is T)
                {
                    abilityToRemove = m_Abilities[i];
                    break;
                }
            }

            if (abilityToRemove != null)
            {
                m_Abilities.Remove(abilityToRemove);

                if (abilityToRemove is MonoBehaviour component)
                {
                    Destroy(component);
                }

                return true;
            }

            return false;
        }

        public bool TryActivateAbility<T>()
            where T : class, IMovementAbility
        {
            foreach (var ability in m_Abilities)
            {
                if (ability is T typedAbility)
                {
                    return typedAbility.TryActivate();
                }
            }

            return false;
        }

        public float GetAbilityStaminaCost<T>()
            where T : class, IMovementAbility
        {
            foreach (var ability in m_Abilities)
            {
                if (ability is T typedAbility)
                {
                    return typedAbility.StaminaCost;
                }
            }

            return 0f;
        }

        public void SetDirectionMode(
            MovementDirectionMode mode
        )
        {
            directionMode = mode;
        }

        public void OnVariableActionReleased()
        {
            VariableActionReleasedThisFrame = true;
        }

        public void SetRotationTransform(
            Transform targetTransform
        )
        {
            rotationTransform = targetTransform;
        }

        public void ResetMovementForces()
        {
            m_VerticalVelocity = 0f;
            m_ExternalForce = Vector3.zero;
            m_ArealVelocity = Vector3.zero;
            gravity = m_InitialGravity;
        }

        #endregion

        #region Private Methods

        private void ProcessAbilities()
        {
            var finalModifier = new MovementModifier();

            foreach (var ability in m_Abilities)
            {
                var modifier = ability.Process();

                finalModifier.ArealVelocity +=
                    modifier.ArealVelocity;

                finalModifier.OverrideGravity |=
                    modifier.OverrideGravity;
            }

            if (m_ExternalForce.magnitude > 0.01f)
            {
                finalModifier.ArealVelocity += m_ExternalForce;

                m_ExternalForce =
                    Vector3.Lerp(
                        m_ExternalForce,
                        Vector3.zero,
                        forceDecayRate * Time.deltaTime
                    );
            }
            else
            {
                m_ExternalForce = Vector3.zero;
            }

            m_ArealVelocity =
                finalModifier.ArealVelocity;

            var horizontalVelocity =
                new Vector3(
                    -m_ArealVelocity.x,
                    0,
                    -m_ArealVelocity.z
                );

            if (horizontalVelocity.sqrMagnitude > 0.01f)
            {
                LastMoveDirection =
                    horizontalVelocity.normalized;
            }

            if (IsGrounded &&
                !finalModifier.OverrideGravity)
            {
                if (m_VerticalVelocity < 0.0f)
                {
                    m_VerticalVelocity = -2f;
                }
            }

            if (!finalModifier.OverrideGravity)
            {
                if (m_VerticalVelocity < m_TerminalVelocity)
                {
                    m_VerticalVelocity +=
                        gravity * Time.deltaTime;
                }
            }

            ApplyRotation(
                finalModifier.ArealVelocity
            );

            Vector3 movement =
                finalModifier.ArealVelocity;

            movement.y = m_VerticalVelocity;

            IsOnSlope = OnSlope();

            if (IsOnSlope &&
                (MoveInput.x != 0 ||
                 MoveInput.y != 0))
            {
                movement +=
                    Vector3.down * slopeForce;
            }

            Vector3 finalMovementVector =
                movement * Time.deltaTime;

            if (FinalMoveCalculationOverride != null)
            {
                finalMovementVector =
                    FinalMoveCalculationOverride(
                        finalMovementVector
                    );
            }

            if (m_CharacterController != null &&
                m_CharacterController.enabled)
            {
                if (!float.IsNaN(finalMovementVector.x) &&
                    !float.IsNaN(finalMovementVector.y) &&
                    !float.IsNaN(finalMovementVector.z))
                {
                    m_CharacterController.Move(
                        finalMovementVector
                    );
                }
            }
        }

        private void ApplyRotation(
            Vector3 horizontalVelocity
        )
        {
            Transform targetTransform =
                rotationTransform != null
                    ? rotationTransform
                    : transform;

            if (RotationOverride != null)
            {
                targetTransform.rotation =
                    RotationOverride();

                return;
            }

            bool isCoupled =
                PlayerRotationMode ==
                    CouplingMode.Coupled ||
                (
                    PlayerRotationMode ==
                        CouplingMode.CoupledWhenMoving &&
                    MoveInput != Vector2.zero
                );

            if (isCoupled)
            {
                float rotation =
                    Mathf.SmoothDampAngle(
                        targetTransform.eulerAngles.y,
                        TargetRotationY,
                        ref m_RotationVelocity,
                        rotationSmoothTime
                    );

                targetTransform.rotation =
                    Quaternion.Euler(
                        0.0f,
                        rotation,
                        0.0f
                    );
            }
            else if (horizontalVelocity.magnitude > 0.1f)
            {
                float targetAngle =
                    Mathf.Atan2(
                        horizontalVelocity.x,
                        horizontalVelocity.z
                    ) * Mathf.Rad2Deg;

                float rotation =
                    Mathf.SmoothDampAngle(
                        targetTransform.eulerAngles.y,
                        targetAngle,
                        ref m_RotationVelocity,
                        rotationSmoothTime
                    );

                targetTransform.rotation =
                    Quaternion.Euler(
                        0.0f,
                        rotation,
                        0.0f
                    );
            }
        }

        private void GroundedCheck()
        {
            Vector3 spherePosition =
                new Vector3(
                    transform.position.x,
                    transform.position.y + groundedOffset,
                    transform.position.z
                );

            bool wasGrounded = IsGrounded;

            IsGrounded =
                Physics.CheckSphere(
                    spherePosition,
                    groundedRadius,
                    groundLayers,
                    QueryTriggerInteraction.Ignore
                );

            if (IsGrounded)
            {
                m_TimeLastGrounded = Time.time;
            }

            if (IsGrounded != wasGrounded)
            {
                OnGroundedStateChanged?.Invoke(
                    IsGrounded
                );

                if (IsGrounded &&
                    m_VerticalVelocity < -2.0f)
                {
                    m_TimeSinceLanded = 0f;

                    OnLanded?.Invoke(
                        new Vector3(
                            0,
                            m_VerticalVelocity,
                            0
                        )
                    );
                }
            }
        }

        private bool OnSlope()
        {
            if (JumpRequested ||
                m_VerticalVelocity > 0)
            {
                return false;
            }

            RaycastHit hit;

            Vector3 rayStart =
                transform.position;

            float rayLength =
                m_CharacterController.height /
                2 *
                slopeForceRayLength;

            if (Physics.Raycast(
                rayStart,
                Vector3.down,
                out hit,
                rayLength,
                groundLayers,
                QueryTriggerInteraction.Ignore))
            {
                if (hit.normal != Vector3.up)
                {
                    return true;
                }
            }

            return false;
        }

        #endregion
    }
}

