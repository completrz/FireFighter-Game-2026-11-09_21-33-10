using UnityEngine;
using UnityEngine.InputSystem;

namespace Firehouse.Core
{
    /// <summary>
    /// F1 debug panel. Created automatically at startup in every scene and build.
    /// Shows a slider for every [Tunable] field on loaded <see cref="TunableData"/> assets,
    /// plus any custom sections registered with <see cref="TuningRegistry.AddSection"/>.
    /// </summary>
    public sealed class DebugPanel : MonoBehaviour
    {
        public static bool IsOpen { get; private set; }

        private Vector2 _scroll;
        private Rect _window = new Rect(16, 16, 380, 560);
        private CursorLockMode _prevLock;
        private bool _prevVisible;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (FindAnyObjectByType<DebugPanel>() != null) return;
            var go = new GameObject("[DebugPanel]");
            go.AddComponent<DebugPanel>();
            DontDestroyOnLoad(go);
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.f1Key.wasPressedThisFrame)
                SetOpen(!IsOpen);
        }

        private void SetOpen(bool open)
        {
            if (open == IsOpen) return;
            IsOpen = open;
            if (open)
            {
                _prevLock = Cursor.lockState;
                _prevVisible = Cursor.visible;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.lockState = _prevLock;
                Cursor.visible = _prevVisible;
            }
        }

        private void OnGUI()
        {
            if (!IsOpen) return;
            _window = GUILayout.Window(0xF1, _window, DrawWindow, "Firehouse debug (F1)");
        }

        private void DrawWindow(int id)
        {
            _scroll = GUILayout.BeginScrollView(_scroll);

            foreach (var (title, draw) in TuningRegistry.Sections)
            {
                GUILayout.Label($"<b>{title}</b>", RichLabel);
                draw?.Invoke();
                GUILayout.Space(8);
            }

            foreach (var entry in TuningRegistry.Entries)
            {
                if (entry.Asset == null || entry.Fields.Count == 0) continue;
                GUILayout.Label($"<b>{entry.Asset.DebugCategory}</b>", RichLabel);
                bool changed = false;
                foreach (var (field, range) in entry.Fields)
                    changed |= DrawField(entry.Asset, field, range);
                if (changed) entry.Asset.OnTuningChanged();
                GUILayout.Space(8);
            }

            if (TuningRegistry.Entries.Count == 0 && TuningRegistry.Sections.Count == 0)
                GUILayout.Label("Nothing registered yet.");

            GUILayout.EndScrollView();
            GUI.DragWindow();
        }

        private static bool DrawField(Object asset, System.Reflection.FieldInfo field, TunableAttribute range)
        {
            string label = ObjectNames(field.Name);
            object value = field.GetValue(asset);

            if (field.FieldType == typeof(bool))
            {
                bool b = (bool)value;
                bool nb = GUILayout.Toggle(b, label);
                if (nb == b) return false;
                field.SetValue(asset, nb);
                return true;
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(150));
            bool changedValue = false;
            if (field.FieldType == typeof(int))
            {
                int i = (int)value;
                int ni = Mathf.RoundToInt(GUILayout.HorizontalSlider(i, range.Min, range.Max));
                GUILayout.Label(ni.ToString(), GUILayout.Width(50));
                if (ni != i) { field.SetValue(asset, ni); changedValue = true; }
            }
            else
            {
                float f = (float)value;
                float nf = GUILayout.HorizontalSlider(f, range.Min, range.Max);
                GUILayout.Label(nf.ToString("0.###"), GUILayout.Width(50));
                if (!Mathf.Approximately(nf, f)) { field.SetValue(asset, nf); changedValue = true; }
            }
            GUILayout.EndHorizontal();
            return changedValue;
        }

        private static string ObjectNames(string fieldName)
        {
            // "_maxSpeed" -> "Max Speed"
            var s = fieldName.StartsWith("m_") ? fieldName.Substring(2) : fieldName.TrimStart('_');
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (i == 0) { sb.Append(char.ToUpperInvariant(c)); continue; }
                if (char.IsUpper(c) && !char.IsUpper(s[i - 1])) sb.Append(' ');
                sb.Append(c);
            }
            return sb.ToString();
        }

        private static GUIStyle s_rich;
        private static GUIStyle RichLabel => s_rich ??= new GUIStyle(GUI.skin.label) { richText = true };
    }
}
