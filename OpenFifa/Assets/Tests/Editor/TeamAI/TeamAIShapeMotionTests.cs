using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor.TeamAI
{
    /// <summary>
    /// Off-ball shape motion: holders keep walking small adjustments and slide with the block (never a frozen spot),
    /// and team-mates of the carrier give him room (support from 8-15 m on different lines).
    /// </summary>
    [TestFixture]
    [Category("FID_TEAMAI")]
    public class TeamAIShapeMotionTests
    {
        [Test]
        public void Adjustment_EveryPlayerAnyTime_SpeedStaysAWalk()
        {
            float min = float.MaxValue, max = 0f, maxOff = 0f;
            for (int id = 0; id < 22; id++)
            {
                for (int k = 0; k < 1200; k++)
                {
                    float t = k * 0.05f;
                    TeamAIShapeMotion.Adjustment(id, t, false, (id & 1) == 0, out var off, out var vel);
                    float s = vel.Magnitude;
                    if (s < min) min = s;
                    if (s > max) max = s;
                    if (off.Magnitude > maxOff) maxOff = off.Magnitude;
                }
            }
            Assert.Greater(min, SimTeamAIAdapter.MovingTargetSpeed, "drift speed fell to a stop (the adapter would park him)");
            Assert.Less(max, 1.5f, "drift is a walk, not a run");
            Assert.Less(maxOff, 1.8f, "drift strays too far from the spot");
        }

        [Test]
        public void Adjustment_VelocityIsTheDerivativeOfTheOffset()
        {
            const float h = 1e-3f;
            for (int id = 0; id < 22; id += 5)
            {
                TeamAIShapeMotion.Adjustment(id, 10f, false, false, out var a, out var v);
                TeamAIShapeMotion.Adjustment(id, 10f + h, false, false, out var b, out _);
                var fd = (b - a) * (1f / h);
                Assert.Less((fd - v).Magnitude, 0.02f, $"player {id}");
            }
        }

        [Test]
        public void Adjustment_SameInputs_SameOutput_DifferentPlayers_Differ()
        {
            TeamAIShapeMotion.Adjustment(7, 33.3f, false, false, out var a1, out var v1);
            TeamAIShapeMotion.Adjustment(7, 33.3f, false, false, out var a2, out var v2);
            Assert.AreEqual(a1.X, a2.X);
            Assert.AreEqual(v1.Z, v2.Z);
            TeamAIShapeMotion.Adjustment(8, 33.3f, false, false, out var b, out _);
            Assert.Greater((a1 - b).Magnitude, 0.01f, "two players drifting in lockstep");
        }

        [Test]
        public void Adjustment_LineKeeper_DepthDriftStaysSmall()
        {
            float maxZ = 0f;
            for (int k = 0; k < 2000; k++)
            {
                TeamAIShapeMotion.Adjustment(3, k * 0.03f, true, false, out var off, out _);
                maxZ = System.Math.Max(maxZ, System.Math.Abs(off.Z));
            }
            Assert.LessOrEqual(maxZ, TeamAIShapeMotion.LineDepthAmplitude + 1e-4f, "the back line must stay level");
        }

        private static TeamAIDirector Director() =>
            new TeamAIDirector(PitchSpec.Standard, new TeamAITactics(), new TeamAITactics(), 1f, 1f, 7);

        [Test]
        public void Brain_HoldingPlayers_TargetMovesEveryDecisionAndCarriesItsVelocity()
        {
            // Ball loose and still in the centre circle: before, holders got a fixed spot and stood once within 1.2 m of it.
            var w = TeamAITestWorlds.FullMatch(new TeamAITactics(), new TeamAITactics());
            int carrier = 6; // team 0 central midfielder area
            TeamAITestWorlds.GiveBall(w, carrier);
            var dir = Director();
            dir.DecideNow(w, 0.1f);
            var before = new SimVec3[w.PlayerCount];
            for (int i = 0; i < w.PlayerCount; i++) before[i] = dir.Commands[i].MoveTarget;
            w.Time += 0.1f;
            dir.DecideNow(w, 0.1f);
            int holders = 0;
            for (int i = 0; i < w.PlayerCount; i++)
            {
                var c = dir.Commands[i];
                if (c.Intent != TeamAIIntent.HoldShape && c.Intent != TeamAIIntent.HoldWidth && c.Intent != TeamAIIntent.KeepLine) continue;
                holders++;
                Assert.Greater(TeamAIMath.FlatDistance(before[i], c.MoveTarget), 0.02f, $"player {i} spot did not move");
                // Line players share the line's depth drift and may pause at the end of their lateral swing.
                if (c.Intent != TeamAIIntent.KeepLine)
                    Assert.Greater(c.MoveTargetVelocity.Magnitude, SimTeamAIAdapter.MovingTargetSpeed, $"player {i} target has no velocity");
            }
            Assert.GreaterOrEqual(holders, 8);
        }

        [Test]
        public void Brain_BallTravelsAcross_AnchorsSlideWithIt()
        {
            var w = TeamAITestWorlds.FullMatch(new TeamAITactics(), new TeamAITactics());
            w.Ball.Position = new SimVec3(0f, 0.11f, 0f);
            w.Ball.Velocity = new SimVec3(8f, 0f, 0f); // a cross-field pass toward +X, nobody on it
            w.Ball.LastTouchTeam = 0;
            var dir = Director();
            for (int k = 0; k < 6; k++)
            {
                dir.DecideNow(w, 0.1f);
                w.Time += 0.1f;
                w.Ball.Position += w.Ball.Velocity * 0.1f;
            }
            for (int team = 0; team < 2; team++)
            {
                var b = dir.Brains[team];
                for (int s = 1; s < TeamAIFormations.SlotCount; s++)
                    Assert.Greater(b.AnchorVelocities[s].X, 0.5f, $"team {team} slot {s} does not slide toward the ball");
            }
        }

        [Test]
        public void Brain_CarrierInMidfield_HoldersAndSupportersKeepEightMetres()
        {
            for (int c = 1; c < 11; c++)
            {
                var w = TeamAITestWorlds.FullMatch(new TeamAITactics(), new TeamAITactics());
                // Pull two team-mates right onto the carrier: their spots must still be 8 m out.
                var cp = w.Players[c].Position;
                for (int j = 1; j < 11; j++)
                    if (j != c && j <= 3) w.Players[j].Position = cp + new SimVec3(1.5f * j - 3f, 0f, 1f);
                TeamAITestWorlds.GiveBall(w, c);
                var dir = Director();
                dir.SetPossession(0);
                dir.DecideNow(w, 0.1f);
                for (int j = 1; j < 11; j++)
                {
                    if (j == c) continue;
                    var cmd = dir.Commands[j];
                    if (cmd.Intent != TeamAIIntent.HoldShape && cmd.Intent != TeamAIIntent.HoldWidth && cmd.Intent != TeamAIIntent.Support)
                        continue;
                    float d = TeamAIMath.FlatDistance(cmd.MoveTarget, w.Players[c].Position);
                    Assert.GreaterOrEqual(d, TeamAIBrain.CarrierSpacing - 0.05f, $"carrier {c}: team-mate {j} ({cmd.Intent}) told to stand {d:F1} m away");
                }
            }
        }

        [Test]
        public void Brain_TwoSupporters_TakeDifferentLines()
        {
            int checkedCases = 0;
            for (int c = 4; c < 11; c++)
            {
                var w = TeamAITestWorlds.FullMatch(new TeamAITactics(), new TeamAITactics());
                TeamAITestWorlds.GiveBall(w, c);
                var dir = new TeamAIDirector(PitchSpec.Standard, new TeamAITactics(), new TeamAITactics(), 1f, 1f, 11);
                dir.SetPossession(0);
                w.Time = 2f; // past the reception delay
                dir.DecideNow(w, 0.1f);
                int a = -1, b = -1;
                for (int j = 0; j < 11; j++)
                {
                    if (dir.Commands[j].Intent != TeamAIIntent.Support) continue;
                    if (a < 0) a = j; else b = j;
                }
                if (a < 0 || b < 0) continue;
                checkedCases++;
                var o = w.Players[c].Position;
                var da = TeamAIMath.FlatDirection(o, dir.Commands[a].MoveTarget);
                var db = TeamAIMath.FlatDirection(o, dir.Commands[b].MoveTarget);
                Assert.Less(SimVec3.Dot(da, db), 0.65f, $"carrier {c}: both supporters on one line");
                Assert.GreaterOrEqual(TeamAIMath.FlatDistance(dir.Commands[a].MoveTarget, dir.Commands[b].MoveTarget), 7.9f);
            }
            Assert.Greater(checkedCases, 2, "too few two-supporter cases to check");
        }
    }
}
