using Unity.Netcode;
using UnityEngine;

namespace Firehouse.Player
{
    /// <summary>
    /// Turns on the first-person camera and AudioListener only for the local player, and turns the scene's
    /// menu camera off while that player exists.
    /// </summary>
    public sealed class LocalPlayerCamera : NetworkBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private AudioListener _listener;
        [SerializeField, Tooltip("Renderers hidden from the owner's own camera (e.g. the head, so it doesn't block the view).")]
        private Renderer[] _hideForOwner;

        private Camera _sceneCamera;

        public Camera Camera => _camera;

        public override void OnNetworkSpawn()
        {
            bool mine = IsOwner;
            _camera.enabled = mine;
            _listener.enabled = mine;
            if (!mine) return;

            _camera.tag = "MainCamera";
            foreach (var cam in FindObjectsByType<Camera>(FindObjectsInactive.Exclude))
            {
                if (cam == _camera || !cam.CompareTag("MainCamera")) continue;
                _sceneCamera = cam;
                cam.gameObject.SetActive(false);
            }
            foreach (var r in _hideForOwner)
                if (r != null) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
        }

        public override void OnNetworkDespawn()
        {
            if (_sceneCamera != null) _sceneCamera.gameObject.SetActive(true);
        }
    }
}
