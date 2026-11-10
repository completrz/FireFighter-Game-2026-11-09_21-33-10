using System.Collections.Generic;
using Firehouse.Fire;
using NUnit.Framework;
using UnityEngine;

namespace Firehouse.Tests
{
    public class FireGridSimTests
    {
        private FireMaterialDef _wood, _grass;
        private FireSettings _settings;
        private AgentDef _water;

        [SetUp]
        public void SetUp()
        {
            _wood = ScriptableObject.CreateInstance<FireMaterialDef>();
            _wood.IgnitionTemp = 300; _wood.Fuel = 60; _wood.BurnRate = 1; _wood.HeatOutput = 120;
            _grass = ScriptableObject.CreateInstance<FireMaterialDef>();
            _grass.IgnitionTemp = 160; _grass.Fuel = 6; _grass.BurnRate = 1; _grass.HeatOutput = 300; _grass.RemoveWhenBurntOut = false;
            _settings = ScriptableObject.CreateInstance<FireSettings>();
            _settings.Randomness = 0f;
            _settings.WindStrength = 0f;
            _water = ScriptableObject.CreateInstance<AgentDef>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_wood);
            Object.DestroyImmediate(_grass);
            Object.DestroyImmediate(_settings);
            Object.DestroyImmediate(_water);
        }

        private FireGridSim Line(int length, FireMaterialDef mat)
        {
            var m = new byte[length];
            for (int i = 0; i < length; i++) m[i] = 1;
            return new FireGridSim(length, 1, 1, m, new[] { mat }, _settings);
        }

        private static void Run(FireGridSim sim, float seconds, float dt = 0.1f)
        {
            for (float t = 0; t < seconds; t += dt) sim.Step(dt);
        }

        [Test]
        public void Fire_SpreadsToNeighbours()
        {
            var sim = Line(5, _grass);
            sim.Ignite(0);
            Run(sim, 3f);
            Assert.IsTrue(sim.Burning[1] || sim.Fuel[1] < _grass.Fuel, "neighbour should have caught within 3 s");
            Run(sim, 6f);
            Assert.Less(sim.Fuel[4], _grass.Fuel, "grass fire should run down the whole 5-cell line in under 9 s");
        }

        [Test]
        public void EmptyCells_StopSpread()
        {
            var m = new byte[] { 1, 0, 0, 0, 1 };
            var sim = new FireGridSim(5, 1, 1, m, new[] { _grass }, _settings);
            sim.Ignite(0);
            Run(sim, 10f);
            Assert.AreEqual(_grass.Fuel, sim.Fuel[4], 1e-4f, "fire must not jump a 3-cell gap");
        }

        [Test]
        public void Fire_BurnsOut()
        {
            var sim = Line(1, _grass);
            sim.Ignite(0);
            Run(sim, _grass.Fuel / _grass.BurnRate + 1f);
            Assert.IsFalse(sim.Burning[0]);
            Assert.AreEqual(0f, sim.Fuel[0]);
            Assert.AreEqual(0, sim.BurningCount);
        }

        [Test]
        public void Water_PutsFireOut_AndWetCellsResistIgnition()
        {
            var sim = Line(2, _wood);
            sim.Ignite(0);
            sim.ApplyAgent(0, _water, 10f);
            Assert.IsFalse(sim.Burning[0]);
            Assert.Greater(sim.Wet[0], 0f);

            // A soaked cell next to a fresh fire takes noticeably longer to catch than a dry one.
            var dry = Line(2, _wood);
            var wet = Line(2, _wood);
            wet.ApplyAgent(1, _water, 5f);
            dry.Ignite(0);
            wet.Ignite(0);
            float tDry = TimeToIgnite(dry, 1), tWet = TimeToIgnite(wet, 1);
            Assert.Greater(tWet, tDry);
        }

        private static float TimeToIgnite(FireGridSim sim, int cell)
        {
            for (float t = 0; t < 60f; t += 0.1f)
            {
                sim.Step(0.1f);
                if (sim.Burning[cell]) return t;
            }
            return 999f;
        }

        [Test]
        public void Fire_ClimbsFasterThanItFalls()
        {
            // Column of 3: ignite the middle, the top should catch before the bottom.
            var m = new byte[] { 1, 1, 1 };
            var sim = new FireGridSim(1, 3, 1, m, new[] { _wood }, _settings);
            sim.Ignite(1);
            float up = -1, down = -1;
            for (float t = 0; t < 30f && (up < 0 || down < 0); t += 0.1f)
            {
                sim.Step(0.1f);
                if (up < 0 && sim.Burning[2]) up = t;
                if (down < 0 && sim.Burning[0]) down = t;
            }
            Assert.Greater(up, -1f);
            Assert.IsTrue(down < 0 || down > up);
        }

        [Test]
        public void DirtyTracking_ReportsChangedCellsOnce()
        {
            var sim = Line(3, _wood);
            var dirty = new List<int>();
            sim.CollectDirty(dirty);
            dirty.Clear();
            sim.Ignite(1);
            sim.CollectDirty(dirty);
            CollectionAssert.AreEqual(new[] { 1 }, dirty);
            dirty.Clear();
            sim.CollectDirty(dirty);
            Assert.IsEmpty(dirty);
        }

        [Test]
        public void WrongAgentOnGrease_DoesNothing()
        {
            var grease = ScriptableObject.CreateInstance<FireMaterialDef>();
            grease.Class = FireClass.Grease;
            var sim = Line(1, grease);
            sim.Ignite(0);
            float eff = sim.ApplyAgent(0, _water, 10f);
            Assert.AreEqual(0f, eff);
            Assert.IsTrue(sim.Burning[0]);
            Object.DestroyImmediate(grease);
        }
    }

    public class FireSyncCodecTests
    {
        [Test]
        public void Delta_RoundTrips()
        {
            var states = new byte[2000];
            var changed = new List<int> { 3, 4, 150, 1999 };
            foreach (var i in changed) states[i] = (byte)(i % 63 + 1);
            var data = FireSyncCodec.EncodeDelta(changed, states);
            var target = new byte[2000];
            var got = new List<int>();
            Assert.IsTrue(FireSyncCodec.ApplyDelta(data, target, got));
            CollectionAssert.AreEqual(changed, got);
            foreach (var i in changed) Assert.AreEqual(states[i], target[i]);
            Assert.LessOrEqual(data.Length, 1 + changed.Count * 3, "should be ~2-3 bytes per changed cell");
        }

        [Test]
        public void Snapshot_RoundTrips_AndCompresses()
        {
            var states = new byte[900];
            for (int i = 100; i < 140; i++) states[i] = 0b101;
            states[899] = 7;
            var data = FireSyncCodec.EncodeSnapshot(states);
            var target = new byte[900];
            Assert.IsTrue(FireSyncCodec.ApplySnapshot(data, target));
            CollectionAssert.AreEqual(states, target);
            Assert.Less(data.Length, 40);
        }

        [Test]
        public void Delta_RejectsGarbage()
        {
            Assert.IsFalse(FireSyncCodec.ApplyDelta(new byte[] { 5, 200 }, new byte[10]));
        }
    }
}
