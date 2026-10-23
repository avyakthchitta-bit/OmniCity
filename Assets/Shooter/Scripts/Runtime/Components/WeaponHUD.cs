
using UnityEngine;
using System.Collections;
using Blocks.Gameplay.Core;
using UnityEngine.UIElements;
using Image = UnityEngine.UI.Image;

namespace Blocks.Gameplay.Shooter
{
    public class WeaponHUD : CoreHUD
    {
        #region Fields & Properties

        [Header("Listening to")]
        [Tooltip("Event triggered when clip ammo count changes.")]
        [SerializeField] private IntPairEvent onClipAmmoChanged;
        [Tooltip("Event triggered when weapon is swapped.")]
        [SerializeField] private WeaponSwapEvent onWeaponChanged;
        [Tooltip("Event triggered when aiming state changes.")]
        [SerializeField] private BoolEvent onAimingStateChanged;
        [Tooltip("Event triggered when weapon is fired.")]
        [SerializeField] private FloatEvent onWeaponFiredEvent;
        [Tooltip("Event triggered when reload starts.")]
        [SerializeField] private FloatEvent onReloadStartedEvent;
        [Tooltip("Event triggered when weapon spread changes.")]
        [SerializeField] private FloatEvent onWeaponSpreadChanged;

        [Header("Component Dependencies")]
        [Tooltip("Reference to the aim controller for reticle management.")]
        [SerializeField] private AimController aimController;

        [Header("UGUI")]
        [Tooltip("UGUI image component for displaying reload progress indicator.")]
        [SerializeField] private Image weaponReload;

        private Coroutine m_ReloadCoroutine;
        private Coroutine m_MessageCoroutine;
        private Coroutine m_ReticleCooldownCoroutine;

        private Label m_AmmoLabel;
        private Label m_WeaponTypeLabel;
        private UnityEngine.UIElements.Image m_WeaponIcon;
        private ProgressBar m_PlayerAmmoBar;
        private VisualElement m_PlayerReticle;
        private WeaponData m_CurrentWeaponData;

        #endregion

        #region Unity Methods

        protected override void Initialize()
        {
            base.Initialize();

            if (aimController == null)
            {
                Debug.LogError("AimController is not assigned on WeaponHUD!", this);
            }

            UpdateWeaponReticleAndIcon(onWeaponChanged.LastValue);
            UpdateReticleVisibility(onAimingStateChanged.LastValue);
        }

        private void LateUpdate()
        {
            if (aimController == null)
                return;

            // Keep the reticle centered.
            if (m_PlayerReticle != null &&
                m_PlayerReticle.style.position != StyleKeyword.Initial)
            {
                m_PlayerReticle.style.position = StyleKeyword.Initial;
                m_PlayerReticle.style.left = StyleKeyword.Initial;
                m_PlayerReticle.style.top = StyleKeyword.Initial;
            }

            // Keep the reload indicator centered on the reticle.
            if (weaponReload != null)
            {
                weaponReload.rectTransform.anchoredPosition = Vector2.zero;
            }
        }

        #endregion

        #region Protected Methods

        protected override void RegisterAdditionalListeners()
        {
            onWeaponFiredEvent.RegisterListener(HandleWeaponFired);
            onClipAmmoChanged.RegisterListener(UpdateClipAmmoDisplay);
            onReloadStartedEvent.RegisterListener(HandleReloadStarted);
            onWeaponSpreadChanged.RegisterListener(UpdateReticleSpread);
            onWeaponChanged.RegisterListener(UpdateWeaponReticleAndIcon);
            onAimingStateChanged.RegisterListener(UpdateReticleVisibility);
        }

        protected override void UnregisterAdditionalListeners()
        {
            onWeaponFiredEvent.UnregisterListener(HandleWeaponFired);
            onClipAmmoChanged.UnregisterListener(UpdateClipAmmoDisplay);
            onReloadStartedEvent.UnregisterListener(HandleReloadStarted);
            onWeaponSpreadChanged.UnregisterListener(UpdateReticleSpread);
            onWeaponChanged.UnregisterListener(UpdateWeaponReticleAndIcon);
            onAimingStateChanged.UnregisterListener(UpdateReticleVisibility);
        }

        protected override void QueryHUDElements(VisualElement root)
        {
            base.QueryHUDElements(root);
            m_WeaponTypeLabel = root.Q<Label>("weapon-type-label");
            m_AmmoLabel = root.Q<Label>("ammo-label");
            m_PlayerAmmoBar = root.Q<ProgressBar>("player-ammo-bar");
            m_PlayerReticle = root.Q<VisualElement>("player-reticle");
            m_WeaponIcon = root.Q<UnityEngine.UIElements.Image>("weapon-icon");
        }

        protected override void SetHUDDefaults()
        {
            base.SetHUDDefaults();

            if (weaponReload != null)
            {
                float baseSize = m_CurrentWeaponData?.reticleBaseSize ?? 80f;
                weaponReload.rectTransform.sizeDelta = new Vector2(baseSize, baseSize);
                weaponReload.gameObject.SetActive(false);
            }

            if (m_PlayerReticle != null)
            {
                m_PlayerReticle.style.position = Position.Absolute;
            }
        }

        #endregion

        #region Private Methods

        private void UpdateClipAmmoDisplay(IntPair clip)
        {
            if (m_AmmoLabel != null)
            {
                m_AmmoLabel.text = $"{clip.value1} / {clip.value2}";

                if (clip.value1 == 0)
                    m_AmmoLabel.style.color = Color.red;
                else if (clip.value1 <= clip.value2 * 0.3f)
                    m_AmmoLabel.style.color = Color.yellow;
                else
                    m_AmmoLabel.style.color = Color.white;
            }

            if (m_PlayerAmmoBar != null)
            {
                m_PlayerAmmoBar.value = clip.value1;
                m_PlayerAmmoBar.highValue = clip.value2;
            }
        }

        private void UpdateWeaponReticleAndIcon(WeaponSwapPayload payload)
        {
            if (m_PlayerReticle == null)
                return;

            if (payload.NewWeapon != null)
            {
                m_CurrentWeaponData = payload.NewWeapon.GetWeaponData();

                if (m_CurrentWeaponData != null)
                {
                    m_PlayerReticle.style.backgroundImage =
                        new StyleBackground(m_CurrentWeaponData.reticleImage);

                    if (m_WeaponIcon != null)
                        m_WeaponIcon.sprite = m_CurrentWeaponData.weaponIcon;

                    if (m_WeaponTypeLabel != null)
                        m_WeaponTypeLabel.text = m_CurrentWeaponData.weaponName;
                }

                if (payload.NewWeapon is ModularWeapon simpleWeapon)
                {
                    simpleWeapon.GetAmmoInfo(out int current, out int max);
                    UpdateClipAmmoDisplay(
                        new IntPair { value1 = current, value2 = max });

                    UpdateReticleSpread(simpleWeapon.GetCurrentSpreadAngle());
                }
            }
            else
            {
                m_CurrentWeaponData = null;

                if (m_WeaponTypeLabel != null)
                    m_WeaponTypeLabel.text = "No Weapon";

                if (m_WeaponIcon != null)
                    m_WeaponIcon.sprite = null;

                if (m_AmmoLabel != null)
                    m_AmmoLabel.text = "";

                if (m_PlayerAmmoBar != null)
                    m_PlayerAmmoBar.value = 0;

                m_PlayerReticle.style.backgroundImage = StyleKeyword.None;
                m_PlayerReticle.style.display = DisplayStyle.None;

                UpdateReticleSpread(0);
            }
        }

        private void UpdateReticleVisibility(bool isAiming)
        {
            if (m_PlayerReticle == null)
                return;

            bool shouldShow = isAiming && m_CurrentWeaponData != null;

            m_PlayerReticle.style.display =
                shouldShow ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void HandleWeaponFired(float fireRate)
        {
            if (m_ReticleCooldownCoroutine != null)
                StopCoroutine(m_ReticleCooldownCoroutine);

            m_ReticleCooldownCoroutine = StartCoroutine(
                ReticleCooldownCoroutine(fireRate));
        }

        private void UpdateReticleSpread(float spreadAngle)
        {
            if (m_PlayerReticle == null)
                return;

            float baseSize = m_CurrentWeaponData?.reticleBaseSize ?? 80f;
            float spreadMultiplier =
                m_CurrentWeaponData?.reticleSpreadMultiplier ?? 100f;

            float finalSize = baseSize + (spreadAngle * spreadMultiplier);

            m_PlayerReticle.style.width = finalSize;
            m_PlayerReticle.style.height = finalSize;

            if (weaponReload != null)
            {
                float weaponReloadSize = finalSize * 0.7f;

                weaponReload.rectTransform.sizeDelta =
                    new Vector2(weaponReloadSize, weaponReloadSize);
            }
        }

        private void HandleReloadStarted(float reloadTime)
        {
            if (m_ReloadCoroutine != null)
                StopCoroutine(m_ReloadCoroutine);

            m_ReloadCoroutine =
                StartCoroutine(ReloadAnimationCoroutine(reloadTime));
        }

        private IEnumerator ReticleCooldownCoroutine(float duration)
        {
            if (m_PlayerReticle == null)
                yield break;

            m_PlayerReticle.style.unityBackgroundImageTintColor =
                new StyleColor(Color.grey);

            yield return new WaitForSeconds(duration);

            m_PlayerReticle.style.unityBackgroundImageTintColor =
                new StyleColor(Color.white);
        }

        private IEnumerator ReloadAnimationCoroutine(float reloadTime)
        {
            if (weaponReload == null || reloadTime <= 0f)
                yield break;

            weaponReload.gameObject.SetActive(true);
            weaponReload.fillAmount = 0f;

            float elapsedTime = 0f;

            while (elapsedTime < reloadTime)
            {
                elapsedTime += Time.deltaTime;
                weaponReload.fillAmount =
                    Mathf.Clamp01(elapsedTime / reloadTime);

                yield return null;
            }

            weaponReload.fillAmount = 1f;
            weaponReload.gameObject.SetActive(false);
        }

        #endregion
    }
}
