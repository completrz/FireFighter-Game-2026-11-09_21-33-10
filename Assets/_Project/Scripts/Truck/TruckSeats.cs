using System.Collections.Generic;
using Firehouse.Player;
using Unity.Netcode;
using UnityEngine;

namespace Firehouse.Truck
{
    /// <summary>
    /// Who sits where on a truck. The host owns the occupancy list. Taking seat 0 (driver) hands the truck's
    /// ownership to that player so their client simulates it with no input lag; leaving gives it back to the host.
    /// </summary>
    public sealed class TruckSeats : NetworkBehaviour
    {
        public const ulong Empty = ulong.MaxValue;
        public static readonly List<TruckSeats> All = new();

        [SerializeField] private TruckSeat[] _seats;

        private readonly NetworkList<ulong> _occupants = new();
        private TruckController _truck;

        public IReadOnlyList<TruckSeat> Seats => _seats;
        public TruckController Truck => _truck;

        private void Awake()
        {
            _truck = GetComponent<TruckController>();
            for (int i = 0; i < _seats.Length; i++)
            {
                _seats[i].Index = i;
                _seats[i].Owner = this;
            }
        }

        private void OnEnable() => All.Add(this);
        private void OnDisable() => All.Remove(this);

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                _occupants.Clear();
                for (int i = 0; i < _seats.Length; i++) _occupants.Add(Empty);
                NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && NetworkManager != null) NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        }

        public bool IsFree(int index) => index >= 0 && index < _occupants.Count && _occupants[index] == Empty;
        public ulong OccupantOf(int index) => index >= 0 && index < _occupants.Count ? _occupants[index] : Empty;

        public int SeatIndexOf(ulong clientId)
        {
            for (int i = 0; i < _occupants.Count; i++)
                if (_occupants[i] == clientId) return i;
            return -1;
        }

        /// <summary>Finds which truck/seat a player is in. Returns -1 if not seated.</summary>
        public static int FindSeatOf(ulong clientId, out TruckSeats seats)
        {
            foreach (var s in All)
            {
                if (!s.IsSpawned) continue;
                int i = s.SeatIndexOf(clientId);
                if (i >= 0) { seats = s; return i; }
            }
            seats = null;
            return -1;
        }

        // ---------------------------------------------------------------- client requests

        public void RequestSeat(int index) => TakeSeatRpc(index);

        /// <summary>Called by the seated player's client. Passes the truck's motion so a moving truck keeps rolling.</summary>
        public void RequestLeave(SmokieController who)
        {
            var rb = _truck.Body;
            LeaveSeatRpc(rb.linearVelocity, rb.angularVelocity);
        }

        [Rpc(SendTo.Server)]
        private void TakeSeatRpc(int index, RpcParams rpc = default)
        {
            ulong sender = rpc.Receive.SenderClientId;
            if (!IsFree(index) || FindSeatOf(sender, out _) >= 0) return;
            var player = NetworkManager.SpawnManager.GetPlayerNetworkObject(sender);
            var smokie = player != null ? player.GetComponent<SmokieController>() : null;
            if (smokie == null || smokie.State == SmokieState.KnockedDown) return;

            _occupants[index] = sender;
            if (_seats[index].Kind == SeatKind.Driver && OwnerClientId != sender)
                _truck.HandOwnership(sender, _truck.Body.linearVelocity, _truck.Body.angularVelocity);
        }

        [Rpc(SendTo.Server)]
        private void LeaveSeatRpc(Vector3 velocity, Vector3 angularVelocity, RpcParams rpc = default)
        {
            int i = SeatIndexOf(rpc.Receive.SenderClientId);
            if (i >= 0) FreeSeat(i, velocity, angularVelocity);
        }

        /// <summary>Truck owner → host: throw a back-step rider off with this velocity.</summary>
        public void Eject(int index, Vector3 launchVelocity) => EjectRpc(index, launchVelocity);

        [Rpc(SendTo.Server)]
        private void EjectRpc(int index, Vector3 launchVelocity)
        {
            ulong who = OccupantOf(index);
            if (who == Empty) return;
            FreeSeat(index, _truck.Body.linearVelocity, _truck.Body.angularVelocity);
            var player = NetworkManager.SpawnManager.GetPlayerNetworkObject(who);
            if (player != null) player.GetComponent<SmokieController>()?.Knockback(launchVelocity, forceKnockdown: true);
        }

        private void FreeSeat(int index, Vector3 velocity, Vector3 angularVelocity)
        {
            _occupants[index] = Empty;
            if (_seats[index].Kind == SeatKind.Driver && OwnerClientId != NetworkManager.ServerClientId)
                _truck.HandOwnership(NetworkManager.ServerClientId, velocity, angularVelocity);
        }

        private void OnClientDisconnected(ulong clientId)
        {
            int i = SeatIndexOf(clientId);
            if (i >= 0) FreeSeat(i, Vector3.zero, Vector3.zero);
        }
    }
}
