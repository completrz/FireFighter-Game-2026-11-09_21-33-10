using System.Collections.Generic;
using Firehouse.Core;
using Firehouse.Player;
using Unity.Netcode;
using UnityEngine;

namespace Firehouse.Fire
{
    /// <summary>
    /// World-level fire API across all <see cref="FireGrid"/>s. The hose (step 5), extinguishers, explosions and
    /// level scripts call these on the **host**. Also owns the F1 "Fire" debug section (ignite/douse at crosshair).
    /// </summary>
    public static class FireSystem
    {
        public static readonly List<FireGrid> Grids = new();

        /// <summary>Host: apply an agent to every cell within <paramref name="radius"/>. Amount is split by distance.</summary>
        public static void ApplyAgentAt(Vector3 worldPos, float radius, AgentDef agent, float amount)
        {
            foreach (var g in Grids)
                if (g.IsSpawned && g.IsServer) g.ApplyAgentAt(worldPos, radius, agent, amount);
        }

        /// <summary>Host: set cells within <paramref name="radius"/> on fire.</summary>
        public static void IgniteAt(Vector3 worldPos, float radius)
        {
            foreach (var g in Grids)
                if (g.IsSpawned && g.IsServer) g.IgniteAt(worldPos, radius);
        }

        /// <summary>Host: add heat to cells near a point (fire spreading between grids, explosions, a lighter).</summary>
        public static void AddHeatAt(Vector3 worldPos, float heat, float radius, FireGrid except = null)
        {
            foreach (var g in Grids)
                if (g != except && g.IsSpawned && g.IsServer && g.MightContain(worldPos, radius)) g.AddHeatNear(worldPos, heat, radius);
        }

        public static int TotalBurning()
        {
            int n = 0;
            foreach (var g in Grids) n += g.BurningCellCount;
            return n;
        }

        /// <summary>Is there fire within <paramref name="radius"/> of a point? (works on every client, from synced state)</summary>
        public static bool IsBurningNear(Vector3 worldPos, float radius)
        {
            foreach (var g in Grids)
                if (g.IsBurningNear(worldPos, radius)) return true;
            return false;
        }

        // ---------------------------------------------------------------- debug (F1)

        private static bool s_sectionAdded;
        private static AgentDef s_debugAgent;

        internal static void EnsureDebugSection(AgentDef debugAgent)
        {
            if (debugAgent != null) s_debugAgent = debugAgent;
            if (s_sectionAdded) return;
            s_sectionAdded = true;
            TuningRegistry.AddSection("Fire", DrawDebug);
        }

        internal static void RemoveDebugSectionIfEmpty()
        {
            if (Grids.Count > 0 || !s_sectionAdded) return;
            s_sectionAdded = false;
            TuningRegistry.RemoveSection("Fire");
        }

        private static void DrawDebug()
        {
            GUILayout.Label($"Grids {Grids.Count} · burning cells {TotalBurning()}");
            var smokie = SmokieController.Local;
            if (smokie == null || Grids.Count == 0) return;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Ignite at crosshair")) Grids[0].RequestDebug(CrosshairPoint(smokie), ignite: true);
            if (GUILayout.Button("Douse at crosshair")) Grids[0].RequestDebug(CrosshairPoint(smokie), ignite: false);
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Reset all fires (host)") && NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
                foreach (var g in Grids) g.ResetSim();
        }

        internal static AgentDef DebugAgent => s_debugAgent;

        private static Vector3 CrosshairPoint(SmokieController s)
        {
            var ray = new Ray(s.LookOrigin, s.LookDirection);
            return Physics.Raycast(ray, out var hit, 40f, ~0, QueryTriggerInteraction.Ignore)
                ? hit.point
                : ray.GetPoint(6f);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Grids.Clear();
            s_sectionAdded = false;
            s_debugAgent = null;
        }
    }
}
