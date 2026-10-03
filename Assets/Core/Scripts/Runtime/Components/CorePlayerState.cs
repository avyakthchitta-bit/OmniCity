using System;
using UnityEngine;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Defines the lifecycle states a player can be in.
    /// </summary>
    public enum PlayerLifeState : byte
    {
        InitialSpawn,
        Eliminated,
        Respawned
    }

    /// <summary>
    /// Manages local player state such as the player's name and lifecycle state.
    /// </summary>
    public class CorePlayerState : MonoBehaviour
    {
        [Tooltip("Global ScriptableObject event raised when player state changes.")]
        [SerializeField] private PlayerStateEvent onPlayerStateChangedGlobal;

        [SerializeField] private string playerName = "Player";

        private PlayerLifeState m_LifeState = PlayerLifeState.InitialSpawn;

        public string PlayerName => playerName;

        public PlayerLifeState LifeState => m_LifeState;

        public bool IsActive =>
            LifeState == PlayerLifeState.InitialSpawn ||
            LifeState == PlayerLifeState.Respawned;

        public event Action<string> OnNameChanged;
        public event Action<PlayerLifeState> OnLifeStateChanged;

        private void Awake()
        {
            OnLifeStateChanged?.Invoke(m_LifeState);
        }

        public void SetPlayerName(string newName)
        {
            if (string.IsNullOrEmpty(newName))
                return;

            if (playerName == newName)
                return;

            playerName = newName;
            OnNameChanged?.Invoke(playerName);
        }

        public void SetLifeState(PlayerLifeState newState)
        {
            if (m_LifeState == newState)
                return;

            PlayerLifeState oldState = m_LifeState;
            m_LifeState = newState;

            OnLifeStateChanged?.Invoke(newState);

            if (onPlayerStateChangedGlobal != null)
            {
                onPlayerStateChangedGlobal.Raise(
                    new PlayerStatePayload
                    {
                        playerId = 0,
                        newState = newState,
                        oldState = oldState
                    });
            }
        }
    }
}