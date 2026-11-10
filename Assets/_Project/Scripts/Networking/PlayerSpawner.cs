using Unity.Netcode;
using UnityEngine;

namespace Firehouse.Networking
{
    /// <summary>
    /// Scene object (not networked). On the host, spawns a player object for every connected client that doesn't
    /// have one yet, at this scene's <see cref="SpawnPoint"/>s. Player objects are destroyed with the scene, so each
    /// scene's spawner places the crew fresh.
    /// </summary>
    public sealed class PlayerSpawner : MonoBehaviour
    {
        private SpawnPoint[] _points;
        private int _next;

        private void OnEnable()
        {
            _points = FindObjectsByType<SpawnPoint>(FindObjectsInactive.Exclude);
            var nm = NetworkManager.Singleton;
            if (nm == null) return;
            nm.OnServerStarted += SpawnAll;
            nm.OnClientConnectedCallback += OnClientConnected;
            if (nm.IsServer) SpawnAll();
        }

        private void OnDisable()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return;
            nm.OnServerStarted -= SpawnAll;
            nm.OnClientConnectedCallback -= OnClientConnected;
        }

        private void SpawnAll()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer) return;
            foreach (var id in nm.ConnectedClientsIds)
                SpawnFor(id);
        }

        private void OnClientConnected(ulong clientId)
        {
            if (NetworkManager.Singleton.IsServer) SpawnFor(clientId);
        }

        private void SpawnFor(ulong clientId)
        {
            var nm = NetworkManager.Singleton;
            if (nm.SpawnManager.GetPlayerNetworkObject(clientId) != null) return;
            var prefab = SessionManager.Instance != null ? SessionManager.Instance.PlayerPrefab : null;
            if (prefab == null)
            {
                Debug.LogError("[PlayerSpawner] No player prefab set on SessionManager.");
                return;
            }

            Vector3 pos = transform.position;
            Quaternion rot = transform.rotation;
            if (_points != null && _points.Length > 0)
            {
                var p = _points[_next++ % _points.Length];
                pos = p.transform.position;
                rot = p.transform.rotation;
            }

            var instance = Instantiate(prefab, pos, rot);
            instance.SpawnAsPlayerObject(clientId, destroyWithScene: true);
        }
    }
}
