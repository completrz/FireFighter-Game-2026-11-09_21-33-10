using System.Collections.Generic;
using UnityEngine;

namespace Firehouse.Player
{
    /// <summary>
    /// Something the local player can use with Interact (E): truck seats, the pump panel, later doors, valves, the radio...
    /// <see cref="PlayerInteractor"/> picks the best one in front of the player and shows its prompt.
    /// </summary>
    public abstract class Interactable : MonoBehaviour
    {
        public static readonly List<Interactable> All = new();

        [SerializeField] private float _range = 2.4f;

        public float Range => _range;
        public virtual Vector3 InteractPoint => transform.position;

        protected virtual void OnEnable() => All.Add(this);
        protected virtual void OnDisable() => All.Remove(this);

        /// <summary>Short prompt, e.g. "Drive". Shown as "E: Drive".</summary>
        public abstract string GetPrompt(SmokieController who);
        public abstract bool CanInteract(SmokieController who);
        /// <summary>Called on the interacting player's client.</summary>
        public abstract void Interact(SmokieController who);
    }
}
