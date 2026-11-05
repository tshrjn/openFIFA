using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor.TeamAI
{
    /// <summary>Off-ball movement: runs into gaps, check runs, midfielders tracking runners.</summary>
    [TestFixture]
    [Category("FID_TEAMAI")]
    public class TeamAIMovementTests
    {
        private static readonly PitchSpec Pitch = PitchSpec.Standard;
        private static float Z(float depth) => TeamAIMath.DepthToZ(depth, 1, Pitch);

        /// <summary>Team 0 attacks +Z: carrier CM at depth 0.6 facing forward; team 1 back four at depth 0.8.</summary>
        private static TeamAIWorld Attack(out int carrier, float[] defenderXs)
        {
            var w = TeamAITestWorlds.Empty();
            TeamAITestWorlds.Add(w, 0, TeamAIRole.GK, 0, 0f, Z(0.02f));
            TeamAITestWorlds.Add(w, 0, TeamAIRole.CB, 1, -8f, Z(0.4f));
            TeamAITestWorlds.Add(w, 0, TeamAIRole.CB, 2, 8f, Z(0.4f));
            TeamAITestWorlds.Add(w, 0, TeamAIRole.DM, 5, 0f, Z(0.5f));
            carrier = TeamAITestWorlds.Add(w, 0, TeamAIRole.CM, 6, 0f, Z(0.6f));
            w.Players[carrier].Facing = new SimVec3(0f, 0f, 1f);
            TeamAITestWorlds.Add(w, 1, TeamAIRole.GK, 0, 0f, Z(0.99f));
            for (int k = 0; k < defenderXs.Length; k++)
                TeamAITestWorlds.Add(w, 1, TeamAIRole.CB, 1 + k, defenderXs[k], Z(0.8f));
            TeamAITestWorlds.GiveBall(w, carrier);
            w.Time = 10f;
            return w;
        }

        [Test]
        public void RunInBehind_AimsAtTheGapBetweenDefenders_NotAtADefender()
        {
            // A striker lined up exactly behind a centre-back (x = 4): the gap is between x = 4 and x = 16.
            var w = Attack(out _, new[] { -16f, -4f, 4f, 16f });
            int st = TeamAITestWorlds.Add(w, 0, TeamAIRole.ST, 10, 4f, Z(0.785f));
            var dir = new TeamAIDirector(Pitch, new TeamAITactics(), new TeamAITactics(), 1f, 1f, 3);
            dir.DecideNow(w, 0.1f);
            var c = dir.Commands[st];
            Assert.AreEqual(TeamAIIntent.RunInBehind, c.Intent);
            float x = c.MoveTarget.X;
            bool inGap = (x > -4f + 2f && x < 4f - 2f) || (x > 4f + 2f && x < 16f - 2f);
            Assert.IsTrue(inGap, $"run target x {x:F1} should be in a gap, at least 2 m from any defender");

            var w2 = Attack(out _, new[] { -16f, -4f, 4f, 16f });
            int st2 = TeamAITestWorlds.Add(w2, 0, TeamAIRole.ST, 10, 4f, Z(0.785f));
            var dir2 = new TeamAIDirector(Pitch, new TeamAITactics(), new TeamAITactics(), 1f, 1f, 3);
            dir2.Brains[0].GapRuns = false;
            dir2.DecideNow(w2, 0.1f);
            Assert.AreEqual(4f * 0.7f, dir2.Commands[st2].MoveTarget.X, 0.5f, "old behaviour: 30% toward the middle");
        }

        [Test]
        public void TightlyMarkedForward_ChecksShortTowardTheBall()
        {
            var w = Attack(out int carrier, new[] { -16f, 16f });
            // Two team-mates near the ball take the support spots.
            TeamAITestWorlds.Add(w, 0, TeamAIRole.CM, 7, -6f, Z(0.62f));
            TeamAITestWorlds.Add(w, 0, TeamAIRole.AM, 8, 5f, Z(0.65f));
            // A winger 20 m from the ball, deep of the offside line, with a marker on his shoulder.
            int wing = TeamAITestWorlds.Add(w, 0, TeamAIRole.W, 9, 18f, Z(0.72f));
            TeamAITestWorlds.Add(w, 1, TeamAIRole.FB, 5, 18.8f, Z(0.735f));
            var dir = new TeamAIDirector(Pitch, new TeamAITactics(), new TeamAITactics(), 1f, 1f, 5);
            dir.DecideNow(w, 0.1f);
            var c = dir.Commands[wing];
            Assert.AreEqual(TeamAIIntent.CheckRun, c.Intent);
            float before = TeamAIMath.FlatDistance(w.Players[wing].Position, w.Players[carrier].Position);
            float after = TeamAIMath.FlatDistance(c.MoveTarget, w.Players[carrier].Position);
            Assert.Less(after, before - 4f, "comes toward the ball");

            var off = Attack(out _, new[] { -16f, 16f });
            TeamAITestWorlds.Add(off, 0, TeamAIRole.CM, 7, -6f, Z(0.62f));
            TeamAITestWorlds.Add(off, 0, TeamAIRole.AM, 8, 5f, Z(0.65f));
            int wing2 = TeamAITestWorlds.Add(off, 0, TeamAIRole.W, 9, 18f, Z(0.72f));
            TeamAITestWorlds.Add(off, 1, TeamAIRole.FB, 5, 18.8f, Z(0.735f));
            var dir2 = new TeamAIDirector(Pitch, new TeamAITactics(), new TeamAITactics(), 1f, 1f, 5);
            dir2.Brains[0].CheckRuns = false;
            dir2.DecideNow(off, 0.1f);
            Assert.AreNotEqual(TeamAIIntent.CheckRun, dir2.Commands[wing2].Intent);
        }

        private static TeamAIWorld WingerScene(out int wing, out int marker, out int carrier)
        {
            var w = Attack(out carrier, new[] { -16f, 16f });
            TeamAITestWorlds.Add(w, 0, TeamAIRole.CM, 7, -6f, Z(0.62f));
            TeamAITestWorlds.Add(w, 0, TeamAIRole.AM, 8, 5f, Z(0.65f));
            wing = TeamAITestWorlds.Add(w, 0, TeamAIRole.W, 9, 18f, Z(0.72f));
            marker = TeamAITestWorlds.Add(w, 1, TeamAIRole.FB, 5, 18.8f, Z(0.735f));
            return w;
        }

        /// <summary>Steps <paramref name="ticks"/> decision ticks of 0.1 s; the winger walks to his target at 4 m/s and the
        /// marker either follows him (keeping his offset) or stays put.</summary>
        private static TeamAIIntent[] RunCheck(bool markerFollows, int ticks, out TeamAIDirector dir)
        {
            var w = WingerScene(out int wing, out int marker, out _);
            dir = new TeamAIDirector(Pitch, new TeamAITactics(), new TeamAITactics(), 1f, 1f, 5);
            var intents = new TeamAIIntent[ticks];
            for (int k = 0; k < ticks; k++)
            {
                dir.DecideNow(w, 0.1f);
                var c = dir.Commands[wing];
                intents[k] = c.Intent;
                var before = w.Players[wing].Position;
                var step = TeamAIMath.FlatDirection(before, c.MoveTarget) * System.Math.Min(0.4f, TeamAIMath.FlatDistance(before, c.MoveTarget));
                w.Players[wing].Position = before + step;
                if (markerFollows) w.Players[marker].Position = w.Players[marker].Position + step;
                w.Time += 0.1f;
            }
            return intents;
        }

        [Test]
        public void CheckRun_LastsItsFullLength()
        {
            var intents = RunCheck(true, 14, out _);
            int n = (int)System.Math.Round(TeamAIBrain.CheckRunSeconds / 0.1f);
            for (int k = 0; k < n; k++) Assert.AreEqual(TeamAIIntent.CheckRun, intents[k], $"tick {k}");
        }

        [Test]
        public void CheckRun_SpinsInBehindOnlyWhenTheMarkerFollows()
        {
            var followed = RunCheck(true, 18, out _);
            var left = RunCheck(false, 18, out _);
            int n = (int)System.Math.Round(TeamAIBrain.CheckRunSeconds / 0.1f);
            bool spun = false, spunAlone = false;
            for (int k = n; k < 18; k++)
            {
                spun |= followed[k] == TeamAIIntent.RunInBehind;
                spunAlone |= left[k] == TeamAIIntent.RunInBehind;
            }
            Assert.IsTrue(spun, "followed by his marker: spins in behind after the check");
            Assert.IsFalse(spunAlone, "left free: no spin (he is the free man to feet)");
        }

        [Test]
        public void MidfielderTracksARunnerBurstingIntoOurHalf_AndKeepsHim()
        {
            // Team 1 attacks -Z with the ball in midfield; its CM sprints into our half past our midfielder.
            var w = TeamAITestWorlds.Empty();
            TeamAITestWorlds.Add(w, 0, TeamAIRole.GK, 0, 0f, Z(0.02f));
            TeamAITestWorlds.Add(w, 0, TeamAIRole.CB, 1, -8f, Z(0.2f));
            TeamAITestWorlds.Add(w, 0, TeamAIRole.CB, 2, 8f, Z(0.2f));
            int cm = TeamAITestWorlds.Add(w, 0, TeamAIRole.CM, 6, 4f, Z(0.42f));
            // Two team-mates close to the carrier are the pressers.
            TeamAITestWorlds.Add(w, 0, TeamAIRole.DM, 5, -9f, Z(0.57f));
            TeamAITestWorlds.Add(w, 0, TeamAIRole.W, 8, -13f, Z(0.58f));
            TeamAITestWorlds.Add(w, 1, TeamAIRole.GK, 0, 0f, Z(0.98f));
            int carrier = TeamAITestWorlds.Add(w, 1, TeamAIRole.CM, 6, -10f, Z(0.6f));
            int runner = TeamAITestWorlds.Add(w, 1, TeamAIRole.CM, 7, 2f, Z(0.47f));
            var runVel = new SimVec3(0f, 0f, -6f); // toward our goal
            w.Players[runner].Velocity = runVel;
            TeamAITestWorlds.GiveBall(w, carrier);
            w.Time = 10f;
            var dir = new TeamAIDirector(Pitch, new TeamAITactics(), new TeamAITactics(), 1f, 1f, 7);
            int firstMark = -1, marked = 0;
            for (int k = 0; k < 14; k++)
            {
                dir.DecideNow(w, 0.1f);
                var c = dir.Commands[cm];
                if (c.Intent == TeamAIIntent.Mark && c.TargetPlayerId == w.Players[runner].Id)
                {
                    if (firstMark < 0) firstMark = k;
                    marked++;
                    Assert.Less(c.MoveTarget.Z, w.Players[runner].Position.Z, "goal-side of the runner");
                    // The tracker keeps pace: he stands where he was told (1.5 m goal-side), which must not release him.
                    w.Players[cm].Position = c.MoveTarget;
                    w.Players[cm].Velocity = runVel;
                }
                else if (firstMark >= 0) Assert.Fail($"tracker let the runner go at tick {k}");
                w.Players[runner].Position = w.Players[runner].Position + runVel * 0.1f;
                w.Time += 0.1f;
            }
            Assert.GreaterOrEqual(firstMark, 0, "the runner was never tracked");
            Assert.Greater(marked, 8);
        }

        [Test]
        public void Tracking_ReleasedWhenTheRunnerStops()
        {
            var w = TeamAITestWorlds.Empty();
            TeamAITestWorlds.Add(w, 0, TeamAIRole.GK, 0, 0f, Z(0.02f));
            TeamAITestWorlds.Add(w, 0, TeamAIRole.CB, 1, -8f, Z(0.2f));
            TeamAITestWorlds.Add(w, 0, TeamAIRole.CB, 2, 8f, Z(0.2f));
            int cm = TeamAITestWorlds.Add(w, 0, TeamAIRole.CM, 6, 4f, Z(0.42f));
            TeamAITestWorlds.Add(w, 0, TeamAIRole.DM, 5, -9f, Z(0.57f));
            TeamAITestWorlds.Add(w, 0, TeamAIRole.W, 8, -13f, Z(0.58f));
            TeamAITestWorlds.Add(w, 1, TeamAIRole.GK, 0, 0f, Z(0.98f));
            int carrier = TeamAITestWorlds.Add(w, 1, TeamAIRole.CM, 6, -10f, Z(0.6f));
            int runner = TeamAITestWorlds.Add(w, 1, TeamAIRole.CM, 7, 2f, Z(0.47f));
            w.Players[runner].Velocity = new SimVec3(0f, 0f, -6f);
            TeamAITestWorlds.GiveBall(w, carrier);
            w.Time = 10f;
            var dir = new TeamAIDirector(Pitch, new TeamAITactics(), new TeamAITactics(), 1f, 1f, 7);
            for (int k = 0; k < 5; k++) { dir.DecideNow(w, 0.1f); w.Time += 0.1f; }
            Assert.AreEqual(TeamAIIntent.Mark, dir.Commands[cm].Intent);
            w.Players[runner].Velocity = new SimVec3(0f, 0f, -1f); // pulls up
            dir.DecideNow(w, 0.1f);
            Assert.AreNotEqual(TeamAIIntent.Mark, dir.Commands[cm].Intent, "released under the release speed");
        }

        [Test]
        public void DirectedRun_KeepsItsLineAfterTheCallExpires()
        {
            var w = Attack(out _, new[] { -16f, -4f, 4f, 16f });
            int st = TeamAITestWorlds.Add(w, 0, TeamAIRole.ST, 10, 4f, Z(0.7f));
            w.Directives[0].RequestRun(w.Players[st].Id, SimVec3.Zero, false, w.Time + 0.15f);
            var dir = new TeamAIDirector(Pitch, new TeamAITactics(), new TeamAITactics(), 1f, 1f, 3);
            dir.DecideNow(w, 0.1f);
            float x0 = dir.Commands[st].MoveTarget.X;
            Assert.AreEqual(TeamAIIntent.RunInBehind, dir.Commands[st].Intent);
            for (int k = 0; k < 4; k++)
            {
                w.Time += 0.1f;
                dir.DecideNow(w, 0.1f);
                Assert.AreEqual(TeamAIIntent.RunInBehind, dir.Commands[st].Intent, $"tick {k}: run continues after the call");
                Assert.AreEqual(x0, dir.Commands[st].MoveTarget.X, 0.01f, $"tick {k}: same line");
            }
        }
    }
}
