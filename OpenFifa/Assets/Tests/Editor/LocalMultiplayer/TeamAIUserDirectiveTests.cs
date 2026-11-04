using NUnit.Framework;
using OpenFifa.Core;
using OpenFifa.Tests.Editor.TeamAI;

namespace OpenFifa.Tests.Editor.LocalMultiplayer
{
    [TestFixture]
    [Category("LOCAL-2P")]
    public class TeamAIUserDirectiveTests
    {
        private static readonly PitchSpec Pitch = PitchSpec.Standard;

        private static TeamAIDirector Director() =>
            new TeamAIDirector(Pitch, new TeamAITactics(TeamAIFormation.F433, 0.5f, 0.5f, 0.5f), new TeamAITactics(), 1f, 1f, 42);

        private static int FirstOfRole(TeamAIWorld w, int team, TeamAIRole role)
        {
            for (int i = 0; i < w.PlayerCount; i++) if (w.Players[i].Team == team && w.Players[i].Role == role) return i;
            return -1;
        }

        private static void Run(TeamAIDirector dir, TeamAIWorld w, int ticks)
        {
            for (int i = 0; i < ticks; i++) { dir.DecideNow(w, 0.1f); w.Time += 0.1f; }
        }

        [Test]
        public void Brain_HumanControlledPlayer_GetsNoAICommand()
        {
            var w = TeamAITestWorlds.FullMatch(new TeamAITactics(TeamAIFormation.F433, 0.5f, 0.5f, 0.5f), new TeamAITactics());
            int owner = FirstOfRole(w, 0, TeamAIRole.CM);
            TeamAITestWorlds.GiveBall(w, owner);
            for (int i = 0; i < w.PlayerCount; i++)
                if (w.Players[i].Team == 0 && i != owner && w.Players[i].Role != TeamAIRole.GK) w.Players[i].HumanControlled = true;
            var dir = Director();
            Run(dir, w, 5);
            for (int i = 0; i < w.PlayerCount; i++)
            {
                if (!w.Players[i].HumanControlled) continue;
                Assert.AreEqual(TeamAIIntent.Idle, dir.Commands[i].Intent, $"human player {i} got an AI command");
                Assert.AreEqual(TeamAIAction.None, dir.Commands[i].Action);
            }
        }

        private static TeamAIWorld TwoOptions(out int owner, out int preferred, out int caller)
        {
            // Both teammates open; the AI prefers the more advanced one.
            var w = TeamAITestWorlds.Empty();
            owner = TeamAITestWorlds.Add(w, 0, TeamAIRole.CM, 6, 0f, -5f);
            preferred = TeamAITestWorlds.Add(w, 0, TeamAIRole.AM, 8, -10f, 12f);
            caller = TeamAITestWorlds.Add(w, 0, TeamAIRole.FB, 2, 18f, -8f);
            TeamAITestWorlds.Add(w, 1, TeamAIRole.CB, 4, 0f, 30f);
            TeamAITestWorlds.Add(w, 1, TeamAIRole.CB, 5, 8f, 30f);
            TeamAITestWorlds.Add(w, 1, TeamAIRole.GK, 0, 0f, 50f);
            TeamAITestWorlds.Add(w, 0, TeamAIRole.GK, 0, 0f, -50f);
            TeamAITestWorlds.GiveBall(w, owner);
            return w;
        }

        [Test]
        public void OnBall_NoCall_PrefersAdvancedTeammate()
        {
            var w = TwoOptions(out int owner, out int preferred, out _);
            var ob = new TeamAIOnBall();
            var cmd = AIPlayerCommand.Idle(owner, w.Players[owner].Position);
            ob.Decide(w, owner, TeamAIPhase.Progression, new TeamAIDifficulty(1f), new TeamAIRandom(7),
                new TeamAIOnBallContext { OffsideDepth = 0.9f }, ref cmd);
            Assert.AreEqual(preferred, ob.LastBestPass.TargetIndex);
        }

        [Test]
        public void OnBall_TeammateCalls_PassGoesToCaller()
        {
            var w = TwoOptions(out int owner, out _, out int caller);
            var ob = new TeamAIOnBall();
            var cmd = AIPlayerCommand.Idle(owner, w.Players[owner].Position);
            ob.Decide(w, owner, TeamAIPhase.Progression, new TeamAIDifficulty(1f), new TeamAIRandom(7),
                new TeamAIOnBallContext { OffsideDepth = 0.9f, HasCall = true, CalledReceiver = caller }, ref cmd);
            Assert.AreEqual(caller, ob.LastBestPass.TargetIndex);
            Assert.AreEqual(TeamAIIntent.Pass, ob.LastChoice.Intent, "the call should beat carrying the ball");
            Assert.AreEqual(caller, cmd.TargetPlayerId);
        }

        [Test]
        public void OnBall_CallerTightlyMarked_AIStillAvoidsTheInterception()
        {
            var w = TwoOptions(out int owner, out int preferred, out int caller);
            // Two opponents sitting in the passing lane to the caller.
            var c = w.Players[caller].Position;
            var o = w.Players[owner].Position;
            TeamAITestWorlds.Add(w, 1, TeamAIRole.CM, 6, (o.X + c.X) * 0.5f, (o.Z + c.Z) * 0.5f);
            TeamAITestWorlds.Add(w, 1, TeamAIRole.CM, 7, c.X - 0.8f, c.Z + 0.3f);
            var ob = new TeamAIOnBall();
            var cmd = AIPlayerCommand.Idle(owner, w.Players[owner].Position);
            ob.Decide(w, owner, TeamAIPhase.Progression, new TeamAIDifficulty(1f), new TeamAIRandom(7),
                new TeamAIOnBallContext { OffsideDepth = 0.9f, HasCall = true, CalledReceiver = caller }, ref cmd);
            Assert.AreEqual(preferred, ob.LastBestPass.TargetIndex, "a call should not force a pass straight to an opponent");
        }

        [Test]
        public void Brain_PassCallDirective_ReachesOnBallDecision()
        {
            var w = TwoOptions(out int owner, out _, out int caller);
            w.Players[caller].HumanControlled = true;
            w.Directives[0].CallForPass(caller, false, 10f);
            var dir = Director();
            Run(dir, w, 6); // past the reception reaction delay
            Assert.AreEqual(TeamAIAction.Pass, dir.Commands[owner].Action);
            Assert.AreEqual(caller, dir.Commands[owner].TargetPlayerId);
        }

        [Test]
        public void Brain_ExpiredPassCall_IsIgnored()
        {
            var w = TwoOptions(out int owner, out _, out int caller);
            w.Directives[0].CallForPass(caller, false, -1f);
            var dir = Director();
            Run(dir, w, 6);
            Assert.AreNotEqual(caller, dir.Brains[0].OnBall.LastBestPass.TargetIndex);
        }

        [Test]
        public void Brain_RunRequestWithTarget_AITeammateSprintsThere()
        {
            var w = TeamAITestWorlds.FullMatch(new TeamAITactics(TeamAIFormation.F433, 0.5f, 0.5f, 0.5f), new TeamAITactics());
            int owner = FirstOfRole(w, 0, TeamAIRole.CM);
            w.Players[owner].HumanControlled = true;
            TeamAITestWorlds.GiveBall(w, owner);
            var target = new SimVec3(20f, 0f, 25f);
            w.Directives[0].RequestRun(-1, target, true, 5f);
            var dir = Director();
            Run(dir, w, 2);
            int runner = dir.Brains[0].DirectedRunnerIndex;
            Assert.GreaterOrEqual(runner, 0);
            Assert.AreNotEqual(owner, runner);
            Assert.IsFalse(w.Players[runner].HumanControlled);
            var cmd = dir.Commands[runner];
            Assert.AreEqual(TeamAIIntent.RunInBehind, cmd.Intent);
            Assert.IsTrue(cmd.Sprint);
            Assert.Less(TeamAIMath.FlatDistance(cmd.MoveTarget, target), 0.5f);
        }

        [Test]
        public void Brain_RunRequestWithoutTarget_ForwardRunsBeyondTheBall()
        {
            var w = TeamAITestWorlds.FullMatch(new TeamAITactics(TeamAIFormation.F433, 0.5f, 0.5f, 0.5f), new TeamAITactics());
            int owner = FirstOfRole(w, 0, TeamAIRole.CM);
            w.Players[owner].HumanControlled = true;
            TeamAITestWorlds.GiveBall(w, owner);
            w.Directives[0].RequestRun(-1, SimVec3.Zero, false, 5f);
            var dir = Director();
            Run(dir, w, 2);
            int runner = dir.Brains[0].DirectedRunnerIndex;
            Assert.GreaterOrEqual(runner, 0);
            var role = w.Players[runner].Role;
            Assert.IsTrue(role == TeamAIRole.ST || role == TeamAIRole.W || role == TeamAIRole.AM, $"runner role {role}");
            Assert.Greater(dir.Commands[runner].MoveTarget.Z, w.Players[runner].Position.Z, "run goes toward goal");
        }

        [Test]
        public void Brain_RunRequestForNamedTeammate_ThatTeammateRuns()
        {
            var w = TeamAITestWorlds.FullMatch(new TeamAITactics(TeamAIFormation.F433, 0.5f, 0.5f, 0.5f), new TeamAITactics());
            int owner = FirstOfRole(w, 0, TeamAIRole.CM);
            int fb = FirstOfRole(w, 0, TeamAIRole.FB);
            TeamAITestWorlds.GiveBall(w, owner);
            w.Directives[0].RequestRun(fb, SimVec3.Zero, false, 5f);
            var dir = Director();
            Run(dir, w, 2);
            Assert.AreEqual(fb, dir.Brains[0].DirectedRunnerIndex);
        }

        [Test]
        public void Brain_TeammatePress_NearestAITeammatePressesWhenHumanIsFirstPresser()
        {
            var w = TeamAITestWorlds.FullMatch(new TeamAITactics(TeamAIFormation.F433, 0.5f, 0.5f, 0.1f), new TeamAITactics());
            int carrier = FirstOfRole(w, 1, TeamAIRole.CM);
            TeamAITestWorlds.GiveBall(w, carrier);
            // The human is the nearest defender.
            int human = FirstOfRole(w, 0, TeamAIRole.CM);
            var ball = w.Ball.Position;
            w.Players[human].Position = new SimVec3(ball.X, 0f, ball.Z - 3f);
            w.Players[human].HumanControlled = true;

            var plain = Director();
            Run(plain, w, 2);
            Assert.AreEqual(-1, plain.Brains[0].RequestedPresserIndex);

            w.Directives[0].RequestPress(w.Time + 1f);
            var dir = Director();
            Run(dir, w, 2);
            int presser = dir.Brains[0].RequestedPresserIndex;
            Assert.GreaterOrEqual(presser, 0);
            Assert.AreNotEqual(human, presser);
            Assert.AreEqual(TeamAIIntent.Press, dir.Commands[presser].Intent);
            Assert.IsTrue(dir.Commands[presser].Sprint);
        }
    }
}
