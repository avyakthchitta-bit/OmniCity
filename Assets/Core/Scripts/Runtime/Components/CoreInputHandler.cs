using UnityEngine;
using UnityEngine.InputSystem;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Handles local player input and raises events for movement, looking,
    /// jumping, sprinting, primary actions, and menu input.
    /// </summary>
    public class CoreInputHandler : MonoBehaviour
    {
        [Header("Input Events")]
        [SerializeField] private Vector2Event onMoveInput;
        [SerializeField] private Vector2Event onLookInput;
        [SerializeField] private GameEvent onJumpPressed;
        [SerializeField] private GameEvent onJumpReleased;
        [SerializeField] private BoolEvent onSprintStateChanged;
        [SerializeField] private GameEvent onPrimaryPressed;
        [SerializeField] private GameEvent onMenuPressed;

        private GameplayInputSystem_Actions m_InputActions;

        private void Awake()
        {
            m_InputActions = new GameplayInputSystem_Actions();
        }

        private void OnEnable()
        {
            RegisterInputEvents();
            m_InputActions.Player.Enable();
        }

        private void OnDisable()
        {
            UnregisterInputEvents();
            m_InputActions.Player.Disable();
        }

        private void RegisterInputEvents()
        {
            m_InputActions.Player.Move.performed += HandleMove;
            m_InputActions.Player.Move.canceled += HandleMove;

            m_InputActions.Player.Look.performed += HandleLook;
            m_InputActions.Player.Look.canceled += HandleLook;

            m_InputActions.Player.Jump.performed += HandleJumpPressed;
            m_InputActions.Player.Jump.canceled += HandleJumpReleased;

            m_InputActions.Player.Sprint.performed += HandleSprint;
            m_InputActions.Player.Sprint.canceled += HandleSprint;

            
            m_InputActions.Player.Menu.performed += HandleMenu;
        }

        private void UnregisterInputEvents()
        {
            if (m_InputActions == null)
                return;

            m_InputActions.Player.Move.performed -= HandleMove;
            m_InputActions.Player.Move.canceled -= HandleMove;

            m_InputActions.Player.Look.performed -= HandleLook;
            m_InputActions.Player.Look.canceled -= HandleLook;

            m_InputActions.Player.Jump.performed -= HandleJumpPressed;
            m_InputActions.Player.Jump.canceled -= HandleJumpReleased;

            m_InputActions.Player.Sprint.performed -= HandleSprint;
            m_InputActions.Player.Sprint.canceled -= HandleSprint;

            
            m_InputActions.Player.Menu.performed -= HandleMenu;
        }

        private void HandleMove(InputAction.CallbackContext context)
        {
            onMoveInput?.Raise(context.ReadValue<Vector2>());
        }

        private void HandleLook(InputAction.CallbackContext context)
        {
            onLookInput?.Raise(context.ReadValue<Vector2>());
        }

        private void HandleJumpPressed(InputAction.CallbackContext context)
        {
            onJumpPressed?.Raise();
        }

        private void HandleJumpReleased(InputAction.CallbackContext context)
        {
            onJumpReleased?.Raise();
        }

        private void HandleSprint(InputAction.CallbackContext context)
        {
            onSprintStateChanged?.Raise(context.ReadValueAsButton());
        }

        

        private void HandleMenu(InputAction.CallbackContext context)
        {
            onMenuPressed?.Raise();
        }
    }
}