using TMPro;
using UnityEngine;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// An IPlayerAddon that displays the player's name above their head.
    /// </summary>
    public class NamePlateAddon : MonoBehaviour, IPlayerAddon
    {
        #region Fields & Properties

        [Header("Name Display")]
        [Tooltip("World space canvas that displays the player's name above their head.")]
        [SerializeField] private Canvas nameDisplayCanvas;

        [Tooltip("TextMeshPro component for displaying the player name.")]
        [SerializeField] private TextMeshProUGUI nameDisplayText;

        private CorePlayerManager m_PlayerManager;
        private Camera m_MainCamera;

        #endregion

        #region Public Methods

        /// <summary>
        /// Initializes the nameplate addon and subscribes to name change events.
        /// </summary>
        public void Initialize(CorePlayerManager playerManager)
        {
            m_PlayerManager = playerManager;

            if (m_PlayerManager.PlayerState != null)
            {
                m_PlayerManager.PlayerState.OnNameChanged += UpdateNameDisplay;
            }
        }

        /// <summary>
        /// Called when the player spawns.
        /// </summary>
        public void OnPlayerSpawn()
        {
            m_MainCamera = Camera.main;

            // Single-player: show the player's own nameplate.
            if (nameDisplayCanvas != null)
            {
                nameDisplayCanvas.gameObject.SetActive(true);
            }

            UpdateNameDisplay(m_PlayerManager.PlayerName);
        }

        /// <summary>
        /// Called when the player despawns.
        /// </summary>
        public void OnPlayerDespawn()
        {
            if (m_PlayerManager != null &&
                m_PlayerManager.PlayerState != null)
            {
                m_PlayerManager.PlayerState.OnNameChanged -= UpdateNameDisplay;
            }
        }

        /// <summary>
        /// Called when the player's life state changes.
        /// </summary>
        public void OnLifeStateChanged(
            PlayerLifeState previousState,
            PlayerLifeState newState)
        {
            if (nameDisplayCanvas != null)
            {
                nameDisplayCanvas.gameObject.SetActive(
                    newState != PlayerLifeState.Eliminated);
            }
        }

        #endregion

        #region Unity Methods

        private void LateUpdate()
        {
            UpdateNameDisplayRotation();
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Updates the displayed name text.
        /// </summary>
        private void UpdateNameDisplay(string newName)
        {
            if (nameDisplayText != null)
            {
                nameDisplayText.text = newName;
            }
        }

        /// <summary>
        /// Rotates the nameplate canvas to always face the camera.
        /// </summary>
        private void UpdateNameDisplayRotation()
        {
            if (nameDisplayCanvas == null ||
                !nameDisplayCanvas.gameObject.activeSelf)
            {
                return;
            }

            if (m_MainCamera == null)
            {
                m_MainCamera = Camera.main;
            }

            if (m_MainCamera == null)
            {
                return;
            }

            nameDisplayCanvas.transform.LookAt(m_MainCamera.transform);
            nameDisplayCanvas.transform.Rotate(0, 180, 0);
        }

        #endregion
    }
}