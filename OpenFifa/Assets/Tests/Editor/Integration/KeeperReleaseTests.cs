using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor.Integration
{
    /// <summary>
    /// Regression for the sim fix made while tuning the single-player AI (MatchSim.ProtectKeeperRelease): an opponent
    /// standing at the keeper's elbow used to take every throw, punt and drop-kick at the release point, because the
    /// sim hands a loose ball to anyone within the control radius. Law 12 forbids preventing the release.
    /// </summary>
    [TestFixture]
    [Category("FID_INTEGRATION")]
    public class KeeperReleaseTests
    {
        private const int Keeper = 11;   // Away goalkeeper
        private const int Attacker = 9;  // Home striker

        /// <summary>Away keeper in his box with the ball in his hands, a Home attacker 0.5 m to the side; the keeper
        /// punts upfield. Returns the team that next gains possession (None if nobody does).</summary>
        private static SimTeam ReleaseWithAttackerAtElbow(bool protectRelease)
        {
            var cfg = new MatchSimConfig();
            if (!protectRelease) cfg.Actions.GkReleaseProtectRadius = 0f;
            var sim = new MatchSim(cfg, MatchSetup.CreateDefault(5));
            const bool inHands = true;
            var s = sim.State;
            s.Phase = SimMatchPhase.InPlay;
            s.PendingRestart = RestartType.None;
            s.RestartTakerId = -1;
            s.PassLive = false;
            s.ShotLive = false;
            s.OffsideMask = 0;
            int sign = sim.AttackSign(SimTeam.Away);
            float hl = sim.Config.Pitch.HalfLength;
            for (int i = 0; i < MatchSimState.PlayerCount; i++)
            {
                s.Players[i].Position = new SimVec3(-30f + i * 2.5f, 0f, sign * hl * 0.6f); // everyone far upfield
                s.Players[i].Velocity = SimVec3.Zero;
            }
            var kp = new SimVec3(0f, 0f, -sign * (hl - 8f));
            s.Players[Keeper].Position = kp;
            s.Players[Keeper].Facing = SimMath.Yaw(new SimVec3(0f, 0f, sign));
            s.Players[Attacker].Position = kp + new SimVec3(0.5f, 0f, sign * 0.3f); // level with the ball, at his elbow
            var b = s.Ball;
            b.Position = inHands ? kp + new SimVec3(0f, 1f, sign * 0.3f) : kp + new SimVec3(0f, 0.11f, sign * 0.4f);
            b.Velocity = SimVec3.Zero;
            b.Spin = SimVec3.Zero;
            b.Grounded = !inHands;
            b.InNet = 0;
            s.Ball = b;
            s.Owner = Keeper;
            s.KeeperHolding = inHands;
            s.PossessionTeam = SimTeam.Away;

            var intents = new PlayerIntent[MatchSimState.PlayerCount];
            for (int i = 0; i < intents.Length; i++) intents[i] = PlayerIntent.Idle;
            intents[Keeper].Action = IntentAction.Lob;
            intents[Keeper].Power = 0.7f;
            intents[Keeper].Aim = new SimVec3(0f, 0f, sign);
            intents[Keeper].TargetPlayerId = -1;
            bool kicked = false;
            for (int t = 0; t < 120; t++)
            {
                var ev = sim.Step(intents);
                intents[Keeper] = PlayerIntent.Idle;
                for (int i = 0; i < ev.Count; i++)
                {
                    if (ev[i].Type == MatchEventType.Pass && ev[i].PlayerId == Keeper) kicked = true;
                    if (kicked && (ev[i].Type == MatchEventType.Interception || ev[i].Type == MatchEventType.PossessionChange ||
                                   ev[i].Type == MatchEventType.PassCompleted))
                        return MatchSim.TeamOf(ev[i].PlayerId);
                }
            }
            Assert.IsTrue(kicked, "the keeper never released the ball");
            return SimTeam.None;
        }

        [Test]
        public void KeeperRelease_FromHands_AttackerAtHisElbowCannotTakeIt()
        {
            Assert.AreNotEqual(SimTeam.Home, ReleaseWithAttackerAtElbow(true));
        }

        [Test]
        public void KeeperRelease_ProtectionOff_AttackerAtHisElbowTakesIt()
        {
            // Control case: the same punt with the protection disabled reproduces the bug.
            Assert.AreEqual(SimTeam.Home, ReleaseWithAttackerAtElbow(false));
        }
    }
}
