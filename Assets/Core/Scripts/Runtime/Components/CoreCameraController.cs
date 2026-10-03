using UnityEngine;
using Unity.Cinemachine;
using System.Collections.Generic;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Manages the player's camera rig, handling look input and switching between different camera modes.
    /// This component uses Cinemachine to control the active virtual camera by adjusting priorities.
    /// </summary>
    public class CoreCameraController : MonoBehaviour
    {
        [Header("Camera Setup")]
        [SerializeField] private List<CoreCameraMode> cameraModePrefabs = new List<CoreCameraMode>();
        [SerializeField] private Transform lookTarget;
        [SerializeField] private string initialModeName = "FreeLook";

        [Header("Default Look Settings")]
        [SerializeField] private bool enableLookInput = true;
        [SerializeField] private float defaultLookSensitivity = 1.0f;
        [SerializeField] private float defaultVerticalLookLimit = 70.0f;

        [Header("Listening to Events")]
        [SerializeField] private Vector2Event onLookInput;

        public float CurrentHorizontalLookAngle => m_CurrentHorizontalLookAngle;

        public CoreMovement.CouplingMode CurrentPlayerRotationMode { get; private set; }

        public CoreCameraMode ActiveCameraMode { get; private set; }

        private float m_CurrentVerticalLookAngle;
        private float m_CurrentHorizontalLookAngle;
        private float m_LookSensitivity;
        private float m_VerticalLookLimit;

        private CinemachineBrain m_CinemachineBrain;

        private readonly List<CoreCameraMode> m_RegisteredCameraModes = new List<CoreCameraMode>();
        private readonly HashSet<CoreCameraMode> m_InstantiatedCameraModes = new HashSet<CoreCameraMode>();

        private void Awake()
        {
            m_LookSensitivity = defaultLookSensitivity;
            m_VerticalLookLimit = defaultVerticalLookLimit;

            if (lookTarget == null)
            {
                lookTarget = transform;
            }

            if (onLookInput == null && enableLookInput)
            {
                Debug.LogError("[CoreCameraController] Look input event is not assigned.", this);
            }

            m_CurrentHorizontalLookAngle = transform.rotation.eulerAngles.y;
        }

        private void Start()
        {
            SetupCinemachine();

            if (cameraModePrefabs.Count > 0)
            {
                SwitchCameraMode(initialModeName);
            }

            if (enableLookInput && onLookInput != null)
            {
                onLookInput.RegisterListener(SetLookInput);
            }
        }

        private void OnDestroy()
        {
            if (enableLookInput && onLookInput != null)
            {
                onLookInput.UnregisterListener(SetLookInput);
            }

            foreach (var mode in m_InstantiatedCameraModes)
            {
                if (mode != null)
                {
                    Destroy(mode.gameObject);
                }
            }

            m_InstantiatedCameraModes.Clear();
            m_RegisteredCameraModes.Clear();
        }

        private void LateUpdate()
        {
            if (lookTarget != null && enableLookInput)
            {
                Quaternion horizontalRotation =
                    Quaternion.Euler(0f, m_CurrentHorizontalLookAngle, 0f);

                Quaternion verticalRotation =
                    Quaternion.Euler(m_CurrentVerticalLookAngle, 0f, 0f);

                lookTarget.rotation = horizontalRotation * verticalRotation;
            }
        }

        public void SetLookInput(Vector2 lookInput)
        {
            if (!enableLookInput)
                return;

            m_CurrentHorizontalLookAngle +=
                lookInput.x * m_LookSensitivity;

            m_CurrentVerticalLookAngle = Mathf.Clamp(
                m_CurrentVerticalLookAngle -
                (lookInput.y * m_LookSensitivity),
                -m_VerticalLookLimit,
                m_VerticalLookLimit);
        }

        public void SetLookSensitivity(float sensitivity)
        {
            m_LookSensitivity = sensitivity;
        }

        public bool SwitchCameraMode(string modeName)
        {
            CoreCameraMode targetMode =
                m_RegisteredCameraModes.Find(m => m.ModeName == modeName);

            if (targetMode == null)
            {
                Debug.LogWarning($"Camera mode '{modeName}' not found.");
                return false;
            }

            if (targetMode.CinemachineCamera == null)
            {
                Debug.LogWarning(
                    $"CinemachineCamera for mode '{modeName}' is not available.");
                return false;
            }

            ActiveCameraMode = targetMode;
            CurrentPlayerRotationMode = targetMode.PlayerRotationMode;

            if (targetMode.OverrideLookSettings)
            {
                m_LookSensitivity = targetMode.LookSensitivity;
                m_VerticalLookLimit = targetMode.VerticalLookLimit;
            }
            else
            {
                m_LookSensitivity = defaultLookSensitivity;
                m_VerticalLookLimit = defaultVerticalLookLimit;
            }

            foreach (var mode in m_RegisteredCameraModes)
            {
                if (mode != null)
                {
                    mode.SetActive(mode == targetMode);
                }
            }

            return true;
        }

        public CoreCameraMode RegisterCameraMode(CoreCameraMode cameraModePrefab)
        {
            if (cameraModePrefab == null)
            {
                Debug.LogWarning(
                    "[CoreCameraController] Cannot register a null camera mode prefab.",
                    this);
                return null;
            }

            if (m_RegisteredCameraModes.Exists(
                    m => m.ModeName == cameraModePrefab.ModeName))
            {
                Debug.LogWarning(
                    $"[CoreCameraController] A camera mode with name '{cameraModePrefab.ModeName}' is already registered.",
                    this);
                return null;
            }

            CoreCameraMode modeToRegister = Instantiate(cameraModePrefab);

            modeToRegister.SetTargets(lookTarget);
            m_RegisteredCameraModes.Add(modeToRegister);
            m_InstantiatedCameraModes.Add(modeToRegister);

            modeToRegister.SetActive(false);

            return modeToRegister;
        }

        public bool RegisterCameraModeInstance(CoreCameraMode cameraModeInstance)
        {
            if (cameraModeInstance == null)
            {
                Debug.LogWarning(
                    "[CoreCameraController] Cannot register a null camera mode instance.",
                    this);
                return false;
            }

            if (m_RegisteredCameraModes.Contains(cameraModeInstance))
            {
                Debug.LogWarning(
                    $"[CoreCameraController] Camera mode instance '{cameraModeInstance.ModeName}' is already registered.",
                    this);
                return false;
            }

            if (m_RegisteredCameraModes.Exists(
                    m => m.ModeName == cameraModeInstance.ModeName))
            {
                Debug.LogWarning(
                    $"[CoreCameraController] A camera mode with name '{cameraModeInstance.ModeName}' is already registered.",
                    this);
                return false;
            }

            cameraModeInstance.SetTargets(lookTarget);
            m_RegisteredCameraModes.Add(cameraModeInstance);

            cameraModeInstance.SetActive(false);

            return true;
        }

        public bool UnregisterCameraMode(
            CoreCameraMode cameraMode,
            bool destroy = false)
        {
            if (cameraMode == null)
            {
                Debug.LogWarning(
                    "[CoreCameraController] Cannot unregister a null camera mode.",
                    this);
                return false;
            }

            if (!m_RegisteredCameraModes.Contains(cameraMode))
            {
                Debug.LogWarning(
                    $"[CoreCameraController] Camera mode '{cameraMode.ModeName}' is not registered.",
                    this);
                return false;
            }

            if (ActiveCameraMode == cameraMode)
            {
                CoreCameraMode fallbackMode = null;

                foreach (var mode in m_RegisteredCameraModes)
                {
                    if (mode != cameraMode && mode != null)
                    {
                        fallbackMode = mode;
                        break;
                    }
                }

                if (fallbackMode != null)
                {
                    SwitchCameraMode(fallbackMode.ModeName);
                }
                else
                {
                    ActiveCameraMode = null;
                    CurrentPlayerRotationMode =
                        CoreMovement.CouplingMode.Decoupled;
                }
            }

            m_RegisteredCameraModes.Remove(cameraMode);
            m_InstantiatedCameraModes.Remove(cameraMode);

            if (destroy && cameraMode != null)
            {
                Destroy(cameraMode.gameObject);
            }

            return true;
        }

        public bool UnregisterCameraMode(
            string modeName,
            bool destroy = false)
        {
            CoreCameraMode targetMode =
                m_RegisteredCameraModes.Find(m => m.ModeName == modeName);

            if (targetMode == null)
            {
                Debug.LogWarning(
                    $"[CoreCameraController] Camera mode '{modeName}' not found for unregistration.",
                    this);
                return false;
            }

            return UnregisterCameraMode(targetMode, destroy);
        }

        public IReadOnlyList<CoreCameraMode> GetRegisteredCameraModes()
        {
            return m_RegisteredCameraModes.AsReadOnly();
        }

        public bool IsCameraModeRegistered(string modeName)
        {
            return m_RegisteredCameraModes.Exists(
                m => m.ModeName == modeName);
        }

        private void SetupCinemachine()
        {
            var mainCam = Camera.main;

            if (mainCam == null)
            {
                Debug.LogError(
                    "No main camera found in the scene. [CoreCameraController] requires a main camera.",
                    this);
                return;
            }

            m_CinemachineBrain =
                mainCam.GetComponent<CinemachineBrain>();

            if (m_CinemachineBrain == null)
            {
                m_CinemachineBrain =
                    mainCam.gameObject.AddComponent<CinemachineBrain>();
            }

            if (cameraModePrefabs.Count == 0)
            {
                Debug.LogError(
                    "[CoreCameraController] No camera mode prefabs assigned.",
                    this);
                return;
            }

            foreach (var prefab in cameraModePrefabs)
            {
                if (prefab == null)
                {
                    Debug.LogWarning(
                        "Null camera mode prefab in [CoreCameraController]",
                        this);
                    continue;
                }

                if (m_RegisteredCameraModes.Exists(
                        m => m.ModeName == prefab.ModeName))
                {
                    Debug.LogWarning(
                        $"[CoreCameraController] Duplicate camera mode name '{prefab.ModeName}' found in prefab list. Skipping duplicate.",
                        this);
                    continue;
                }

                CoreCameraMode instance = Instantiate(prefab);

                instance.SetTargets(lookTarget);
                m_RegisteredCameraModes.Add(instance);
                m_InstantiatedCameraModes.Add(instance);
            }
        }
    }
}