using Firehouse.Core;
using Firehouse.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Firehouse.Player
{
    /// <summary>
    /// Offline walk-around controller for art/level test scenes (e.g. Scenes/Firehouse_Test). Not networked and
    /// not the gameplay Smokie: WASD + mouse, Shift sprint, Space jump, Esc frees the cursor, click re-locks.
    /// Hides the prototype session menu so the scene is playable without hosting.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class SimpleFirstPersonController : MonoBehaviour
    {
        [SerializeField] private Transform _camera;
        [SerializeField] private float _walkSpeed = 4f;
        [SerializeField] private float _sprintSpeed = 7f;
        [SerializeField] private float _jumpHeight = 1.1f;
        [SerializeField] private float _gravity = -20f;
        [SerializeField] private float _mouseSensitivity = 0.1f;
        [SerializeField] private float _stickLookSpeed = 160f;
        [SerializeField] private bool _hideSessionMenu = true;

        private CharacterController _cc;
        private float _pitch;
        private float _verticalVelocity;

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            if (_camera == null && Camera.main != null) _camera = Camera.main.transform;
        }

        private void Start()
        {
            if (_hideSessionMenu)
            {
                var menu = FindAnyObjectByType<MainMenu>();
                if (menu != null)
                {
                    menu.enabled = false;
                    UIFocus.SetOpen(menu, false);
                }
            }
            UIFocus.GameplayWantsCursorLock = true;
        }

        private void OnDisable() => UIFocus.GameplayWantsCursorLock = false;

        private void Update()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            var pad = Gamepad.current;

            if (kb != null && kb.escapeKey.wasPressedThisFrame) UIFocus.GameplayWantsCursorLock = false;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && !UIFocus.IsBlocked) UIFocus.GameplayWantsCursorLock = true;
            UIFocus.ApplyCursor();

            bool active = !UIFocus.IsBlocked && Cursor.lockState == CursorLockMode.Locked;

            // Look
            Vector2 look = Vector2.zero;
            if (active && mouse != null) look += mouse.delta.ReadValue() * _mouseSensitivity;
            if (!UIFocus.IsBlocked && pad != null) look += pad.rightStick.ReadValue() * (_stickLookSpeed * Time.deltaTime);
            transform.Rotate(0f, look.x, 0f, Space.Self);
            _pitch = Mathf.Clamp(_pitch - look.y, -85f, 85f);
            if (_camera != null) _camera.localRotation = Quaternion.Euler(_pitch, 0f, 0f);

            // Move
            Vector2 move = Vector2.zero;
            bool sprint = false, jump = false;
            if (!UIFocus.IsBlocked)
            {
                if (kb != null)
                {
                    move.x = (kb.dKey.isPressed ? 1 : 0) - (kb.aKey.isPressed ? 1 : 0);
                    move.y = (kb.wKey.isPressed ? 1 : 0) - (kb.sKey.isPressed ? 1 : 0);
                    sprint = kb.leftShiftKey.isPressed;
                    jump = kb.spaceKey.wasPressedThisFrame;
                }
                if (pad != null)
                {
                    move += pad.leftStick.ReadValue();
                    sprint |= pad.leftStickButton.isPressed;
                    jump |= pad.buttonSouth.wasPressedThisFrame;
                }
            }
            move = Vector2.ClampMagnitude(move, 1f);

            bool grounded = _cc.isGrounded;
            if (grounded && _verticalVelocity < 0f) _verticalVelocity = -2f;
            if (grounded && jump) _verticalVelocity = Mathf.Sqrt(_jumpHeight * -2f * _gravity);
            _verticalVelocity += _gravity * Time.deltaTime;

            float speed = sprint ? _sprintSpeed : _walkSpeed;
            Vector3 velocity = (transform.right * move.x + transform.forward * move.y) * speed;
            velocity.y = _verticalVelocity;
            _cc.Move(velocity * Time.deltaTime);
        }
    }
}
