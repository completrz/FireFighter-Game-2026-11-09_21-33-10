using Firehouse.Player;
using UnityEngine;

namespace Firehouse.Water
{
    /// <summary>
    /// The pump panel on the side of the truck: E steps the pressure 0 → 25 → 50 → 75 → 100 % → 0.
    /// The prompt doubles as the gauge (pressure + tank level). A physical gauge mesh comes later.
    /// </summary>
    public sealed class PumpPanel : Interactable
    {
        [SerializeField] private TruckWater _water;

        public override string GetPrompt(SmokieController who)
        {
            int next = Mathf.RoundToInt(HoseStats.PressureSteps[(System.Array.IndexOf(HoseStats.PressureSteps, _water.Pressure) + 1) % HoseStats.PressureSteps.Length] * 100f);
            return $"Pump {_water.Pressure * 100f:0}% → {next}%   (tank {_water.Tank01 * 100f:0}%)";
        }

        public override bool CanInteract(SmokieController who) => _water != null && _water.IsSpawned;

        public override void Interact(SmokieController who) => _water.RequestCyclePressure();
    }
}
