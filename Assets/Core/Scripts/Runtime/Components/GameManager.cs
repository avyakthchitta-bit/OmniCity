using UnityEngine;
using System.Collections.Generic;

namespace Blocks.Gameplay.Core
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Player")]
        [SerializeField] private GameObject playerPrefab;

        [Header("Spawn Points")]
        [SerializeField] private List<Transform> spawnPoints = new List<Transform>();

        private GameObject m_LocalPlayer;
        private bool m_PlayerSpawned;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            SpawnPlayer();

            // Lock the cursor for gameplay.
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void SpawnPlayer()
        {
            if (m_PlayerSpawned)
                return;

            if (playerPrefab == null)
            {
                Debug.LogError(
                    "[GameManager] No player prefab assigned!"
                );
                return;
            }

            if (spawnPoints == null || spawnPoints.Count == 0)
            {
                Debug.LogError(
                    "[GameManager] No spawn points assigned!"
                );
                return;
            }

            // Pick a random spawn point.
            Transform spawnPoint =
                spawnPoints[Random.Range(0, spawnPoints.Count)];

            m_LocalPlayer = Instantiate(
                playerPrefab,
                spawnPoint.position,
                spawnPoint.rotation
            );

            m_PlayerSpawned = true;

            Debug.Log(
                $"[GameManager] Player spawned at {spawnPoint.name}"
            );
        }

        public GameObject GetLocalPlayer()
        {
            return m_LocalPlayer;
        }

        public Transform GetLocalPlayerTransform()
        {
            return m_LocalPlayer != null
                ? m_LocalPlayer.transform
                : null;
        }

        public void RespawnPlayer()
        {
            if (m_LocalPlayer == null || spawnPoints.Count == 0)
                return;

            Transform spawnPoint =
                spawnPoints[Random.Range(0, spawnPoints.Count)];

            m_LocalPlayer.transform.SetPositionAndRotation(
                spawnPoint.position,
                spawnPoint.rotation
            );

            Debug.Log(
                $"[GameManager] Player respawned at {spawnPoint.name}"
            );
        }
    }
}

