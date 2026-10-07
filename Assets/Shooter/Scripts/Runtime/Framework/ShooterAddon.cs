using UnityEngine;
using Blocks.Gameplay.Core;

namespace Blocks.Gameplay.Shooter
{
    /// <summary>
    /// Player addon that extends CorePlayerManager with shooter-specific functionality.
    /// Handles aiming, weapons, shooter animations, and shooter camera modes.
    /// </summary>
    public class ShooterAddon : MonoBehaviour, IPlayerAddon
    {
        #region Fields & Properties

        [Header("Shooter Components")]
        [Tooltip("Reference to the weapon HUD for displaying ammo and weapon information.")]
        [SerializeField] private WeaponHUD weaponHUD;

        [Tooltip("Controls aiming mechanics and animation rigging.")]
        [SerializeField] private AimController aimController;

        [Tooltip("Manages shooter-specific animations.")]
        [SerializeField] private ShooterAnimator shooterAnimator;

        [Tooltip("Handles weapon switching and firing logic.")]
        [SerializeField] private WeaponController weaponController;

        [Tooltip("Processes hit detection and damage application.")]
        [SerializeField] private ShooterHitProcessor shooterHitProcessor;

        [Header("Camera Mode Names")]
        [Tooltip("Name of the third-person free look camera mode.")]
        [SerializeField] private string thirdPersonFreeLookCameraName = "FreeLook";

        [Tooltip("Name of the third-person aim camera mode.")]
        [SerializeField] private string thirdPersonAimCameraName = "Aim";

        [Header("Camera Sensitivity")]
        [Tooltip("Camera look sensitivity when in free look mode.")]
        public float freeLookSensitivity = 1.0f;

        [Tooltip("Camera look sensitivity when aiming.")]
        public float aimSensitivity = 0.5f;

        [Header("Input Events (Listening)")]
        [Tooltip("Event triggered when the player toggles aim mode.")]
        [SerializeField] private GameEvent onAimToggled;

        [Header("Gameplay Events (Broadcasting)")]
        [Tooltip("Event raised when aiming state changes.")]
        [SerializeField] private BoolEvent onAimingStateChanged;

        private CorePlayerManager m_PlayerManager;
        private bool m_IsAiming;

        private static readonly int k_AnimIDIsReloading =
            Animator.StringToHash("IsReloading");

        public WeaponHUD WeaponHUD => weaponHUD;
        public AimController AimController => aimController;
        public ShooterAnimator ShooterAnimator => shooterAnimator;
        public WeaponController WeaponController => weaponController;
        public ShooterHitProcessor ShooterHitProcessor => shooterHitProcessor;

        #endregion

        #region Public Methods

        public void Initialize(CorePlayerManager playerManager)
        {
            m_PlayerManager = playerManager;

            if (weaponHUD == null)
                weaponHUD = GetComponent<WeaponHUD>();

            if (aimController == null)
                aimController = GetComponent<AimController>();

            if (shooterAnimator == null)
                shooterAnimator = GetComponent<ShooterAnimator>();

            if (weaponController == null)
                weaponController = GetComponent<WeaponController>();

            if (shooterHitProcessor == null)
                shooterHitProcessor = GetComponent<ShooterHitProcessor>();
        }

        public void OnPlayerSpawn()
        {
            onAimToggled?.RegisterListener(HandleAimToggled);
        }

        public void OnPlayerDespawn()
        {
            onAimToggled?.UnregisterListener(HandleAimToggled);
        }

        public void OnLifeStateChanged(
            PlayerLifeState previousState,
            PlayerLifeState newState)
        {
            if (newState == PlayerLifeState.Eliminated)
            {
                ResetAimState();
                OnEliminated(true);
            }
            else if (
                previousState == PlayerLifeState.Eliminated &&
                newState == PlayerLifeState.Respawned)
            {
                ResetAimState();
                OnEliminated(false);
            }
        }

        public void OnEliminated(bool isEliminated)
        {
            bool isActive = !isEliminated;

            if (weaponController != null)
            {
                weaponController.enabled = isActive;
                weaponController.SetCurrentWeaponActive(isActive);
            }
        }

        #endregion

        #region Private Methods

        private void ResetAimState()
        {
            if (m_IsAiming)
            {
                m_IsAiming = false;
                onAimingStateChanged?.Raise(false);

                if (m_PlayerManager != null &&
                    m_PlayerManager.CoreCamera != null)
                {
                    m_PlayerManager.CoreCamera.SwitchCameraMode(
                        thirdPersonFreeLookCameraName);

                    m_PlayerManager.CoreCamera.SetLookSensitivity(
                        freeLookSensitivity);

                    if (m_PlayerManager.CoreMovement != null)
                    {
                        m_PlayerManager.CoreMovement.PlayerRotationMode =
                            m_PlayerManager.CoreCamera.CurrentPlayerRotationMode;
                    }
                }
            }

            if (aimController != null)
            {
                aimController.ResetRiggingState();
            }
        }

        private void HandleAimToggled()
        {
            bool isWeaponReloading =
                weaponController != null &&
                weaponController.CurrentWeapon?.GetCurrentState() ==
                WeaponState.Reloading;

            bool isAnimatorReloading =
                shooterAnimator != null &&
                shooterAnimator.Animator.GetBool(k_AnimIDIsReloading);

            if (isWeaponReloading || isAnimatorReloading)
            {
                return;
            }

            m_IsAiming = !m_IsAiming;

            onAimingStateChanged?.Raise(m_IsAiming);

            if (m_PlayerManager.CoreCamera != null)
            {
                string modeName = m_IsAiming
                    ? thirdPersonAimCameraName
                    : thirdPersonFreeLookCameraName;

                m_PlayerManager.CoreCamera.SwitchCameraMode(modeName);

                float sensitivity = m_IsAiming
                    ? aimSensitivity
                    : freeLookSensitivity;

                m_PlayerManager.CoreCamera.SetLookSensitivity(sensitivity);
            }

            if (m_PlayerManager.CoreMovement != null &&
                m_PlayerManager.CoreCamera != null)
            {
                m_PlayerManager.CoreMovement.PlayerRotationMode =
                    m_PlayerManager.CoreCamera.CurrentPlayerRotationMode;
            }
        }

        #endregion
    }
}