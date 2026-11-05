using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor.TeamAI
{
    /// <summary>
    /// Off-ball tempo: shape holders walk small corrections and jog shifts, supporters jog, sprints stay with pressing,
    /// chasing, runs in behind and urgent defensive shifts.
    /// </summary>
    [TestFixture]
    [Category("FID_TEAMAI")]
    public class TeamAITempoTests
    {
        private static TeamAIWorld World(out TeamAIPlayer p)
        {
            var w = TeamAITestWorlds.Empty();
            int i = TeamAITestWorlds.Add(w, 0, TeamAIRole.CM, 5, 0f, 0f);
            w.Ball.Position = new SimVec3(0f, 0.11f, 15f); // ball 15 m ahead, in the opponent half for team 0
            w.Ball.Velocity = SimVec3.Zero;
            p = w.Players[i];
            return w;
        }

        [Test]
        public void ShapeSpeed_SmallCorrection_IsAWalk()
        {
            var w = World(out var p);
            bool sprint = false;
            float v = TeamAITempo.ShapeSpeed(w, 0, p, new SimVec3(1.2f, 0f, 0f), SimVec3.Zero, TeamAIPhase.BuildUp, 0.5f, false, ref sprint);
            Assert.LessOrEqual(v, 2f, "a 1 m correction should be walked");
            Assert.IsFalse(sprint);
        }

        [Test]
        public void ShapeSpeed_LongerShifts_JogNeverRun()
        {
            var w = World(out var p);
            float prev = 0f;
            for (float d = 1f; d <= 40f; d += 1f)
            {
                bool sprint = true;
                float v = TeamAITempo.ShapeSpeed(w, 0, p, new SimVec3(d, 0f, 0f), SimVec3.Zero, TeamAIPhase.BuildUp, 1f, false, ref sprint);
                Assert.GreaterOrEqual(v, prev - 1e-4f, "tempo should not drop as the spot gets further");
                Assert.LessOrEqual(v, TeamAITempo.Jog * TeamAITempo.DifficultyFactor(1f) + 1e-3f, $"{d} m shift is a run");
                Assert.Less(v, 4.5f, $"{d} m shift is a run");
                Assert.IsFalse(sprint, "no sprint for a shape shift in possession");
                prev = v;
            }
        }

        [Test]
        public void ShapeSpeed_MovingSpot_NeverFallsBehind()
        {
            var w = World(out var p);
            bool sprint = false;
            float v = TeamAITempo.ShapeSpeed(w, 0, p, new SimVec3(0.5f, 0f, 0f), new SimVec3(3f, 0f, 0f), TeamAIPhase.MidBlock, 0f, false, ref sprint);
            Assert.GreaterOrEqual(v, 3f + TeamAITempo.TrackMargin - 1e-4f);
        }

        [Test]
        public void ShapeSpeed_UrgentDefensiveShift_KeepsTheSprint()
        {
            var w = World(out var p);
            w.Ball.Position = new SimVec3(0f, 0.11f, -30f); // deep in our half
            bool sprint = true;
            TeamAITempo.ShapeSpeed(w, 0, p, new SimVec3(0f, 0f, -25f), SimVec3.Zero, TeamAIPhase.LowBlock, 0.5f, false, ref sprint);
            Assert.IsTrue(sprint, "a 25 m recovery toward our goal with the ball there is urgent");
        }

        [Test]
        public void SupportSpeed_IsAJog()
        {
            Assert.That(TeamAITempo.SupportSpeed(3f, 0.5f), Is.InRange(2.5f, 4f));
            Assert.LessOrEqual(TeamAITempo.SupportSpeed(30f, 0.5f), 5.5f + 1e-3f, "support is a jog, not a sprint");
            Assert.Less(TeamAITempo.SupportSpeed(30f, 0f), TeamAITempo.SupportSpeed(30f, 1f), "a stronger side supports quicker");
        }

        [Test]
        public void TempoThrottle_TurnsTheLimitIntoTheSimJogThrottle()
        {
            var cmd = AIPlayerCommand.Idle(0, SimVec3.Zero);
            Assert.AreEqual(1f, SimTeamAIAdapter.TempoThrottle(cmd), 1e-6f, "no limit, full jog");
            cmd.SpeedLimit = 2f;
            cmd.SprintSpeed = 8f;
            Assert.AreEqual(2f / (8f * SimTeamAIAdapter.MaxJogFraction), SimTeamAIAdapter.TempoThrottle(cmd), 1e-5f);
        }

        [Test]
        public void Brain_HoldersAndSupporters_CarryATempoLimit_PressersDoNot()
        {
            var w = TeamAITestWorlds.FullMatch(new TeamAITactics(), new TeamAITactics());
            TeamAITestWorlds.GiveBall(w, 6);
            var dir = new TeamAIDirector(PitchSpec.Standard, new TeamAITactics(), new TeamAITactics(), 1f, 1f, 3);
            dir.SetPossession(0);
            w.Time = 2f;
            dir.DecideNow(w, 0.1f);
            int limited = 0;
            for (int i = 0; i < w.PlayerCount; i++)
            {
                var c = dir.Commands[i];
                switch (c.Intent)
                {
                    case TeamAIIntent.HoldShape:
                    case TeamAIIntent.HoldWidth:
                    case TeamAIIntent.KeepLine:
                    case TeamAIIntent.Support:
                        Assert.Greater(c.SpeedLimit, 0f, $"player {i} ({c.Intent}) has no tempo");
                        Assert.Greater(c.SprintSpeed, 0f);
                        limited++;
                        break;
                    case TeamAIIntent.Press:
                    case TeamAIIntent.ChaseBall:
                    case TeamAIIntent.RunInBehind:
                    case TeamAIIntent.Recover:
                        Assert.AreEqual(0f, c.SpeedLimit, $"player {i} ({c.Intent}) must not be slowed");
                        break;
                }
            }
            Assert.GreaterOrEqual(limited, 10);
        }
    }
}
