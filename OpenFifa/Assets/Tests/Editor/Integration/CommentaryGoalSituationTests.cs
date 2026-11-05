using System.Collections.Generic;
using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor.Integration
{
    /// <summary>
    /// Regression: the goal situation used MatchSimState.LastKickRestart at goal time. Only kicks reset it (dribble
    /// touches, deflections and tackles do not), so a goal that was not struck from the restart could be called a
    /// penalty or free kick ("Composure personified from the spot." after a rebound or a short free kick).
    /// </summary>
    [TestFixture]
    [Category("FID_INTEGRATION")] // Unity NUnit rejects "-" in category names
    public class CommentaryGoalSituationTests
    {
        private static MatchSim NewSim() => new MatchSim(new MatchSimConfig(), MatchSetup.CreateDefault(1));

        private static MatchEvent Shot(int player, float z) =>
            new MatchEvent { Type = MatchEventType.Shot, PlayerId = player, Team = SimTeam.Home, Position = new SimVec3(0f, 0.1f, z), Value = 0.3f };

        private static MatchEvent Goal(int player) =>
            new MatchEvent { Type = MatchEventType.Goal, PlayerId = player, OtherPlayerId = -1, Team = SimTeam.Home };

        private static GoalSituation Single(IReadOnlyList<CommentaryEvent> outp)
        {
            CommentaryEvent goal = null;
            foreach (var c in outp) if (c.Type == CommentaryEventType.Goal) goal = c;
            Assert.IsNotNull(goal, "no Goal commentary event");
            return goal.Situation;
        }

        [Test]
        public void GoalSituation_PenaltyShotScores_IsPenalty()
        {
            var sim = NewSim();
            var c = new SimCommentaryAdapter(sim);
            sim.State.LastKickRestart = RestartType.Penalty; // set by the penalty kick, before its Shot event
            var outp = c.Convert(new List<MatchEvent> { Shot(9, 41.5f), Goal(9) }, 1.0);
            Assert.AreEqual(GoalSituation.Penalty, Single(outp));
        }

        [Test]
        public void GoalSituation_SavedPenaltyThenDribbledIn_IsNotPenalty()
        {
            var sim = NewSim();
            var c = new SimCommentaryAdapter(sim);
            sim.State.LastKickRestart = RestartType.Penalty;
            c.Convert(new List<MatchEvent> { Shot(9, 41.5f) }, 1.0);
            c.Convert(new List<MatchEvent> { new MatchEvent { Type = MatchEventType.Save, PlayerId = 11, OtherPlayerId = 9, Team = SimTeam.Away } }, 1.4);
            // Rebound collected and walked in: dribble touches do not reset LastKickRestart in the sim.
            var outp = c.Convert(new List<MatchEvent>
            {
                new MatchEvent { Type = MatchEventType.DribbleTouch, PlayerId = 7, Team = SimTeam.Home },
                Goal(7),
            }, 2.5);
            Assert.AreNotEqual(GoalSituation.Penalty, Single(outp));
        }

        [Test]
        public void GoalSituation_ShortFreeKickThenDribbledIn_IsNotFreeKick()
        {
            var sim = NewSim();
            var c = new SimCommentaryAdapter(sim);
            sim.State.LastKickRestart = RestartType.DirectFreeKick; // free kick played short as a pass
            var outp = c.Convert(new List<MatchEvent>
            {
                new MatchEvent { Type = MatchEventType.RestartTaken, PlayerId = 4, Team = SimTeam.Home, Detail = (int)RestartType.DirectFreeKick },
                new MatchEvent { Type = MatchEventType.DribbleTouch, PlayerId = 8, Team = SimTeam.Home },
                Goal(8),
            }, 3.0);
            Assert.AreEqual(GoalSituation.OpenPlay, Single(outp));
        }

        [Test]
        public void GoalSituation_DirectFreeKickShotScores_IsFreeKick()
        {
            var sim = NewSim();
            var c = new SimCommentaryAdapter(sim);
            sim.State.LastKickRestart = RestartType.DirectFreeKick;
            var outp = c.Convert(new List<MatchEvent> { Shot(10, 27f), Goal(10) }, 1.0);
            Assert.AreEqual(GoalSituation.FreeKick, Single(outp));
        }

        [Test]
        public void GoalSituation_OpenPlayShotAfterEarlierPenalty_IsNotPenalty()
        {
            var sim = NewSim();
            var c = new SimCommentaryAdapter(sim);
            sim.State.LastKickRestart = RestartType.Penalty;
            c.Convert(new List<MatchEvent> { Shot(9, 41.5f), new MatchEvent { Type = MatchEventType.Save, PlayerId = 11, Team = SimTeam.Away } }, 1.0);
            sim.State.LastKickRestart = RestartType.None; // the next kick is open play
            var outp = c.Convert(new List<MatchEvent> { Shot(7, 38f), Goal(7) }, 4.0);
            Assert.AreEqual(GoalSituation.OpenPlay, Single(outp));
        }
    }
}
