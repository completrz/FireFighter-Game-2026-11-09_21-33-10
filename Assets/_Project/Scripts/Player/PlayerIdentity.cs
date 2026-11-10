using Firehouse.Lobby;
using Steamworks;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Firehouse.Player
{
    /// <summary>
    /// Name, helmet number (1–8, host-assigned) and coat color for a crew member.
    /// Tints every renderer listed in <see cref="_coatRenderers"/> with the coat color.
    /// </summary>
    public sealed class PlayerIdentity : NetworkBehaviour
    {
        public static readonly Color[] CoatColors =
        {
            new(0.86f, 0.62f, 0.12f), // 1 classic tan/yellow
            new(0.80f, 0.20f, 0.16f), // 2 red
            new(0.20f, 0.42f, 0.80f), // 3 blue
            new(0.22f, 0.62f, 0.30f), // 4 green
            new(0.55f, 0.30f, 0.75f), // 5 purple
            new(0.95f, 0.50f, 0.15f), // 6 orange
            new(0.15f, 0.65f, 0.68f), // 7 teal
            new(0.92f, 0.45f, 0.62f), // 8 pink
        };

        [SerializeField] private Renderer[] _coatRenderers;

        private readonly NetworkVariable<FixedString64Bytes> _displayName =
            new("", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private readonly NetworkVariable<byte> _helmetNumber = new(0);

        public string DisplayName => _displayName.Value.ToString();
        public int HelmetNumber => _helmetNumber.Value;
        public Color CoatColor => HelmetNumber > 0 ? CoatColors[(HelmetNumber - 1) % CoatColors.Length] : Color.white;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private MaterialPropertyBlock _mpb;

        public override void OnNetworkSpawn()
        {
            if (IsServer) _helmetNumber.Value = PickFreeHelmetNumber();
            if (IsOwner)
            {
                string name = SteamService.IsReady ? SteamClient.Name : $"Smokie {OwnerClientId}";
                _displayName.Value = new FixedString64Bytes(name.Length > 30 ? name.Substring(0, 30) : name);
            }
            _helmetNumber.OnValueChanged += (_, _) => ApplyColor();
            ApplyColor();
        }

        private byte PickFreeHelmetNumber()
        {
            var used = new bool[9];
            foreach (var p in FindObjectsByType<PlayerIdentity>(FindObjectsInactive.Exclude))
                if (p != this && p.IsSpawned) used[p.HelmetNumber] = true;
            for (byte i = 1; i <= 8; i++)
                if (!used[i]) return i;
            return (byte)(OwnerClientId % 8 + 1);
        }

        private void ApplyColor()
        {
            _mpb ??= new MaterialPropertyBlock();
            foreach (var r in _coatRenderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor(BaseColorId, CoatColor);
                r.SetPropertyBlock(_mpb);
            }
        }

        // Temporary name tags for testing; replaced by in-world helmet numbers later.
        private void OnGUI()
        {
            if (!IsSpawned || IsOwner) return;
            var cam = Camera.main;
            if (cam == null) return;
            var world = transform.position + Vector3.up * 2.1f;
            var sp = cam.WorldToScreenPoint(world);
            if (sp.z <= 0 || sp.z > 30f) return;
            var label = $"#{HelmetNumber} {DisplayName}";
            var size = GUI.skin.label.CalcSize(new GUIContent(label));
            GUI.Label(new Rect(sp.x - size.x * 0.5f, Screen.height - sp.y - size.y, size.x, size.y), label);
        }
    }
}
