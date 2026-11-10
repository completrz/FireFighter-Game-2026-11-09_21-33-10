using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Firehouse.Player
{
    /// <summary>
    /// Player input actions built in code (keyboard/mouse + gamepad). One instance per local player.
    /// Rebinding can be layered on later by saving overrides on <see cref="Map"/>.
    /// </summary>
    public sealed class PlayerInputReader : IDisposable
    {
        public readonly InputActionMap Map = new("Player");
        public readonly InputAction Move, Look, Jump, Sprint, Grab, Throw, Interact;

        public PlayerInputReader()
        {
            Move = Map.AddAction("Move", InputActionType.Value);
            Move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            Move.AddBinding("<Gamepad>/leftStick");

            Look = Map.AddAction("Look", InputActionType.Value);
            Look.AddBinding("<Mouse>/delta");
            Look.AddBinding("<Gamepad>/rightStick").WithProcessor("scaleVector2(x=1,y=1)");

            Jump = Map.AddAction("Jump", InputActionType.Button, "<Keyboard>/space");
            Jump.AddBinding("<Gamepad>/buttonSouth");

            Sprint = Map.AddAction("Sprint", InputActionType.Button, "<Keyboard>/leftShift");
            Sprint.AddBinding("<Gamepad>/leftStickPress");

            Grab = Map.AddAction("Grab", InputActionType.Button, "<Mouse>/leftButton");
            Grab.AddBinding("<Gamepad>/rightTrigger");

            Throw = Map.AddAction("Throw", InputActionType.Button, "<Mouse>/rightButton");
            Throw.AddBinding("<Gamepad>/leftTrigger");

            Interact = Map.AddAction("Interact", InputActionType.Button, "<Keyboard>/e");
            Interact.AddBinding("<Gamepad>/buttonWest");

            Map.Enable();
        }

        /// <summary>Look delta this frame in degrees (mouse) — gamepad is converted using speed * dt.</summary>
        public Vector2 ReadLookDegrees(float mouseSensitivity, float gamepadSpeed)
        {
            var v = Look.ReadValue<Vector2>();
            bool fromGamepad = Look.activeControl?.device is Gamepad;
            return fromGamepad ? v * (gamepadSpeed * Time.deltaTime) : v * mouseSensitivity;
        }

        public void Dispose()
        {
            Map.Disable();
            Map.Dispose();
        }
    }
}
