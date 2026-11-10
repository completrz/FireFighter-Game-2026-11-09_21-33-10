using Firehouse.Core;
using UnityEngine;

namespace Firehouse.Fire
{
    /// <summary>An extinguishing agent (water, foam, CO2, dry chemical, sand, wet chemical).</summary>
    [CreateAssetMenu(menuName = "Firehouse/Agent", fileName = "Agent_")]
    public sealed class AgentDef : TunableData
    {
        [Tunable(0, 600)] public float Cooling = 160f;  // heat removed per unit
        [Tunable(0, 3)] public float Wetting = 1f;      // wetness added per unit (blocks re-ignition)
        [Tooltip("Multiplier per FireClass (index = (int)FireClass).")]
        public float[] Effectiveness = { 1f, 0.2f, 0.4f, 0f, 0f, 0f };
        [Tooltip("Side effect per FireClass when this agent is a bad choice.")]
        public WrongAgentEffect[] WrongEffect =
        {
            WrongAgentEffect.None, WrongAgentEffect.SpreadsLiquid, WrongAgentEffect.ShocksHolder,
            WrongAgentEffect.Fireball, WrongAgentEffect.Explosion, WrongAgentEffect.RaisesRadiation
        };

        public float EffectivenessFor(FireClass c) =>
            Effectiveness != null && (int)c < Effectiveness.Length ? Effectiveness[(int)c] : 1f;
    }
}
