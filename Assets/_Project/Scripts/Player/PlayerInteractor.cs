using Firehouse.Core;
using Firehouse.Truck;
using Unity.Netcode;
using UnityEngine;

namespace Firehouse.Player
{
    /// <summary>
    /// Local player: finds the <see cref="Interactable"/> you're looking at (closest to the crosshair, in range),
    /// shows "E: prompt", and uses it on Interact. While seated, Interact leaves the seat.
    /// </summary>
    [RequireComponent(typeof(SmokieController))]
    public sealed class PlayerInteractor : NetworkBehaviour
    {
        private SmokieController _smokie;
        private Interactable _focus;
        private string _seatedPrompt;

        public Interactable Focus => _focus;

        private void Awake() => _smokie = GetComponent<SmokieController>();

        private void Update()
        {
            if (!IsSpawned || !IsOwner || _smokie.Input == null) return;
            bool blocked = UIFocus.IsBlocked;

            if (_smokie.State == SmokieState.Seated)
            {
                _focus = null;
                var seat = TruckSeats.FindSeatOf(OwnerClientId, out var seats);
                _seatedPrompt = seat >= 0 ? (seat == 0 ? "E: get out   ·   H horn · F siren · L lights · R flip" : "E: get out") : null;
                if (!blocked && seats != null && _smokie.Input.Interact.WasPressedThisFrame())
                    seats.RequestLeave(_smokie);
                return;
            }

            _seatedPrompt = null;
            _focus = blocked || _smokie.State == SmokieState.KnockedDown ? null : FindBest();
            if (_focus != null && _smokie.Input.Interact.WasPressedThisFrame())
                _focus.Interact(_smokie);
        }

        private Interactable FindBest()
        {
            var origin = _smokie.LookOrigin;
            var dir = _smokie.LookDirection;
            Interactable best = null;
            float bestScore = float.MaxValue;
            foreach (var it in Interactable.All)
            {
                if (it == null || !it.isActiveAndEnabled) continue;
                var to = it.InteractPoint - origin;
                float dist = to.magnitude;
                if (dist > it.Range) continue;
                float aim = Vector3.Dot(dir, to / Mathf.Max(dist, 0.001f));
                if (aim < 0.55f) continue;
                if (!it.CanInteract(_smokie)) continue;
                float score = dist * (1.6f - aim);
                if (score < bestScore) { bestScore = score; best = it; }
            }
            return best;
        }

        private void OnGUI()
        {
            if (!IsSpawned || !IsOwner || UIFocus.IsBlocked) return;
            string text = _seatedPrompt ?? (_focus != null ? $"E: {_focus.GetPrompt(_smokie)}" : null);
            if (text == null) return;
            var size = GUI.skin.label.CalcSize(new GUIContent(text));
            float y = _seatedPrompt != null ? Screen.height - 60 : Screen.height * 0.5f + 36;
            GUI.Label(new Rect((Screen.width - size.x) * 0.5f, y, size.x, size.y), text);
        }
    }
}
