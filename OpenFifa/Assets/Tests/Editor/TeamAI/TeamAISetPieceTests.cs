using System;
using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor.TeamAI
{
    /// <summary>Review items 10 and 15: keeper on his line for a penalty, corner / penalty / free-kick routines.</summary>
    [TestFixture]
    [Category("FID-TEAMAI")]
    public class TeamAISetPieceTests
    {
        private static readonly PitchSpec Pitch = PitchSpec.Standard;

        private static int First(TeamAIWorld w, int team, TeamAIRole role)
        {
            for (int i = 0; i < w.PlayerCount; i++) if (w.Players[i].Team == team && w.Players[i].Role == role) return i;
            return -1;
        }

        /// <summary>Home (team 0, attacks +Z) has a restart at <paramref name="spot"/>; <paramref name="taker"/> owns the ball.</summary>
        private static TeamAIWorld Restart(TeamAIMatchPhase phase, SimVec3 spot, TeamAIRole takerRole, out int taker, int restartTeam = 0)
        {
            var w = TeamAITestWorlds.FullMatch(new TeamAITactics(), new TeamAITactics());
            taker = First(w, restartTeam, takerRole);
            w.Players[taker].Position = new SimVec3(spot.X, 0f, spot.Z - w.AttackSign(restartTeam) * 0.5f);
            w.Ball.Position = new SimVec3(spot.X, 0.11f, spot.Z);
            w.Ball.Velocity = SimVec3.Zero;
            w.Ball.OwnerId = w.Players[taker].Id;
            w.Ball.LastTouchTeam = restartTeam;
            w.MatchPhase = phase;
            w.RestartTeam = restartTeam;
            return w;
        }

        private static TeamAIDirector Dir(float theta = 1f, ulong seed = 42) =>
            new TeamAIDirector(Pitch, new TeamAITactics(), new TeamAITactics(), theta, theta, seed);

        /// <summary>Decide, then walk every player toward his target (no ball physics). Returns the tick a kick was ordered, or -1.</summary>
        private static int Run(TeamAIDirector dir, TeamAIWorld w, int ticks, int watch = -1)
        {
            for (int k = 0; k < ticks; k++)
            {
                dir.DecideNow(w, 0.1f);
                if (watch >= 0 && dir.Commands[watch].Action != TeamAIAction.None) return k;
                for (int i = 0; i < w.PlayerCount; i++)
                {
                    var c = dir.Commands[i];
                    var to = c.MoveTarget - w.Players[i].Position;
                    float d = TeamAIMath.FlatDistance(c.MoveTarget, w.Players[i].Position);
                    float step = Math.Min(d, w.Players[i].MaxSpeed * 0.1f);
                    if (d > 1e-3f) w.Players[i].Position += new SimVec3(to.X / d * step, 0f, to.Z / d * step);
                }
                w.Time += 0.1f;
            }
            return -1;
        }

        private static float Dz(SimVec3 p, float goalZ) => Math.Abs(goalZ - p.Z);

        // ------------------------------------------------------------ penalties

        [Test]
        public void Penalty_Against_KeeperStaysOnHisLineUntilTheKick()
        {
            // Away penalty at the home goal (-Z): home keeper defends it.
            var spot = new SimVec3(0f, 0f, -Pitch.HalfLength + 11f);
            var w = Restart(TeamAIMatchPhase.Penalty, spot, TeamAIRole.ST, out _, restartTeam: 1);
            int gk = First(w, 0, TeamAIRole.GK);
            w.Players[gk].Position = new SimVec3(0f, 0f, -Pitch.HalfLength + 0.3f);
            var dir = Dir();
            for (int k = 0; k < 20; k++)
            {
                dir.DecideNow(w, 0.1f);
                var t = dir.Commands[gk].MoveTarget;
                Assert.Less(Dz(t, -Pitch.HalfLength), 0.35f, $"tick {k}: keeper target off his line");
                Assert.Less(Math.Abs(t.X), Pitch.GoalWidth * 0.5f);
                Assert.IsFalse(dir.Commands[gk].Sprint);
                w.Time += 0.1f;
            }
            // Also through the plain keeper path (no set-piece routine).
            var cmd = AIPlayerCommand.Idle(gk, w.Players[gk].Position);
            TeamAIGoalkeeper.Decide(w, gk, 0.2f, false, ref cmd);
            Assert.Less(Dz(cmd.MoveTarget, -Pitch.HalfLength), 0.35f);

            // After the kick the ball is in play and the keeper is free to come for it.
            w.MatchPhase = TeamAIMatchPhase.InPlay;
            w.RestartTeam = -1;
            w.Ball.OwnerId = -1;
            w.Ball.Position = new SimVec3(0f, 0.11f, -Pitch.HalfLength + 6f);
            w.Ball.Velocity = SimVec3.Zero;
            dir.DecideNow(w, 0.1f);
            Assert.Greater(Dz(dir.Commands[gk].MoveTarget, -Pitch.HalfLength), 0.5f, "after the kick the keeper leaves the line");
        }

        [Test]
        public void Penalty_EveryoneElseOutsideTheBoxAndArc()
        {
            var spot = new SimVec3(0f, 0f, Pitch.HalfLength - 11f);
            var w = Restart(TeamAIMatchPhase.Penalty, spot, TeamAIRole.ST, out int taker);
            var dir = Dir();
            dir.DecideNow(w, 0.1f);
            for (int i = 0; i < w.PlayerCount; i++)
            {
                if (i == taker || w.Players[i].Role == TeamAIRole.GK) continue;
                var t = dir.Commands[i].MoveTarget;
                bool inBox = Dz(t, Pitch.HalfLength) < Pitch.PenaltyAreaDepth && Math.Abs(t.X) < Pitch.PenaltyAreaWidth * 0.5f;
                Assert.IsFalse(inBox, $"player {i} (team {w.Players[i].Team}) inside the box");
                Assert.GreaterOrEqual(TeamAIMath.FlatDistance(t, spot), TeamAIBrain.FreeKickDistance, $"player {i} inside the arc");
            }
            Assert.AreEqual(TeamAISetPieceKind.PenaltyFor, dir.Brains[0].SetPieces.Kind);
            Assert.AreEqual(TeamAISetPieceKind.PenaltyAgainst, dir.Brains[1].SetPieces.Kind);
        }

        private static int PenaltyAimSide(ulong seed, float keeperX, float theta)
        {
            var spot = new SimVec3(0f, 0f, Pitch.HalfLength - 11f);
            var w = Restart(TeamAIMatchPhase.Penalty, spot, TeamAIRole.ST, out int taker);
            w.Players[First(w, 1, TeamAIRole.GK)].Position = new SimVec3(keeperX, 0f, Pitch.HalfLength - 0.2f);
            var dir = Dir(theta, seed);
            int k = Run(dir, w, 40, taker);
            Assert.GreaterOrEqual(k, 0, "the taker should shoot");
            var c = dir.Commands[taker];
            Assert.AreEqual(TeamAIIntent.Shoot, c.Intent);
            return Math.Abs(c.ActionTarget.X) < 0.5f ? 0 : (c.ActionTarget.X > 0f ? 1 : -1);
        }

        [Test]
        public void Penalty_Taker_ShootsAwayFromTheKeepersLeanWithNoise()
        {
            int away = 0, toward = 0;
            for (ulong s = 1; s <= 40; s++)
            {
                int side = PenaltyAimSide(s, 0.7f, 1f);
                if (side < 0) away++; else if (side > 0) toward++;
            }
            Assert.Greater(away, 30, "keeper leaning +X: most kicks go -X");
            int left = 0, right = 0;
            for (ulong s = 1; s <= 40; s++)
            {
                int side = PenaltyAimSide(s, 0f, 0.3f);
                if (side < 0) left++; else if (side > 0) right++;
            }
            Assert.Greater(left, 8, "centred keeper: both sides get picked");
            Assert.Greater(right, 8, "centred keeper: both sides get picked");
        }

        // ------------------------------------------------------------ corners

        private static TeamAIWorld CornerFor(out int taker) =>
            Restart(TeamAIMatchPhase.Corner, new SimVec3(Pitch.HalfWidth - 0.5f, 0f, Pitch.HalfLength - 0.5f), TeamAIRole.W, out taker);

        private static int CountRole(TeamAISetPieces sp, TeamAIWorld w, TeamAISetPieceRole r)
        {
            int n = 0;
            for (int i = 0; i < w.PlayerCount; i++) if (sp.Roles[i] == r) n++;
            return n;
        }

        [Test]
        public void Corner_For_FillsNearFarCentralEdgeAndShortWithRestDefence()
        {
            var w = CornerFor(out int taker);
            var dir = Dir();
            dir.DecideNow(w, 0.1f);
            var sp = dir.Brains[0].SetPieces;
            Assert.AreEqual(TeamAISetPieceKind.CornerFor, sp.Kind);
            foreach (var r in new[] { TeamAISetPieceRole.NearPost, TeamAISetPieceRole.FarPost, TeamAISetPieceRole.Central,
                         TeamAISetPieceRole.EdgeOfBox, TeamAISetPieceRole.ShortOption })
                Assert.AreEqual(1, CountRole(sp, w, r), r.ToString());
            Assert.GreaterOrEqual(CountRole(sp, w, TeamAISetPieceRole.RestDefence), 2);
            float g = Pitch.HalfLength;
            for (int i = 0; i < w.PlayerCount; i++)
            {
                var t = sp.Targets[i];
                switch (sp.Roles[i])
                {
                    case TeamAISetPieceRole.NearPost: Assert.Greater(t.X, 0f); Assert.Less(Dz(t, g), 7f); break;
                    case TeamAISetPieceRole.FarPost: Assert.Less(t.X, 0f); Assert.Less(Dz(t, g), 8f); break;
                    case TeamAISetPieceRole.EdgeOfBox: Assert.AreEqual(Pitch.PenaltyAreaDepth, Dz(t, g), 3f); break;
                    case TeamAISetPieceRole.ShortOption: Assert.Less(TeamAIMath.FlatDistance(t, w.Ball.Position), 10f); break;
                    case TeamAISetPieceRole.RestDefence: Assert.Greater(Dz(t, g), 40f); break;
                }
            }
            // Centre-backs go up for the corner.
            Assert.AreNotEqual(TeamAISetPieceRole.RestDefence, sp.Roles[First(w, 0, TeamAIRole.CB)]);
        }

        [Test]
        public void Corner_For_TakerWaitsForTheRoutineThenDeliversToABoxRunner()
        {
            var w = CornerFor(out int taker);
            var dir = Dir();
            int k = Run(dir, w, 90, taker);
            Assert.GreaterOrEqual(k, (int)(TeamAISetPieces.MinSettle / 0.1f) - 1, "taker must wait for the runners");
            var c = dir.Commands[taker];
            Assert.IsTrue(c.Action == TeamAIAction.Cross || c.Action == TeamAIAction.Pass, c.Action.ToString());
            int rec = w.IndexOfId(c.TargetPlayerId);
            Assert.GreaterOrEqual(rec, 0);
            var role = dir.Brains[0].SetPieces.Roles[rec];
            Assert.IsTrue(role == TeamAISetPieceRole.NearPost || role == TeamAISetPieceRole.FarPost ||
                          role == TeamAISetPieceRole.Central || role == TeamAISetPieceRole.EdgeOfBox ||
                          role == TeamAISetPieceRole.ShortOption, role.ToString());
        }

        [Test]
        public void Corner_For_DeliveryVariesAcrossSeeds()
        {
            var seen = new bool[32];
            int kinds = 0;
            for (ulong s = 1; s <= 30; s++)
            {
                var w = CornerFor(out int taker);
                var dir = Dir(0.8f, s);
                Run(dir, w, 90, taker);
                int rec = w.IndexOfId(dir.Commands[taker].TargetPlayerId);
                if (rec < 0) continue;
                int r = (int)dir.Brains[0].SetPieces.Roles[rec];
                if (!seen[r]) { seen[r] = true; kinds++; }
            }
            Assert.GreaterOrEqual(kinds, 3, "corners should not always go to the same spot");
        }

        [Test]
        public void Corner_Against_ZonalPlusManMarkingGoalSide()
        {
            var w = CornerFor(out _);
            var dir = Dir();
            Run(dir, w, 90);
            var sp = dir.Brains[1].SetPieces;
            Assert.AreEqual(TeamAISetPieceKind.CornerAgainst, sp.Kind);
            Assert.AreEqual(1, CountRole(sp, w, TeamAISetPieceRole.ZonalPost));
            Assert.AreEqual(3, CountRole(sp, w, TeamAISetPieceRole.ZonalSixYard));
            Assert.AreEqual(1, CountRole(sp, w, TeamAISetPieceRole.Outlet));
            int markers = 0;
            var goal = new SimVec3(0f, 0f, Pitch.HalfLength);
            for (int i = 0; i < w.PlayerCount; i++)
            {
                if (sp.Roles[i] != TeamAISetPieceRole.Marker) continue;
                markers++;
                int opp = sp.MarkIndex[i];
                Assert.GreaterOrEqual(opp, 0);
                Assert.AreEqual(0, w.Players[opp].Team);
                Assert.Less(TeamAIMath.FlatDistance(sp.Targets[i], goal), TeamAIMath.FlatDistance(w.Players[opp].Position, goal),
                    "marker should be goal-side");
                Assert.AreEqual(TeamAIIntent.Mark, dir.Commands[i].Intent);
            }
            Assert.GreaterOrEqual(markers, 3, "attackers in the box get man-marked");
            int gk = First(w, 1, TeamAIRole.GK);
            Assert.Less(Dz(dir.Commands[gk].MoveTarget, Pitch.HalfLength), 1.5f);
        }

        // ------------------------------------------------------------ free kicks

        [Test]
        public void FreeKick_WallSize_ShrinksWithDistance()
        {
            Assert.AreEqual(5, TeamAISetPieces.WallSizeFor(17f, false));
            Assert.AreEqual(4, TeamAISetPieces.WallSizeFor(21f, false));
            Assert.AreEqual(3, TeamAISetPieces.WallSizeFor(25f, false));
            Assert.AreEqual(2, TeamAISetPieces.WallSizeFor(29f, false));
            Assert.AreEqual(2, TeamAISetPieces.WallSizeFor(17f, true));
        }

        [Test]
        public void FreeKick_Against_WallNineMetresOutCoveringTheNearPost()
        {
            var spot = new SimVec3(6f, 0f, Pitch.HalfLength - 19f);
            var w = Restart(TeamAIMatchPhase.FreeKick, spot, TeamAIRole.CM, out _);
            var dir = Dir();
            dir.DecideNow(w, 0.1f);
            var sp = dir.Brains[1].SetPieces;
            Assert.AreEqual(TeamAISetPieceKind.FreeKickAgainst, sp.Kind);
            float dist = TeamAIMath.FlatDistance(spot, new SimVec3(0f, 0f, Pitch.HalfLength));
            Assert.AreEqual(TeamAISetPieces.WallSizeFor(dist, false), sp.WallSize);
            Assert.AreEqual(sp.WallSize, CountRole(sp, w, TeamAISetPieceRole.Wall));
            var nearPost = new SimVec3(Pitch.GoalWidth * 0.5f, 0f, Pitch.HalfLength);
            float closestToPostLine = 99f;
            for (int i = 0; i < w.PlayerCount; i++)
            {
                if (sp.Roles[i] != TeamAISetPieceRole.Wall) continue;
                Assert.AreEqual(9.15f, TeamAIMath.FlatDistance(sp.Targets[i], spot), 0.6f, "wall at 9.15 m");
                closestToPostLine = Math.Min(closestToPostLine, TeamAIMath.DistanceToLine(spot, nearPost, sp.Targets[i]));
            }
            Assert.Less(closestToPostLine, 0.6f, "the wall covers the near-post line");
            for (int i = 0; i < w.PlayerCount; i++)
                if (w.Players[i].Team == 1 && w.Players[i].Role != TeamAIRole.GK)
                    Assert.GreaterOrEqual(TeamAIMath.FlatDistance(sp.Targets[i], spot), 9.15f, $"defender {i} too close");
        }

        private static TeamAIAction FreeKickChoice(SimVec3 spot, bool indirect, ulong seed)
        {
            var w = Restart(TeamAIMatchPhase.FreeKick, spot, TeamAIRole.CM, out int taker);
            w.IndirectFreeKick = indirect;
            var dir = Dir(1f, seed);
            int k = Run(dir, w, 90, taker);
            Assert.GreaterOrEqual(k, 0, "taker should take the free kick");
            return dir.Commands[taker].Action;
        }

        private static bool IsShot(TeamAIAction a) =>
            a == TeamAIAction.Shoot || a == TeamAIAction.FinesseShot || a == TeamAIAction.DrivenShot;

        [Test]
        public void FreeKick_For_CentralAndCloseMostlyShoots_IndirectOrWideCrosses()
        {
            var central = new SimVec3(1f, 0f, Pitch.HalfLength - 20f);
            var wide = new SimVec3(26f, 0f, Pitch.HalfLength - 14f);
            int shots = 0, indirectShots = 0, wideShots = 0;
            for (ulong s = 1; s <= 20; s++)
            {
                if (IsShot(FreeKickChoice(central, false, s))) shots++;
                if (IsShot(FreeKickChoice(central, true, s))) indirectShots++;
                if (IsShot(FreeKickChoice(wide, false, s))) wideShots++;
            }
            Assert.Greater(shots, 12, "a central free kick at 20 m is usually a shot");
            Assert.AreEqual(0, indirectShots, "never shoot an indirect free kick");
            Assert.AreEqual(0, wideShots, "a wide free kick is crossed");
        }

        [Test]
        public void FreeKick_FarFromGoal_HasNoRoutine()
        {
            var w = Restart(TeamAIMatchPhase.FreeKick, new SimVec3(0f, 0f, -5f), TeamAIRole.CM, out _);
            var dir = Dir();
            dir.DecideNow(w, 0.1f);
            Assert.AreEqual(TeamAISetPieceKind.None, dir.Brains[0].SetPieces.Kind);
            Assert.IsFalse(dir.Brains[0].SetPieceRoutineActive);
        }

        [Test]
        public void SetPieces_SteadyState_AllocateNothing()
        {
            var w = CornerFor(out _);
            var dir = Dir();
            for (int k = 0; k < 5; k++) { dir.DecideNow(w, 0.1f); w.Time += 0.1f; }
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int k = 0; k < 90; k++) { dir.DecideNow(w, 0.1f); w.Time += 0.1f; } // spans the taker's delivery
            long after = GC.GetAllocatedBytesForCurrentThread();
            Assert.AreEqual(0L, after - before);
        }
    }
}
