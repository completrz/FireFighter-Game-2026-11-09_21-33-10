using Firehouse.Core;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Firehouse.Player
{
    /// <summary>
    /// PLACEHOLDER for step 1 (lobby testing): CharacterController walk/look/jump, owner-driven.
    /// Replaced by the physics-based Smokies controller in step 2.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class SimplePlayerMotor : NetworkBehaviour
    {
        [SerializeField] private Transform _cameraPivot;
        [SerializeField] private float _walkSpeed = 4f;
        [SerializeField] private float _sprintSpeed = 7f;
        [SerializeField] private float _jumpSpeed = 5f;
        [SerializeField] private float _mouseSensitivity = 0.1f;

        private CharacterController _cc;
        private float _pitch;
        private float _vy;

        private void Awake() => _cc = GetComponent<CharacterController>();

        public override void OnNetworkSpawn()
        {
            // Only the owner simulates movement; everyone else just receives NetworkTransform updates.
            _cc.enabled = IsOwner;
            if (IsOwner) UIFocus.GameplayWantsCursorLock = true;
        }

        public override void OnNetworkDespawn()
        {
            if (IsOwner) UIFocus.GameplayWantsCursorLock = false;
        }

        private void Update()
        {
            if (!IsSpawned || !IsOwner) return;
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            bool blocked = UIFocus.IsBlocked || kb == null;

            if (!blocked && mouse != null && Cursor.lockState == CursorLockMode.Locked)
            {
                var d = mouse.delta.ReadValue() * _mouseSensitivity;
                transform.Rotate(0f, d.x, 0f);
                _pitch = Mathf.Clamp(_pitch - d.y, -85f, 85f);
                _cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
            }

            Vector2 move = Vector2.zero;
            if (!blocked)
            {
                if (kb.wKey.isPressed) move.y += 1;
                if (kb.sKey.isPressed) move.y -= 1;
                if (kb.dKey.isPressed) move.x += 1;
                if (kb.aKey.isPressed) move.x -= 1;
            }
            move = Vector2.ClampMagnitude(move, 1f);
            float speed = !blocked && kb.leftShiftKey.isPressed ? _sprintSpeed : _walkSpeed;
            var vel = (transform.forward * move.y + transform.right * move.x) * speed;

            if (_cc.isGrounded)
            {
                _vy = -1f;
                if (!blocked && kb.spaceKey.wasPressedThisFrame) _vy = _jumpSpeed;
            }
            _vy += Physics.gravity.y * Time.deltaTime;
            vel.y = _vy;
            _cc.Move(vel * Time.deltaTime);
        }
    }
}
