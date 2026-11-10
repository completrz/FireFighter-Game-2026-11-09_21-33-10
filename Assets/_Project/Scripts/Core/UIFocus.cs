using System.Collections.Generic;
using UnityEngine;

namespace Firehouse.Core
{
    /// <summary>
    /// Decides who owns the mouse. Any open UI (menus, F1 panel) frees the cursor and blocks gameplay input;
    /// otherwise the cursor is locked while a local player wants it.
    /// </summary>
    public static class UIFocus
    {
        private static readonly HashSet<object> s_open = new();

        /// <summary>True while any menu/panel is open; gameplay code should ignore look/move/use input.</summary>
        public static bool IsBlocked => s_open.Count > 0;

        /// <summary>Set by the local player controller while it is alive in the world.</summary>
        public static bool GameplayWantsCursorLock { get; set; }

        public static void SetOpen(object owner, bool open)
        {
            if (open) s_open.Add(owner);
            else s_open.Remove(owner);
        }

        public static void ApplyCursor()
        {
            bool locked = !IsBlocked && GameplayWantsCursorLock && Application.isFocused;
            var mode = locked ? CursorLockMode.Locked : CursorLockMode.None;
            if (Cursor.lockState != mode) Cursor.lockState = mode;
            Cursor.visible = !locked;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_open.Clear();
            GameplayWantsCursorLock = false;
        }
    }
}
