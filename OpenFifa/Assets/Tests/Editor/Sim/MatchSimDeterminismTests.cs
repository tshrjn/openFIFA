using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor.Sim
{
    [TestFixture]
    [Category("FID-SIM")]
    public class MatchSimDeterminismTests
    {
        private static ulong RunBots(ulong seed, int ticks, out int events, out int goals)
        {
            var sim = SimTestHelpers.NewSim(seed);
            var intents = SimTestHelpers.IdleIntents();
            events = 0;
            goals = 0;
            ulong eventHash = 1469598103934665603UL;
            for (int t = 0; t < ticks; t++)
            {
                SimTestHelpers.BotIntents(sim, intents);
                var ev = sim.Step(intents);
                events += ev.Count;
                for (int i = 0; i < ev.Count; i++)
                {
                    if (ev[i].Type == MatchEventType.Goal) goals++;
                    eventHash = unchecked((eventHash ^ (ulong)((int)ev[i].Type * 31 + ev[i].Tick)) * 1099511628211UL);
                }
            }

            return sim.ComputeStateHash() ^ eventHash;
        }

        [Test]
        public void Step_SameSeedSameIntents_10000Ticks_BitIdentical()
        {
            ulong a = RunBots(12345, 10000, out int ea, out _);
            ulong b = RunBots(12345, 10000, out int eb, out _);
            Assert.AreEqual(a, b);
            Assert.AreEqual(ea, eb);
            Assert.Greater(ea, 100, "the bot match should actually produce play");
        }

        [Test]
        public void Step_DifferentSeed_Diverges()
        {
            Assert.AreNotEqual(RunBots(1, 4000, out _, out _), RunBots(2, 4000, out _, out _));
        }

        [Test]
        public void RestoreSnapshot_ReplayFromSnapshot_ReproducesState()
        {
            var sim = SimTestHelpers.NewSim(77);
            var intents = SimTestHelpers.IdleIntents();
            for (int t = 0; t < 2000; t++) { SimTestHelpers.BotIntents(sim, intents); sim.Step(intents); }
            var snap = sim.CaptureSnapshot();
            ulong snapHash = sim.ComputeStateHash();
            for (int t = 0; t < 3000; t++) { SimTestHelpers.BotIntents(sim, intents); sim.Step(intents); }
            ulong first = sim.ComputeStateHash();

            sim.RestoreSnapshot(snap);
            Assert.AreEqual(snapHash, sim.ComputeStateHash());
            for (int t = 0; t < 3000; t++) { SimTestHelpers.BotIntents(sim, intents); sim.Step(intents); }
            Assert.AreEqual(first, sim.ComputeStateHash());
        }

        [Test]
        public void CaptureSnapshot_IsDeepCopy()
        {
            var sim = SimTestHelpers.NewSim(5);
            var snap = sim.CaptureSnapshot();
            var intents = SimTestHelpers.IdleIntents();
            for (int t = 0; t < 600; t++) { SimTestHelpers.BotIntents(sim, intents); sim.Step(intents); }
            Assert.AreNotEqual(snap.Players[3].Position, sim.State.Players[3].Position);
            Assert.AreEqual(0, snap.Tick);
        }

        [Test]
        public void Step_BotSoak_InvariantsHold()
        {
            var sim = SimTestHelpers.NewSim(2024);
            var intents = SimTestHelpers.IdleIntents();
            int home = 0, away = 0, shots = 0;
            var pitch = sim.Config.Pitch;
            for (int t = 0; t < 20000 && sim.Phase != SimMatchPhase.FullTime; t++)
            {
                SimTestHelpers.BotIntents(sim, intents);
                foreach (var e in sim.Step(intents))
                {
                    if (e.Type == MatchEventType.Goal) { if (e.Team == SimTeam.Home) home++; else away++; }
                    if (e.Type == MatchEventType.Shot) { shots++; Assert.That(e.Value, Is.InRange(0f, 1f)); }
                }

                var b = sim.State.Ball.Position;
                Assert.IsFalse(float.IsNaN(b.X) || float.IsNaN(b.Y) || float.IsNaN(b.Z), $"NaN ball at tick {t}");
                Assert.Less(SimMath.Abs(b.X), pitch.HalfWidth + 15f, $"ball far out at tick {t}");
                Assert.Less(SimMath.Abs(b.Z), pitch.HalfLength + 15f, $"ball far out at tick {t}");
                Assert.GreaterOrEqual(b.Y, 0f);
            }

            Assert.AreEqual(home, sim.State.HomeScore);
            Assert.AreEqual(away, sim.State.AwayScore);
            Assert.Greater(shots, 0, "bots should get shots away in 20000 ticks");
        }
    }
}
