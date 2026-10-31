
using UnityEngine;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Controls the player's Animator component based on the state of
    /// the CoreMovement controller.
    /// Handles locomotion parameters and animation sound events.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class CoreAnimator : MonoBehaviour
    {
        #region Fields & Properties

        [Header("Component Dependencies")]
        [SerializeField] private CoreMovement coreMovement;
        [SerializeField] private Animator animator;

        [Header("Sound Effects")]
        [SerializeField] private SoundDef soundDefFootstep;

        private readonly int m_AnimIDSpeed =
            Animator.StringToHash("Speed");

        private readonly int m_AnimIDGrounded =
            Animator.StringToHash("Grounded");

        private readonly int m_AnimIDJump =
            Animator.StringToHash("Jump");

        private readonly int m_AnimIDFreeFall =
            Animator.StringToHash("FreeFall");

        private readonly int m_AnimIDMotionSpeed =
            Animator.StringToHash("MotionSpeed");

        #endregion

        #region Unity Methods

        private void Awake()
        {
            if (animator == null)
                animator = GetComponent<Animator>();

            if (animator == null)
                animator = GetComponentInChildren<Animator>();

            if (coreMovement == null)
                coreMovement = GetComponentInParent<CoreMovement>();

            if (coreMovement == null)
            {
                Debug.LogError(
                    "[Core Animator] needs a CoreMovement component.",
                    this);
            }

            if (animator == null)
            {
                Debug.LogError(
                    "[Core Animator] needs an Animator component.",
                    this);
            }

            if (soundDefFootstep == null)
            {
                Debug.LogError(
                    "[Core Animator] Footstep SoundDef is not assigned.",
                    this);
            }
        }

        private void Update()
        {
            if (coreMovement == null || animator == null)
                return;

            UpdateLocomotionParameters();
        }

        #endregion

        #region Animation Events

        public void OnFootstepWalk(AnimationEvent animationEvent)
        {
            if (animationEvent.animatorClipInfo.weight >= 0.5f)
            {
                OnFootstep(animationEvent, 0, 0.75f, 0);
            }
        }

        public void OnFootstepRun(AnimationEvent animationEvent)
        {
            if (animationEvent.animatorClipInfo.weight > 0.5f)
            {
                OnFootstep(animationEvent, 500, 1, 2000);
            }
        }

        public void OnFootstep(
            AnimationEvent animationEvent,
            float walkRunPitchCents,
            float walkRunVolumeScale,
            float filterCutoffOffset)
        {
            var overrideData =
                new SoundEmitter.SoundDefOverrideData
                {
                    BasePitchInCents = walkRunPitchCents,
                    VolumeScale = walkRunVolumeScale,
                    BaseLowPassCutoff = filterCutoffOffset
                };

            CoreDirector.RequestAudio(soundDefFootstep)
                .AttachedTo(transform)
                .WithOverrides(overrideData)
                .AsReserved(
                    SoundEmitter.ReservedInfo.ReservedEmitterAndAudioSources)
                .Play();
        }

        public void OnLand(AnimationEvent animationEvent)
        {
            CoreDirector.RequestAudio(soundDefFootstep)
                .AttachedTo(transform)
                .AsReserved(
                    SoundEmitter.ReservedInfo.ReservedEmitterAndAudioSources)
                .Play();
        }

        #endregion

        #region Private Methods

        private void UpdateLocomotionParameters()
        {
            bool isGrounded = coreMovement.IsGrounded;
            float verticalVelocity = coreMovement.VerticalVelocity;

            animator.SetBool(m_AnimIDGrounded, isGrounded);

            animator.SetBool(
                m_AnimIDJump,
                !isGrounded && verticalVelocity > 0.1f);

            animator.SetBool(
                m_AnimIDFreeFall,
                !isGrounded && verticalVelocity <= 0.1f);

            animator.SetFloat(
                m_AnimIDSpeed,
                coreMovement.CurrentSpeed);

            animator.SetFloat(
                m_AnimIDMotionSpeed,
                coreMovement.InputMagnitude);
        }

        public void TurnInPlaceStart()
        {
            if (animator != null)
                animator.applyRootMotion = true;
        }

        public void TurnInPlaceEnd()
        {
            if (animator != null)
                animator.applyRootMotion = false;
        }

        #endregion
    }
}
