// Adapted from the community Facepunch transport for Netcode for GameObjects
// (Unity-Technologies/multiplayer-community-contributions, MIT).
// Changes for Firehouse:
//  - Steam's lifetime (Init / RunCallbacks / Shutdown) is owned by Firehouse.Lobby.SteamService,
//    not by the transport, because lobbies and voice keep using Steam after a network session ends.
//  - Target host is set from code (SteamLobbyService) rather than the inspector.
//  - Removed inspector-only helpers.
using System;
using System.Collections.Generic;
using Steamworks;
using Steamworks.Data;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Netcode;
using UnityEngine;

namespace Firehouse.Networking.Transport
{
    using SocketConnection = Connection;

    public sealed class FacepunchTransport : NetworkTransport, IConnectionManager, ISocketManager
    {
        private ConnectionManager _connectionManager;
        private SocketManager _socketManager;
        private Dictionary<ulong, Client> _connectedClients = new();
        private bool _relayInitialized;
        private byte[] _payloadCache = new byte[4096];

        /// <summary>The Steam ID of the host to connect to when starting as a client.</summary>
        public ulong TargetSteamId { get; set; }

        private static LogLevel LogLevel => NetworkManager.Singleton != null ? NetworkManager.Singleton.LogLevel : LogLevel.Normal;

        private sealed class Client
        {
            public SteamId SteamId;
            public SocketConnection Connection;
        }

        public override ulong ServerClientId => 0;

        public override void Initialize(NetworkManager networkManager = null)
        {
            _connectedClients = new Dictionary<ulong, Client>();
            if (!SteamClient.IsValid)
                Debug.LogError("[FacepunchTransport] Steam is not initialized. Start SteamService before using the Steam transport.");
        }

        protected override void OnEarlyUpdate()
        {
            if (!_relayInitialized && SteamClient.IsValid)
            {
                _relayInitialized = true;
                SteamNetworkingUtils.InitRelayNetworkAccess();
            }
        }

        public override bool StartClient()
        {
            if (TargetSteamId == 0)
            {
                Debug.LogError("[FacepunchTransport] No target Steam ID set.");
                return false;
            }
            _connectionManager = SteamNetworkingSockets.ConnectRelay<ConnectionManager>(TargetSteamId);
            _connectionManager.Interface = this;
            return true;
        }

        public override bool StartServer()
        {
            _socketManager = SteamNetworkingSockets.CreateRelaySocket<SocketManager>();
            _socketManager.Interface = this;
            return true;
        }

        public override void DisconnectLocalClient() => _connectionManager?.Connection.Close();

        public override void DisconnectRemoteClient(ulong clientId)
        {
            if (_connectedClients.TryGetValue(clientId, out var user))
            {
                user.Connection.Flush();
                user.Connection.Close();
                _connectedClients.Remove(clientId);
            }
        }

        public override ulong GetCurrentRtt(ulong clientId) => 0;

        public override void Shutdown()
        {
            try
            {
                _connectionManager?.Close();
                _socketManager?.Close();
            }
            catch (Exception e)
            {
                Debug.LogError($"[FacepunchTransport] Exception while shutting down: {e}");
            }
            _connectionManager = null;
            _socketManager = null;
            _connectedClients.Clear();
        }

        private static SendType ToSendType(NetworkDelivery delivery) => delivery switch
        {
            NetworkDelivery.Unreliable => SendType.Unreliable,
            NetworkDelivery.UnreliableSequenced => SendType.Unreliable,
            _ => SendType.Reliable
        };

        public override void Send(ulong clientId, ArraySegment<byte> data, NetworkDelivery delivery)
        {
            var sendType = ToSendType(delivery);
            if (clientId == ServerClientId && _connectionManager != null)
                _connectionManager.Connection.SendMessage(data.Array, data.Offset, data.Count, sendType);
            else if (_connectedClients.TryGetValue(clientId, out var user))
                user.Connection.SendMessage(data.Array, data.Offset, data.Count, sendType);
            else if (LogLevel <= LogLevel.Normal)
                Debug.LogWarning($"[FacepunchTransport] Can't send to client {clientId}: not connected.");
        }

        public override NetworkEvent PollEvent(out ulong clientId, out ArraySegment<byte> payload, out float receiveTime)
        {
            // Receiving dispatches events through InvokeOnTransportEvent.
            _connectionManager?.Receive();
            _socketManager?.Receive();
            clientId = 0;
            receiveTime = Time.realtimeSinceStartup;
            payload = default;
            return NetworkEvent.Nothing;
        }

        private void EnsurePayloadCapacity(int size)
        {
            if (_payloadCache.Length < size)
                _payloadCache = new byte[Math.Max(_payloadCache.Length * 2, size)];
        }

        // ---- Client side (ConnectionManager) ----

        void IConnectionManager.OnConnecting(ConnectionInfo info) { }

        void IConnectionManager.OnConnected(ConnectionInfo info) =>
            InvokeOnTransportEvent(NetworkEvent.Connect, ServerClientId, default, Time.realtimeSinceStartup);

        void IConnectionManager.OnDisconnected(ConnectionInfo info) =>
            InvokeOnTransportEvent(NetworkEvent.Disconnect, ServerClientId, default, Time.realtimeSinceStartup);

        unsafe void IConnectionManager.OnMessage(IntPtr data, int size, long messageNum, long recvTime, int channel)
        {
            EnsurePayloadCapacity(size);
            fixed (byte* payload = _payloadCache)
                UnsafeUtility.MemCpy(payload, (byte*)data, size);
            InvokeOnTransportEvent(NetworkEvent.Data, ServerClientId, new ArraySegment<byte>(_payloadCache, 0, size), Time.realtimeSinceStartup);
        }

        // ---- Host side (SocketManager) ----

        void ISocketManager.OnConnecting(SocketConnection connection, ConnectionInfo info) => connection.Accept();

        void ISocketManager.OnConnected(SocketConnection connection, ConnectionInfo info)
        {
            if (_connectedClients.ContainsKey(connection.Id)) return;
            _connectedClients.Add(connection.Id, new Client { Connection = connection, SteamId = info.Identity.SteamId });
            InvokeOnTransportEvent(NetworkEvent.Connect, connection.Id, default, Time.realtimeSinceStartup);
        }

        void ISocketManager.OnDisconnected(SocketConnection connection, ConnectionInfo info)
        {
            if (_connectedClients.Remove(connection.Id))
                InvokeOnTransportEvent(NetworkEvent.Disconnect, connection.Id, default, Time.realtimeSinceStartup);
        }

        unsafe void ISocketManager.OnMessage(SocketConnection connection, NetIdentity identity, IntPtr data, int size, long messageNum, long recvTime, int channel)
        {
            EnsurePayloadCapacity(size);
            fixed (byte* payload = _payloadCache)
                UnsafeUtility.MemCpy(payload, (byte*)data, size);
            InvokeOnTransportEvent(NetworkEvent.Data, connection.Id, new ArraySegment<byte>(_payloadCache, 0, size), Time.realtimeSinceStartup);
        }

        /// <summary>Steam ID of a connected client (host side), 0 if unknown.</summary>
        public ulong GetSteamIdForClient(ulong transportClientId) =>
            _connectedClients.TryGetValue(transportClientId, out var c) ? c.SteamId.Value : 0UL;
    }
}
