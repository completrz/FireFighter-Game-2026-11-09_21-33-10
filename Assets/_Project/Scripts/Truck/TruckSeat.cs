using Firehouse.Player;
using UnityEngine;

namespace Firehouse.Truck
{
    public enum SeatKind : byte { Driver, Cab, BackStep }

    /// <summary>
    /// One place to ride on a truck. The transform is where the player's feet go (forward = facing).
    /// Interacting with it asks the host for the seat. Back-step riders stand outside and can be thrown off.
    /// </summary>
    public sealed class TruckSeat : Interactable
    {
        [SerializeField] private SeatKind _kind;
        [SerializeField] private string _label = "Ride";
        [SerializeField, Tooltip("Where the player is put when they get out (local to this seat).")]
        private Vector3 _exitOffset = new(-1.6f, 0f, 0f);

        public SeatKind Kind => _kind;
        public int Index { get; set; }
        public TruckSeats Owner { get; set; }
        public Vector3 ExitPoint => transform.TransformPoint(_exitOffset);

        public override Vector3 InteractPoint => transform.position + Vector3.up * 1.0f;

        public override string GetPrompt(SmokieController who) => _label;

        public override bool CanInteract(SmokieController who) =>
            Owner != null && Owner.IsSpawned && Owner.IsFree(Index);

        public override void Interact(SmokieController who) => Owner.RequestSeat(Index);

        private void OnDrawGizmos()
        {
            Gizmos.color = _kind == SeatKind.Driver ? Color.green : _kind == SeatKind.Cab ? Color.cyan : Color.yellow;
            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.8f, new Vector3(0.5f, 1.6f, 0.5f));
            Gizmos.DrawLine(transform.position + Vector3.up, transform.position + Vector3.up + transform.forward * 0.6f);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.TransformPoint(_exitOffset), 0.2f);
        }
    }
}
