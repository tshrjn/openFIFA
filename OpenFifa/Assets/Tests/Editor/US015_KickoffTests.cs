using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("US-015")]
    public class US015_KickoffTests
    {
        [Test]
        public void KickoffState_HasSettingUp()
        {
            Assert.IsTrue(System.Enum.IsDefined(typeof(KickoffState), KickoffState.SettingUp));
        }

        [Test]
        public void KickoffState_HasWaitingForKick()
        {
            Assert.IsTrue(System.Enum.IsDefined(typeof(KickoffState), KickoffState.WaitingForKick));
        }

        [Test]
        public void KickoffState_HasComplete()
        {
            Assert.IsTrue(System.Enum.IsDefined(typeof(KickoffState), KickoffState.Complete));
        }

        [Test]
        public void KickoffLogic_InitialKickingTeam_IsTeamA()
        {
            var logic = new KickoffLogic();
            Assert.AreEqual(TeamIdentifier.TeamA, logic.KickingTeam,
                $"Initial kicking team should be TeamA but was {logic.KickingTeam}");
        }

        [Test]
        public void KickoffLogic_AfterGoal_KickingTeamAlternates()
        {
            var logic = new KickoffLogic();
            // TeamA scored, so TeamB should kick off
            logic.OnGoalScored(TeamIdentifier.TeamA);
            Assert.AreEqual(TeamIdentifier.TeamB, logic.KickingTeam,
                $"After TeamA scores, TeamB should kick off but was {logic.KickingTeam}");
        }

        [Test]
        public void KickoffLogic_AfterSecondGoal_KickingTeamAlternatesAgain()
        {
            var logic = new KickoffLogic();
            logic.OnGoalScored(TeamIdentifier.TeamA);
            logic.OnGoalScored(TeamIdentifier.TeamB);
            Assert.AreEqual(TeamIdentifier.TeamA, logic.KickingTeam,
                $"After TeamB scores (and TeamA scored before), TeamA should kick off");
        }

        [Test]
        public void KickoffLogic_BallCenterPosition_IsZero()
        {
            var logic = new KickoffLogic();
            Assert.AreEqual(0f, logic.BallCenterX, 0.01f);
            Assert.AreEqual(0f, logic.BallCenterZ, 0.01f);
        }

        [Test]
        public void KickoffLogic_SetupDelay_IsApproximately1Second()
        {
            var logic = new KickoffLogic();
            Assert.AreEqual(1f, logic.SetupDelay, 0.5f,
                $"Setup delay should be ~1s but was {logic.SetupDelay}");
        }
    }
}
