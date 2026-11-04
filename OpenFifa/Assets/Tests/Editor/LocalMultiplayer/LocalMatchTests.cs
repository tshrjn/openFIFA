using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor.LocalMultiplayer
{
    [TestFixture]
    [Category("LOCAL_2P")]
    public class LocalMatchTests
    {
        private static SinglePlayerMatchOptions Options(ulong seed = 3) => new SinglePlayerMatchOptions
        {
            Level = TeamAIDifficultyLevel.Professional,
            Seed = seed,
            Config = new MatchSimConfig { HalfDurationSeconds = 60f },
        };

        private static ControllerAssignment Seats(int homePads, int awayPads)
        {
            var c = new ControllerAssignment();
            int dev = 100;
            for (int i = 0; i < homePads; i++) { c.Connect(dev); c.Push(dev++, -1); }
            for (int i = 0; i < awayPads; i++) { c.Connect(dev); c.Push(dev++, +1); }
            return c;
        }

        private static int Bits(long m)
        {
            int n = 0;
            for (; m != 0; m &= m - 1) n++;
            return n;
        }

        [Test]
        public void CoopVsAI_TwoSeatsNeverHoldTheSamePlayer()
        {
            var c = Seats(2, 0);
            var m = new LocalMatch(Options(), c, _ => new ScriptedGamepadBot());
            Assert.AreEqual(LocalMatchMode.CoopVsAI, m.Mode);
            var s = m.Session;
            Assert.AreEqual(2, s.HumanSlots.Count);
            Assert.AreEqual(SimTeam.Home, s.HumanSlots[1].Team);
            int switches = 0;
            int prev0 = s.HumanSlots[0].ControlledId;
            for (int i = 0; i < 60 * 60 && !s.IsFinished; i++)
            {
                s.StepOnce(null);
                int a = s.HumanSlots[0].ControlledId, b = s.HumanSlots[1].ControlledId;
                Assert.AreNotEqual(a, b, $"both seats on player {a} at tick {s.Sim.Tick}");
                Assert.AreEqual(2, Bits(s.HumanMask));
                if (a != prev0) { switches++; prev0 = a; }
            }
            Assert.Greater(switches, 0, "seat 0 should change player during a minute of play");
        }

        [Test]
        public void HeadToHead_OneSeatPerTeam_EqualAIAndRealKeepers()
        {
            var c = Seats(1, 1);
            var opts = Options();
            var m = new LocalMatch(opts, c, _ => new ScriptedGamepadBot());
            var s = m.Session;
            Assert.AreEqual(LocalMatchMode.HeadToHead, m.Mode);
            Assert.AreEqual(SimTeam.Home, s.HumanSlots[0].Team);
            Assert.AreEqual(SimTeam.Away, s.HumanSlots[1].Team);
            Assert.AreEqual(s.Director.Brains[0].Difficulty.Theta, s.Director.Brains[1].Difficulty.Theta, 1e-6f);
            var original = MatchSetup.CreateDefault(opts.Seed);
            Assert.AreEqual(original.Home[0].GkReflexes, s.Sim.AttributesOf(MatchSim.KeeperId(SimTeam.Home)).GkReflexes);
            Assert.AreEqual(original.Away[0].GkReflexes, s.Sim.AttributesOf(MatchSim.KeeperId(SimTeam.Away)).GkReflexes);
            for (int i = 0; i < 60 * 30; i++) s.StepOnce(null);
            Assert.AreEqual(SimTeam.Home, MatchSim.TeamOf(s.HumanSlots[0].ControlledId));
            Assert.AreEqual(SimTeam.Away, MatchSim.TeamOf(s.HumanSlots[1].ControlledId));
        }

        [Test]
        public void AwayOnlyPad_HumanTeamIsAway()
        {
            var m = new LocalMatch(Options(), Seats(0, 1), _ => new ScriptedGamepadBot());
            Assert.AreEqual(SimTeam.Away, m.Session.HumanTeam);
            Assert.AreEqual(LocalMatchMode.SoloVsAI, m.Mode);
        }

        [Test]
        public void PadDropsMidMatch_AIPlaysTheSeatAndMatchPausesUntilAPadReturns()
        {
            var c = Seats(1, 1);
            var m = new LocalMatch(Options(), c, _ => new ScriptedGamepadBot());
            var s = m.Session;
            for (int i = 0; i < 120; i++) s.StepOnce(null);
            c.Disconnect(101); // the away pad
            Assert.IsTrue(s.Paused);
            Assert.IsNull(m.SlotFor(1).Input);
            Assert.AreEqual(1, Bits(s.HumanMask));
            Assert.AreEqual(0, s.Advance(0.1, null), "paused");

            c.Connect(555);
            Assert.IsFalse(s.Paused);
            Assert.IsNotNull(m.SlotFor(1).Input);
            Assert.AreEqual(2, Bits(s.HumanMask));
            m.End();
            Assert.IsFalse(c.InMatch);
        }

        [Test]
        public void Coop_BothSeatsGetOnTheBall()
        {
            var m = new LocalMatch(Options(5), Seats(2, 0), _ => new ScriptedGamepadBot());
            var s = m.Session;
            int[] onBall = new int[2];
            for (int i = 0; i < 60 * 110 && !s.IsFinished; i++)
            {
                s.StepOnce(null);
                int owner = s.Sim.State.Owner;
                for (int k = 0; k < 2; k++) if (owner >= 0 && owner == s.HumanSlots[k].ControlledId) onBall[k]++;
            }
            int total = onBall[0] + onBall[1];
            Assert.Greater(total, 0);
            Assert.Greater(onBall[0], total / 10, $"seat 1 on the ball {onBall[0]} of {total} ticks");
            Assert.Greater(onBall[1], total / 10, $"seat 2 on the ball {onBall[1]} of {total} ticks");
        }

        [Test]
        public void GamepadInput_OffBallPassPressWhilePartnerHasBall_WritesCall()
        {
            LocalGamepadInput second = null;
            int made = 0;
            var m = new LocalMatch(Options(5), Seats(2, 0), seat => seat.Slot == 0
                ? (IHumanInputSource)new ScriptedGamepadBot()
                : second = new LocalGamepadInput());
            Assert.AreEqual(1, ++made);
            var s = m.Session;
            var mine = s.HumanSlots[1];
            int guard = 0;
            while (guard++ < 60 * 110 && !s.IsFinished)
            {
                var st = s.Sim.State;
                if (st.Phase == SimMatchPhase.InPlay && st.Owner >= 0 && st.Owner == s.HumanSlots[0].ControlledId) break;
                second.NextFrame = default;
                s.StepOnce(null);
            }
            Assert.IsFalse(s.IsFinished, "the partner never had the ball");
            second.NextFrame = new GamepadFrame(GamepadButton.South);
            s.StepOnce(null);
            Assert.AreEqual(TeammateCall.PassToFeet, second.Calls.LastCall);
            Assert.AreEqual(mine.ControlledId, s.AI.World.Directives[0].PassCallerId);
        }

        private static SinglePlayerMatchSession LooseBallSession()
        {
            var s = new SinglePlayerMatchSession(Options(5));
            s.HumanSlots[0].Input = new ScriptedGamepadBot();
            for (int i = 0; i < 60 * 120; i++)
            {
                s.StepOnce(null);
                if (s.Sim.State.Phase == SimMatchPhase.InPlay && s.Sim.State.Owner < 0) return s;
            }
            Assert.Fail("ball never loose in play");
            return s;
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Selector_Coop_OnlyThePasserFollowsHisPass(bool iPassed)
        {
            var s = LooseBallSession();
            var sel = new HumanPlayerSelector(SimTeam.Home, 6);
            int partner = 7, receiver = 9;
            var pass = new MatchEvent { Type = MatchEventType.Pass, Team = SimTeam.Home, PlayerId = iPassed ? 6 : partner, OtherPlayerId = receiver };
            sel.Update(s.Sim, new[] { pass }, default, true, 0f, 1L << partner);
            Assert.AreEqual(iPassed ? receiver : 6, sel.ControlledId);
        }

        [Test]
        public void Selector_SingleSeat_StillFollowsAnAITeammatesPass()
        {
            var s = LooseBallSession();
            var sel = new HumanPlayerSelector(SimTeam.Home, 6);
            var pass = new MatchEvent { Type = MatchEventType.Pass, Team = SimTeam.Home, PlayerId = 7, OtherPlayerId = 9 };
            sel.Update(s.Sim, new[] { pass }, default, true, 0f, 1L << 15); // only an opponent seat is blocked
            Assert.AreEqual(9, sel.ControlledId);
        }

        private static void Place(SinglePlayerMatchSession s, int id, float x, float z) =>
            s.Sim.State.Players[id].Position = new SimVec3(x, 0f, z);

        [Test]
        public void Selector_Coop_AITeammateWinsBallEquidistant_LowerIdSeatTakesHim()
        {
            var s = LooseBallSession();
            Place(s, 9, 0f, 0f);
            Place(s, 6, -5f, 0f);
            Place(s, 7, 5f, 0f);
            s.Sim.State.Owner = 9;
            var a = new HumanPlayerSelector(SimTeam.Home, 6);
            var b = new HumanPlayerSelector(SimTeam.Home, 7);
            a.Update(s.Sim, null, default, true, 0f, 1L << 7);
            b.Update(s.Sim, null, default, true, 0f, 1L << 6);
            Assert.AreEqual(9, a.ControlledId, "tie goes to the seat on the lower player id");
            Assert.AreEqual(7, b.ControlledId);
        }

        [Test]
        public void Selector_Coop_NearerSeatTakesAITeammateWhoWinsBall()
        {
            var s = LooseBallSession();
            Place(s, 9, 0f, 0f);
            Place(s, 6, -20f, 0f);
            Place(s, 7, 4f, 0f);
            s.Sim.State.Owner = 9;
            var a = new HumanPlayerSelector(SimTeam.Home, 6);
            var b = new HumanPlayerSelector(SimTeam.Home, 7);
            a.Update(s.Sim, null, default, true, 0f, 1L << 7);
            b.Update(s.Sim, null, default, true, 0f, 1L << 6);
            Assert.AreEqual(6, a.ControlledId);
            Assert.AreEqual(9, b.ControlledId);
        }

        [Test]
        public void Selector_Coop_PartnerHasTheBall_OtherSeatStaysOnHisPlayer()
        {
            var s = LooseBallSession();
            Place(s, 7, 0f, 0f);
            Place(s, 6, -30f, 10f);
            s.Sim.State.Owner = 7;
            var a = new HumanPlayerSelector(SimTeam.Home, 6);
            a.Update(s.Sim, null, default, true, 0f, 1L << 7);
            Assert.AreEqual(6, a.ControlledId);
        }

        [TestCase(0L)]
        [TestCase(1L << 7)]
        public void Selector_ControlledPlayerSentOff_SwitchesToAPlayerStillOnThePitch(long otherSeats)
        {
            var s = LooseBallSession();
            var sel = new HumanPlayerSelector(SimTeam.Home, 6);
            s.Sim.State.Players[6].SentOff = true;
            sel.Update(s.Sim, null, default, true, 0f, otherSeats);
            int id = sel.ControlledId;
            Assert.AreNotEqual(6, id);
            Assert.AreEqual(SimTeam.Home, MatchSim.TeamOf(id));
            Assert.AreNotEqual(MatchSim.KeeperId(SimTeam.Home), id);
            Assert.IsFalse(s.Sim.State.Players[id].SentOff);
            Assert.AreEqual(0L, otherSeats & (1L << id), "never onto the partner's player");
        }

        private static float Excursion(SinglePlayerMatchSession s, int id)
        {
            var p = s.Sim.State.Players[id].Position;
            var pitch = s.Sim.Config.Pitch;
            return System.Math.Max(System.Math.Abs(p.X) - pitch.HalfWidth, System.Math.Abs(p.Z) - pitch.HalfLength);
        }

        [Test]
        public void Seats_NoInputFor10Seconds_StayWithinTheFieldPlus2m()
        {
            var m = new LocalMatch(Options(), Seats(2, 0), _ => new LocalGamepadInput());
            var s = m.Session;
            for (int i = 0; i < 60 * 10; i++)
            {
                s.StepOnce(null);
                for (int k = 0; k < 2; k++)
                {
                    int id = s.HumanSlots[k].ControlledId;
                    Assert.LessOrEqual(Excursion(s, id), 2f, $"seat {k} player {id} at {s.Sim.State.Players[id].Position}, tick {s.Sim.Tick}");
                }
            }
        }

        [Test]
        public void Seats_PlayStopped_SeatPlayersWalkBackOntoThePitch()
        {
            // Bots chase the ball out of play; while play is stopped the guard walks them back on (the sim alone
            // lets them stand 4 m into the run-off).
            var m = new LocalMatch(new SinglePlayerMatchOptions
            {
                Level = TeamAIDifficultyLevel.Professional, Seed = 1, Config = new MatchSimConfig { HalfDurationSeconds = 360f },
            }, Seats(2, 0), _ => new ScriptedGamepadBot());
            var s = m.Session;
            Assert.IsInstanceOf<SeatRestartGuard>(s.HumanSlots[0].Input);
            float worst = 0f;
            for (int i = 0; i < 60 * 200 && !s.IsFinished; i++)
            {
                s.StepOnce(null);
                if (s.Sim.State.Phase == SimMatchPhase.InPlay) continue;
                for (int k = 0; k < 2; k++) worst = System.Math.Max(worst, Excursion(s, s.HumanSlots[k].ControlledId));
            }
            Assert.Less(worst, 2.5f, "a seat player stood deep in the run-off while play was stopped");
        }

        [Test]
        public void PitchToStick_InvertsTheCameraMapping()
        {
            for (float yaw = -3f; yaw < 3f; yaw += 0.7f)
            {
                var dir = new SimVec3(0.6f, 0f, -0.8f);
                var back = TeammateCallResolver.StickToPitch(SeatRestartGuard.PitchToStick(dir, yaw), yaw);
                Assert.AreEqual(dir.X, back.X, 1e-4f);
                Assert.AreEqual(dir.Z, back.Z, 1e-4f);
            }
        }
    }
}
