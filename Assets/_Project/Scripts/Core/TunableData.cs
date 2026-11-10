using UnityEngine;

namespace Firehouse.Core
{
    /// <summary>
    /// Base class for every tuning ScriptableObject (truck stats, hose stats, fire materials...).
    /// Loaded assets register themselves so their [Tunable] fields appear in the F1 debug panel.
    /// Note: in the Editor, changes made at runtime persist into the asset (that's intentional for tuning).
    /// </summary>
    public abstract class TunableData : ScriptableObject
    {
        [SerializeField, Tooltip("Section title in the F1 debug panel. Empty = asset name.")]
        private string _debugCategory = "";

        public string DebugCategory => string.IsNullOrEmpty(_debugCategory) ? name : _debugCategory;

        protected virtual void OnEnable() => TuningRegistry.Register(this);
        protected virtual void OnDisable() => TuningRegistry.Unregister(this);

        /// <summary>Called by the debug panel after a value is changed.</summary>
        public virtual void OnTuningChanged() { }
    }
}
