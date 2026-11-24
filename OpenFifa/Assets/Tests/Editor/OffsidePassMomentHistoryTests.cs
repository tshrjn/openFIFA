using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    /// <summary>The offside freeze-frame shows the pass moment: the history finds the tick the ball was played.</summary>
    [TestFixture]
    [Category("Offside")]
    public class OffsidePassMomentHistoryTests
    {
        private const int Offender = 20, Passer = 18;
        private const float DefLine = -28.69f, BallLine = -10.5f;

        private static MatchSimState StateAt(int tick, bool flagged, int passer = Passer, float defLine = DefLine)
        {
            var s = new MatchSimState { Tick = tick };
            for (int i = 0; i < MatchSimState.PlayerCount; i++)
                s.Players[i].Position = new SimVec3(i, 0f, -tick * 0.1f); // every tick moves everyone
            s.Players[Offender].Facing = 0.5f;
            s.Ball.Position = new SimVec3(100f + tick, 0.11f, 0f);
            if (flagged)
            {
                s.OffsideMask = 1 << Offender;
                s.OffsidePasser = passer;
                s.OffsideDefenderLineZ = defLine;
                s.OffsideBallLineZ = BallLine;
            }
            else
            {
                s.OffsidePasser = -1;
            }
            return s;
        }

        private static OffsideDecision Decision() =>
            new OffsideDecision(OffsideReason.PlayingBall, TeamIdentifier.TeamB, Offender, Passer, 0f, -29.7f,
                -29.71f, DefLine, BallLine);

        [Test]
        public void TryFindPassMoment_PassThenCall_ReturnsFirstFlaggedTick()
        {
            var h = new OffsidePassMomentHistory();
            for (int t = 1; t <= 5; t++) h.Record(StateAt(t, false));
            for (int t = 6; t <= 40; t++) h.Record(StateAt(t, true));   // ball in flight, offender flagged
            for (int t = 41; t <= 50; t++) h.Record(StateAt(t, false)); // called: the sim cleared it, free kick set up

            Assert.IsTrue(h.TryFindPassMoment(Decision(), out int slot, out int tick));
            Assert.AreEqual(6, tick);
            h.GetPlayer(slot, Offender, out var pos, out float facing, out bool sentOff);
            Assert.AreEqual(-0.6f, pos.Z, 1e-5f);
            Assert.AreEqual(Offender, pos.X, 1e-5f);
            Assert.AreEqual(0.5f, facing, 1e-6f);
            Assert.IsFalse(sentOff);
            Assert.AreEqual(106f, h.GetBall(slot).X, 1e-5f);
        }

        [Test]
        public void TryFindPassMoment_EarlierPassBySameOffender_PicksTheLatestRun()
        {
            var h = new OffsidePassMomentHistory();
            for (int t = 1; t <= 10; t++) h.Record(StateAt(t, true));
            for (int t = 11; t <= 20; t++) h.Record(StateAt(t, false));
            for (int t = 21; t <= 30; t++) h.Record(StateAt(t, true));
            Assert.IsTrue(h.TryFindPassMoment(Decision(), out _, out int tick));
            Assert.AreEqual(21, tick);
        }

        [Test]
        public void TryFindPassMoment_DifferentPassOrLines_NotMatched()
        {
            var h = new OffsidePassMomentHistory();
            for (int t = 1; t <= 10; t++) h.Record(StateAt(t, true, passer: 17));
            for (int t = 11; t <= 20; t++) h.Record(StateAt(t, true, defLine: -12f));
            Assert.IsFalse(h.TryFindPassMoment(Decision(), out int slot, out int tick));
            Assert.AreEqual(-1, slot);
            Assert.AreEqual(-1, tick);
        }

        [Test]
        public void TryFindPassMoment_PassBeforeHistoryStart_ReturnsOldestFlaggedTick()
        {
            var h = new OffsidePassMomentHistory(capacity: 8);
            for (int t = 1; t <= 30; t++) h.Record(StateAt(t, true));
            Assert.AreEqual(8, h.Count);
            Assert.IsTrue(h.TryFindPassMoment(Decision(), out _, out int tick));
            Assert.AreEqual(23, tick, "oldest entry still held");
        }

        [Test]
        public void Record_SameTickTwice_StoredOnce_AndEarlierTickRestarts()
        {
            var h = new OffsidePassMomentHistory();
            h.Record(StateAt(5, false));
            h.Record(StateAt(5, false));
            Assert.AreEqual(1, h.Count);
            h.Record(StateAt(6, false));
            h.Record(StateAt(2, false)); // new match
            Assert.AreEqual(1, h.Count);
        }

        [Test]
        public void TryFindPassMoment_NoOffenceOrEmpty_False()
        {
            var h = new OffsidePassMomentHistory();
            Assert.IsFalse(h.TryFindPassMoment(Decision(), out _, out _));
            h.Record(StateAt(1, true));
            Assert.IsFalse(h.TryFindPassMoment(OffsideDecision.None, out _, out _));
            Assert.IsFalse(h.TryFindPassMoment(null, out _, out _));
        }
    }
}
