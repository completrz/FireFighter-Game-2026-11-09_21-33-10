using System;

namespace Firehouse.Core
{
    /// <summary>
    /// Marks a float/int/bool field on a <see cref="TunableData"/> asset so it shows up as a slider
    /// in the F1 debug panel. Use alongside [SerializeField] so the value is also editable in the Inspector.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class TunableAttribute : Attribute
    {
        public readonly float Min;
        public readonly float Max;

        public TunableAttribute(float min = 0f, float max = 1f)
        {
            Min = min;
            Max = max;
        }
    }
}
