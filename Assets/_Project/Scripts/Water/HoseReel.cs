using Firehouse.Player;
using UnityEngine;

namespace Firehouse.Water
{
    /// <summary>The hose reel on the back of the truck: E takes the hose (if nobody has it).</summary>
    public sealed class HoseReel : Interactable
    {
        [SerializeField] private TruckWater _water;

        public override string GetPrompt(SmokieController who) => "Take the hose";

        public override bool CanInteract(SmokieController who) =>
            _water != null && _water.IsSpawned && !_water.HasHolder && who.State == SmokieState.Walking;

        public override void Interact(SmokieController who) => _water.RequestTakeHose();
    }
}
