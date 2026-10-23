using UnityEngine;
using System.Collections.Generic;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Manages a character's stats (e.g., Health, Stamina) based on a <see cref="StatsConfig"/> asset.
    /// It handles stat initialization, modification, consumption, and regeneration.
    /// </summary>
    public class CoreStatsHandler : MonoBehaviour
    {
        #region Fields & Properties

        [Header("Configuration")]
        [Tooltip("The ScriptableObject defining the stats for this character.")]
        [SerializeField] private StatsConfig statsConfig;

        [Header("Broadcasting on")]
        [Tooltip("GameEvent raised when any stat's value changes (only for stats with broadcastNetworkedEvents enabled).")]
        [SerializeField] private StatChangeEvent onStatChangedEvent;
        [Tooltip("GameEvent raised when any stat reaches zero (only for stats with broadcastNetworkedEvents enabled).")]
        [SerializeField] private StatDepletedEvent onStatDepletedEvent;

        /// <summary>
        /// Gets a value indicating whether the character is currently alive.
        /// The character is considered alive if their primary stat (e.g., Health) is greater than zero.
        /// </summary>
        public bool IsAlive { get; private set; } = true;

        private readonly List<RuntimeStat> m_RuntimeStats = new List<RuntimeStat>();

        // Tracks the last time each stat was consumed to enforce regeneration delays
        private readonly Dictionary<int, float> m_LastStatUseTime = new Dictionary<int, float>();

        // Caches stat definitions by hash for fast lookup without config access
        private readonly Dictionary<int, StatDefinition> m_StatDefinitions = new Dictionary<int, StatDefinition>();

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (statsConfig == null)
            {
                Debug.LogError($"[CoreStatsHandler] StatsConfig not assigned", this);
                enabled = false;
                return;
            }

            foreach (var def in statsConfig.stats)
            {
                m_StatDefinitions[Animator.StringToHash(def.statName)] = def;
            }

            InitializeRuntimeStats();
            UpdateAliveState();
        }

        private void Start()
        {
            foreach (var stat in m_RuntimeStats)
            {
                BroadcastStatChange(stat);
            }
        }

        private void Update()
        {
            if (!IsAlive) return;

            HandleRegeneration();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Modifies a stat by a given amount.
        /// </summary>
        /// <param name="statHash">The hash of the stat to modify (use StatKeys).</param>
        /// <param name="amount">The amount to add or subtract.</param>
        /// <param name="sourcePlayerId">Who caused this change.</param>
        /// <param name="sourceType">What type of modification this is.</param>
        public void ModifyStat(int statHash, float amount, ulong sourcePlayerId = 0, ModificationSource sourceType = ModificationSource.Direct)
        {
            int statIndex = FindStatIndex(statHash);

            if (statIndex != -1)
            {
                ModifyStat(statIndex, amount, true, sourcePlayerId, sourceType);
            }
            else
            {
                Debug.LogWarning($"[CoreStatsHandler] ModifyStat failed: Stat with hash {statHash} not found on {gameObject.name}", this);
            }
        }

        /// <summary>
        /// Attempts to consume a certain amount from a stat.
        /// </summary>
        /// <param name="statHash">The hash of the stat to consume (use StatKeys).</param>
        /// <param name="amount">The amount to consume.</param>
        /// <param name="sourcePlayerId">Who caused this consumption.</param>
        /// <returns>True if the stat had enough value to consume, false otherwise.</returns>
        public bool TryConsumeStat(int statHash, float amount, ulong sourcePlayerId = 0)
        {
            int statIndex = FindStatIndex(statHash);

            if (statIndex == -1)
            {
                Debug.LogWarning($"[CoreStatsHandler] TryConsumeStat failed: Stat with hash {statHash} not found on {gameObject.name}", this);
                return false;
            }

            if (amount <= 0f)
            {
                return true;
            }

            if (m_RuntimeStats[statIndex].CurrentValue >= amount)
            {
                ModifyStat(statIndex, -amount, true, sourcePlayerId, ModificationSource.Consumption);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Attempts to consume a certain amount from a stat.
        /// </summary>
        /// <param name="statName">The name of the stat to consume.</param>
        /// <param name="amount">The amount to consume.</param>
        /// <param name="sourcePlayerId">Who caused this consumption.</param>
        /// <returns>True if the stat had enough value to consume, false otherwise.</returns>
        [System.Obsolete("Use TryConsumeStat(int statHash, ...) instead with StatKeys to avoid string hashing overhead")]
        public bool TryConsumeStat(string statName, float amount, ulong sourcePlayerId = 0)
        {
            return TryConsumeStat(Animator.StringToHash(statName), amount, sourcePlayerId);
        }

        /// <summary>
        /// Gets an enumerable collection of all stats with their current and max values.
        /// </summary>
        /// <returns>An IEnumerable of tuples containing the stat name, current value, and max value.</returns>
        public IEnumerable<(string name, float current, float max)> GetAllStats()
        {
            foreach (var runtimeStat in m_RuntimeStats)
            {
                if (m_StatDefinitions.TryGetValue(runtimeStat.StatHash, out var def))
                {
                    yield return (def.statName, runtimeStat.CurrentValue, def.maxValue);
                }
            }
        }

        /// <summary>
        /// Gets the current value of a specific stat.
        /// </summary>
        /// <param name="statHash">The hash of the stat (use StatKeys).</param>
        /// <returns>The current value, or 0 if the stat is not found.</returns>
        public float GetCurrentValue(int statHash)
        {
            int index = FindStatIndex(statHash);
            if (index != -1)
            {
                return m_RuntimeStats[index].CurrentValue;
            }

            Debug.LogWarning($"[CoreStatsHandler] GetCurrentValue: Stat with hash {statHash} not found on {gameObject.name}, returning 0", this);
            return 0;
        }

        /// <summary>
        /// Gets the maximum value of a specific stat.
        /// </summary>
        /// <param name="statHash">The hash of the stat (use StatKeys).</param>
        /// <returns>The maximum value, or 0 if the stat is not found.</returns>
        public float GetMaxValue(int statHash)
        {
            if (m_StatDefinitions.TryGetValue(statHash, out var def))
            {
                return def.maxValue;
            }
            return 0f;
        }

        #endregion

        #region Private Methods

        private void InitializeRuntimeStats()
        {
            m_RuntimeStats.Clear();

            if (statsConfig.stats == null || statsConfig.stats.Count == 0)
            {
                Debug.LogWarning($"[CoreStatsHandler] No stats defined in StatsConfig on {gameObject.name}", this);
                return;
            }

            foreach (var def in statsConfig.stats)
            {
                m_RuntimeStats.Add(new RuntimeStat
                {
                    StatHash = Animator.StringToHash(def.statName),
                    CurrentValue = def.startingValue
                });
            }
        }

        /// <summary>
        /// Handles the regeneration of stats over time, respecting regeneration delays.
        /// </summary>
        private void HandleRegeneration()
        {
            if (m_RuntimeStats.Count == 0)
            {
                return;
            }

            for (int i = 0; i < m_RuntimeStats.Count; i++)
            {
                var stat = m_RuntimeStats[i];
                if (m_StatDefinitions.TryGetValue(stat.StatHash, out var def))
                {
                    if (def.regenRate > 0 && stat.CurrentValue < def.maxValue)
                    {
                        if (!m_LastStatUseTime.ContainsKey(stat.StatHash) ||
                            Time.time - m_LastStatUseTime[stat.StatHash] > def.regenDelay)
                        {
                            ModifyStat(i, def.regenRate * Time.deltaTime, false, 0, ModificationSource.Regeneration);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Updates the character's alive state based on the primary stat.
        /// </summary>
        private void UpdateAliveState()
        {
            if (statsConfig == null)
            {
                IsAlive = true;
                return;
            }

            int index = FindStatIndex(statsConfig.PrimaryStatHash);
            if (index == -1)
            {
                IsAlive = true;
                return;
            }

            IsAlive = m_RuntimeStats[index].CurrentValue > 0;
        }

        /// <summary>
        /// Raises the appropriate events with a payload containing the updated stat information.
        /// </summary>
        /// <param name="stat">The stat that has changed.</param>
        /// <param name="sourcePlayerId">The player who caused this stat change.</param>
        /// <param name="sourceType">The type of modification that occurred.</param>
        private void BroadcastStatChange(RuntimeStat stat, ulong sourcePlayerId = 0, ModificationSource sourceType = ModificationSource.Unknown)
        {
            if (m_StatDefinitions.TryGetValue(stat.StatHash, out var def))
            {
                if (def.eventFlags.HasFlag(StatEventFlags.OnChanged))
                {
                    var statPayload = new StatChangePayload
                    {
                        targetPlayerId = 0,
                        sourcePlayerId = sourcePlayerId,
                        sourceType = sourceType,
                        statName = def.statName,
                        statID = stat.StatHash,
                        currentValue = stat.CurrentValue,
                        maxValue = def.maxValue
                    };
                    onStatChangedEvent?.Raise(statPayload);
                }

                if (def.eventFlags.HasFlag(StatEventFlags.OnDepleted))
                {
                    if (stat.CurrentValue <= 0)
                    {
                        var depletedPayload = new StatDepletedPayload
                        {
                            playerId = 0,
                            statName = def.statName,
                            statID = stat.StatHash
                        };
                        onStatDepletedEvent?.Raise(depletedPayload);
                    }
                }
            }
            else
            {
                Debug.LogWarning($"[CoreStatsHandler] BroadcastStatChange: No definition found for stat hash {stat.StatHash} on {gameObject.name}", this);
            }
        }

        /// <summary>
        /// Internal method to modify a stat by its index.
        /// Clamps the value to the stat's min and max values.
        /// </summary>
        /// <param name="index">The index of the stat.</param>
        /// <param name="amount">The amount to add or subtract.</param>
        /// <param name="recordUseTime">Whether to record the use time for regeneration delay tracking.</param>
        /// <param name="sourcePlayerId">The player who caused this change.</param>
        /// <param name="sourceType">The type of modification.</param>
        private void ModifyStat(int index, float amount, bool recordUseTime, ulong sourcePlayerId = 0, ModificationSource sourceType = ModificationSource.Unknown)
        {
            if (index < 0 || index >= m_RuntimeStats.Count)
            {
                Debug.LogError($"[CoreStatsHandler] ModifyStat: Index {index} out of bounds (count: {m_RuntimeStats.Count}) on {gameObject.name}", this);
                return;
            }

            var stat = m_RuntimeStats[index];
            if (m_StatDefinitions.TryGetValue(stat.StatHash, out var def))
            {
                stat.CurrentValue = Mathf.Clamp(stat.CurrentValue + amount, def.minValue, def.maxValue);
                stat.SourcePlayerId = sourcePlayerId;
                stat.SourceType = sourceType;

                m_RuntimeStats[index] = stat;

                if (recordUseTime && amount < 0)
                {
                    m_LastStatUseTime[stat.StatHash] = Time.time;
                }

                UpdateAliveState();
                BroadcastStatChange(stat, sourcePlayerId, sourceType);
            }
            else
            {
                Debug.LogWarning($"[CoreStatsHandler] ModifyStat: No definition found for stat hash {stat.StatHash} on {gameObject.name}", this);
            }
        }

        /// <summary>
        /// Finds the index of a stat using its hash.
        /// </summary>
        /// <param name="statHash">The hash of the stat to find.</param>
        /// <returns>The index of the stat, or -1 if not found.</returns>
        private int FindStatIndex(int statHash)
        {
            for (int i = 0; i < m_RuntimeStats.Count; i++)
            {
                if (m_RuntimeStats[i].StatHash == statHash)
                {
                    return i;
                }
            }
            return -1;
        }

        #endregion
    }
}
