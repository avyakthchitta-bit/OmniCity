using UnityEngine;
using System.Collections.Generic;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// The central orchestrator for a Core player character.
    /// Handles local input, movement, camera, stats, lifecycle, and player addons.
    /// </summary>
    public class CorePlayerManager : MonoBehaviour
    {
        #region Fields & Properties

        [Header("Core Components")]
        [SerializeField] private CoreInputHandler coreInput;
        [SerializeField] private CoreMovement coreMovement;
        [SerializeField] private CoreStatsHandler coreStats;
        [SerializeField] private CoreCameraController coreCamera;
        [SerializeField] private CorePlayerState corePlayerState;

        [Header("Lifecycle Settings")]
        [SerializeField] private bool autoHandleLifecycle;

        [Header("Input Events")]
        [SerializeField] private Vector2Event onMoveInput;
        [SerializeField] private Vector2Event onLookInput;
        [SerializeField] private GameEvent onJumpPressed;
        [SerializeField] private GameEvent onJumpReleased;
        [SerializeField] private BoolEvent onSprintStateChanged;

        [Header("Game Events")]
        [SerializeField] private StatDepletedEvent onStatDepleted;

        [Header("Sound Effects")]
        [SerializeField] private SoundDef respawnSFX;

        private List<IPlayerAddon> m_Addons = new List<IPlayerAddon>();
        private PlayerLifeState m_LastLifeState = PlayerLifeState.InitialSpawn;
        private bool m_IsMovementInputEnabled = true;
        private System.Action<Vector2> m_OnMoveInputHandler;

        public CoreInputHandler CoreInput => coreInput;
        public CoreMovement CoreMovement => coreMovement;
        public CoreStatsHandler CoreStats => coreStats;
        public CoreCameraController CoreCamera => coreCamera;
        public CorePlayerState PlayerState => corePlayerState;

        public string PlayerName =>
            corePlayerState != null
                ? corePlayerState.PlayerName
                : "Uninitialized";

        public bool AutoHandleLifecycle => autoHandleLifecycle;

        #endregion

        #region Unity Methods

        private void Awake()
        {
            CacheComponentReferences();
            InitializeAddons();
        }

        private void Start()
        {
            RegisterEventListeners();

            if (corePlayerState != null)
            {
                corePlayerState.OnNameChanged += HandlePlayerNameChanged;
                corePlayerState.OnLifeStateChanged += HandleLifeStateChanged;

                if (!string.IsNullOrEmpty(corePlayerState.PlayerName))
                {
                    HandlePlayerNameChanged(corePlayerState.PlayerName);
                }

                m_LastLifeState = corePlayerState.LifeState;
                HandleLifeStateChanged(corePlayerState.LifeState);
            }

            foreach (var addon in m_Addons)
            {
                addon.OnPlayerSpawn();
            }
        }

        private void OnDestroy()
        {
            if (corePlayerState != null)
            {
                corePlayerState.OnNameChanged -= HandlePlayerNameChanged;
                corePlayerState.OnLifeStateChanged -= HandleLifeStateChanged;
            }

            UnregisterEventListeners();

            foreach (var addon in m_Addons)
            {
                addon.OnPlayerDespawn();
            }
        }

        private void Update()
        {
            UpdateCameraTargetRotation();
            HandleSprintingStamina();
        }

        #endregion

        #region Public Methods

        public void SetMovementInputEnabled(bool isEnabled)
        {
            m_IsMovementInputEnabled = isEnabled;

            if (!isEnabled && coreMovement != null)
            {
                coreMovement.SetMoveInput(Vector2.zero);
                coreMovement.SetSprintState(false);
            }
        }

        #endregion

        #region Private Methods

        private void InitializeAddons()
        {
            if (m_Addons == null)
                m_Addons = new List<IPlayerAddon>();
            else
                m_Addons.Clear();

            GetComponents(m_Addons);

            foreach (var addon in m_Addons)
            {
                addon.Initialize(this);
            }
        }

        private void HandleSprint(bool isSprinting)
        {
            if (!m_IsMovementInputEnabled)
                return;

            if (coreStats == null || coreMovement == null)
            {
                Debug.LogWarning(
                    "[CorePlayerManager] coreStats or coreMovement is null in HandleSprint");
                return;
            }

            if (isSprinting &&
                coreStats.GetCurrentValue(StatKeys.Stamina) < 1f)
            {
                coreMovement.SetSprintState(false);
                return;
            }

            coreMovement.SetSprintState(isSprinting);
        }

        private void HandleJump()
        {
            if (!m_IsMovementInputEnabled)
                return;

            if (coreMovement == null)
            {
                Debug.LogWarning(
                    "[CorePlayerManager] coreMovement is null in HandleJump");
                return;
            }

            if (!coreMovement.IsGrounded)
                return;

            if (coreStats == null)
            {
                Debug.LogWarning(
                    "[CorePlayerManager] coreStats is null in HandleJump");
                return;
            }

            float jumpStaminaCost =
                CoreMovement.GetAbilityStaminaCost<JumpAbility>();

            if (coreStats.TryConsumeStat(
                StatKeys.Stamina,
                jumpStaminaCost,
                0))
            {
                coreMovement.PerformJump();
            }
        }

        private void HandleMoveInput(Vector2 input)
        {
            if (!m_IsMovementInputEnabled)
                return;

            if (coreMovement == null)
            {
                Debug.LogWarning(
                    "[CorePlayerManager] coreMovement is null in HandleMoveInput");
                return;
            }

            coreMovement.SetMoveInput(input);
        }

        private void HandleStatDepleted(StatDepletedPayload payload)
        {
            if (payload.statID != StatKeys.Health)
                return;

            if (!autoHandleLifecycle)
                return;

            if (corePlayerState == null)
            {
                Debug.LogWarning(
                    "[CorePlayerManager] corePlayerState is null in HandleStatDepleted");
                return;
            }

            corePlayerState.SetLifeState(PlayerLifeState.Eliminated);
        }

        private void HandleLifeStateChanged(PlayerLifeState newState)
        {
            PlayerLifeState previousState = m_LastLifeState;
            m_LastLifeState = newState;

            bool isEliminated = newState == PlayerLifeState.Eliminated;
            bool isActive = !isEliminated;

            if (coreInput != null)
                coreInput.enabled = isActive;

            if (coreCamera != null)
                coreCamera.enabled = isActive;

            SetMovementInputEnabled(isActive);

            if (coreMovement != null)
            {
                coreMovement.IsMovementEnabled = isActive;

                if (newState == PlayerLifeState.Respawned ||
                    newState == PlayerLifeState.InitialSpawn)
                {
                    if (respawnSFX != null)
                    {
                        CoreDirector.RequestAudio(respawnSFX)
                            .WithPosition(transform.position)
                            .Play();
                    }

                    coreMovement.ResetMovementForces();
                }

                var characterController =
                    coreMovement.GetComponent<CharacterController>();

                if (characterController != null)
                    characterController.enabled = isActive;
            }

            foreach (var addon in m_Addons)
            {
                addon.OnLifeStateChanged(previousState, newState);
            }
        }

        private void HandlePlayerNameChanged(string newName)
        {
            if (string.IsNullOrEmpty(newName))
                return;

            gameObject.name = newName;
        }

        private void RegisterEventListeners()
        {
            m_OnMoveInputHandler = HandleMoveInput;

            if (onMoveInput == null)
                Debug.LogWarning(
                    "[CorePlayerManager] onMoveInput is null in RegisterEventListeners");
            else
                onMoveInput.RegisterListener(m_OnMoveInputHandler);

            if (onLookInput == null || coreCamera == null)
                Debug.LogWarning(
                    "[CorePlayerManager] onLookInput or coreCamera is null in RegisterEventListeners");
            else
                onLookInput.RegisterListener(coreCamera.SetLookInput);

            if (onJumpPressed == null)
                Debug.LogWarning(
                    "[CorePlayerManager] onJumpPressed is null in RegisterEventListeners");
            else
                onJumpPressed.RegisterListener(HandleJump);

            if (onJumpReleased == null || coreMovement == null)
                Debug.LogWarning(
                    "[CorePlayerManager] onJumpReleased or coreMovement is null in RegisterEventListeners");
            else
                onJumpReleased.RegisterListener(
                    coreMovement.OnVariableActionReleased);

            if (onSprintStateChanged == null)
                Debug.LogWarning(
                    "[CorePlayerManager] onSprintStateChanged is null in RegisterEventListeners");
            else
                onSprintStateChanged.RegisterListener(HandleSprint);

            if (onStatDepleted == null)
                Debug.LogWarning(
                    "[CorePlayerManager] onStatDepleted is null in RegisterEventListeners");
            else
                onStatDepleted.RegisterListener(HandleStatDepleted);
        }

        private void UnregisterEventListeners()
        {
            if (onMoveInput != null)
                onMoveInput.UnregisterListener(m_OnMoveInputHandler);

            if (onLookInput != null && coreCamera != null)
                onLookInput.UnregisterListener(coreCamera.SetLookInput);

            if (onJumpPressed != null)
                onJumpPressed.UnregisterListener(HandleJump);

            if (onJumpReleased != null && coreMovement != null)
                onJumpReleased.UnregisterListener(
                    coreMovement.OnVariableActionReleased);

            if (onSprintStateChanged != null)
                onSprintStateChanged.UnregisterListener(HandleSprint);

            if (onStatDepleted != null)
                onStatDepleted.UnregisterListener(HandleStatDepleted);
        }

        private void CacheComponentReferences()
        {
            if (coreInput == null)
                coreInput = GetComponent<CoreInputHandler>();

            if (coreMovement == null)
                coreMovement = GetComponent<CoreMovement>();

            if (coreStats == null)
                coreStats = GetComponent<CoreStatsHandler>();

            if (coreCamera == null)
                coreCamera = GetComponent<CoreCameraController>();

            if (corePlayerState == null)
                corePlayerState = GetComponent<CorePlayerState>();
        }

        private void UpdateCameraTargetRotation()
        {
            if (coreMovement != null && coreCamera != null)
            {
                coreMovement.SetTargetRotation(
                    coreCamera.CurrentHorizontalLookAngle);
            }
        }

        private void HandleSprintingStamina()
        {
            if (coreMovement == null)
                return;

            if (coreStats == null)
                return;

            if (!coreMovement.IsSprinting ||
                coreMovement.CurrentSpeed <= 0.1f ||
                !coreMovement.IsGrounded)
                return;

            float staminaToConsume =
                CoreMovement.GetAbilityStaminaCost<WalkAbility>() *
                Time.deltaTime;

            if (!coreStats.TryConsumeStat(
                StatKeys.Stamina,
                staminaToConsume,
                0))
            {
                coreMovement.SetSprintState(false);
            }
        }

        #endregion
    }
}