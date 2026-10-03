using UnityEngine;
using System.Collections.Generic;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Represents a set of materials that can be applied to different parts of a player model.
    /// </summary>
    [System.Serializable]
    public struct PlayerMaterialSet
    {
        public string name;
        public Material body;
        public Material arms;
        public Material legs;
    }

    /// <summary>
    /// Manages player visual elements including material customization, ragdoll physics, and elimination effects.
    /// </summary>
    public class VisualsAddon : MonoBehaviour, IPlayerAddon
    {
        #region Fields & Properties

        [Header("Component Dependencies")]
        [Tooltip("A direct reference to the player's animator.")]
        [SerializeField] private Animator playerAnimator;

        

        [Header("Visual Customization")]
        [Tooltip("The Renderer (MeshRenderer or SkinnedMeshRenderer) to apply materials to.")]
        [SerializeField] private Renderer targetRenderer;

        [Tooltip("Define the material sets here.")]
        [SerializeField] private List<PlayerMaterialSet> materialSets;

        [Header("Visual Effects")]
        [Tooltip("Visual effect prefab instantiated when the player is eliminated.")]
        [SerializeField] private GameObject eliminatedVFX;

        [Tooltip("Sound definition played when the player is eliminated.")]
        [SerializeField] private SoundDef soundDefPlayerEliminated;

        [Header("Ragdoll Settings")]
        [Tooltip("When enabled, the player model will turn into a ragdoll upon elimination.")]
        [SerializeField] private bool useRagdollOnElimination = true;

        [Tooltip("The root GameObject containing all the ragdoll's Rigidbodies and Colliders.")]
        [SerializeField] private GameObject ragdollRoot;

        private Rigidbody[] m_RagdollRigidbodies;
        private Collider[] m_RagdollColliders;
        private CorePlayerManager m_PlayerManager;

        #endregion

        #region Public Methods

        /// <summary>
        /// Initializes the VisualsAddon with required component references and sets up ragdoll physics.
        /// </summary>
        public void Initialize(CorePlayerManager playerManager)
        {
            m_PlayerManager = playerManager;

            if (playerAnimator == null)
            {
                playerAnimator = GetComponentInChildren<Animator>();
            }

            if (targetRenderer == null)
            {
                targetRenderer = GetComponentInChildren<Renderer>();
            }

            if (useRagdollOnElimination && ragdollRoot != null)
            {
                m_RagdollRigidbodies = ragdollRoot.GetComponentsInChildren<Rigidbody>();
                m_RagdollColliders = ragdollRoot.GetComponentsInChildren<Collider>();

                foreach (var rb in m_RagdollRigidbodies)
                {
                    rb.isKinematic = true;
                }
            }
        }

        /// <summary>
        /// Called when the player spawns.
        /// </summary>
        public void OnPlayerSpawn()
        {
            ApplyMaterialSet();
        }

        /// <summary>
        /// Called when the player despawns.
        /// </summary>
        public void OnPlayerDespawn()
        {
        }

        /// <summary>
        /// Handles player life state transitions.
        /// </summary>
        public void OnLifeStateChanged(
            PlayerLifeState previousState,
            PlayerLifeState newState)
        {
            bool isEliminated = newState == PlayerLifeState.Eliminated;
            bool isActive = !isEliminated;

            if (useRagdollOnElimination && ragdollRoot != null)
            {
                if (isActive)
                {
                    ragdollRoot.transform.localPosition = Vector3.zero;
                    ragdollRoot.transform.localRotation = Quaternion.identity;

                    if (m_RagdollRigidbodies != null)
                    {
                        foreach (var rb in m_RagdollRigidbodies)
                        {
                            rb.transform.localPosition = Vector3.zero;
                            rb.transform.localRotation = Quaternion.identity;

                            if (!rb.isKinematic)
                            {
                                rb.linearVelocity = Vector3.zero;
                                rb.angularVelocity = Vector3.zero;
                            }
                        }
                    }

                    if (playerAnimator != null)
                    {
                        playerAnimator.enabled = true;
                        playerAnimator.Rebind();
                        playerAnimator.Update(0f);
                    }
                }
                else
                {
                    if (playerAnimator != null)
                    {
                        playerAnimator.enabled = false;
                    }
                }

                SetRagdollState(isEliminated);
            }
            

           

            if (isEliminated &&
                previousState != PlayerLifeState.Eliminated)
            {
                PlayEliminatedVFX();
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Selects and applies the material set for the single-player character.
        /// </summary>
        private void ApplyMaterialSet()
        {
            if (targetRenderer == null ||
                materialSets == null ||
                materialSets.Count == 0)
            {
                return;
            }

            // OmniCity is single-player, so use the first configured material set.
            int index = 0;

            PlayerMaterialSet selectedSet = materialSets[index];

            Material[] newMaterials = new Material[3];
            newMaterials[0] = selectedSet.body;
            newMaterials[1] = selectedSet.arms;
            newMaterials[2] = selectedSet.legs;

            targetRenderer.materials = newMaterials;
        }

        /// <summary>
        /// Toggles ragdoll physics.
        /// </summary>
        private void SetRagdollState(bool isActive)
        {
            if (m_RagdollRigidbodies == null)
            {
                return;
            }

            foreach (var rb in m_RagdollRigidbodies)
            {
                rb.isKinematic = !isActive;
            }
        }

        /// <summary>
        /// Plays elimination visual and audio effects.
        /// </summary>
        private void PlayEliminatedVFX()
        {
            if (soundDefPlayerEliminated != null)
            {
                CoreDirector.RequestAudio(soundDefPlayerEliminated)
                    .WithPosition(transform.position)
                    .Play();
            }

            if (eliminatedVFX != null)
            {
                GameObject vfxInstance =
                    Instantiate(
                        eliminatedVFX,
                        transform.position,
                        Quaternion.identity);

                Destroy(vfxInstance, 1f);
            }
        }

        #endregion
    }
}