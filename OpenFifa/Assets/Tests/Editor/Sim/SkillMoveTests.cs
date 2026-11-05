using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor.Sim
{
    /// <summary>Skill moves: star gating, stick mapping, the scripted body/ball path, fakes and wrong-footing.</summary>
    [TestFixture]
    [Category("BODY_SKILL")]
    public class SkillMoveTests
    {
        private const int Carrier = 9;
        private const int Defender = 16;

        /// <summary>Open play, carrier 9 at the origin running +Z with the ball at his feet, everyone else far away.</summary>
        private static MatchSim Setup(ulong seed, float speed = 4f, int stars = 5, int dribbling = 90)
        {
            var sim = SimTestHelpers.NewSim(seed);
            sim.Config.Actions.SkillMovesEnabled = true;
            SimTestHelpers.ForceOpenPlay(sim);
            SimTestHelpers.ParkTeam(sim, SimTeam.Home, -45f);
            SimTestHelpers.ParkTeam(sim, SimTeam.Away, 45f);
            var a = sim.AttributesOf(Carrier);
            a.SkillMoves = stars;
            a.Dribbling = dribbling;
            a.BallControl = dribbling;
            a.Agility = dribbling;
            SimTestHelpers.Place(sim, Carrier, 0f, 0f);
            sim.State.Players[Carrier].Velocity = new SimVec3(0f, 0f, speed);
            sim.State.Players[Carrier].Facing = 0f;
            SimTestHelpers.SetBall(sim, new SimVec3(0f, 0.11f, 0.45f), new SimVec3(0f, 0f, speed));
            sim.State.Owner = Carrier;
            sim.State.PossessionTeam = SimTeam.Home;
            sim.State.Players[Carrier].NextTouchTick = sim.State.Tick + 1000;
            return sim;
        }

        private static PlayerIntent[] Run(MatchSim sim, PlayerIntent first, PlayerIntent then, int ticks,
            System.Action<MatchSim> perTick = null)
        {
            var intents = SimTestHelpers.IdleIntents();
            intents[Carrier] = first;
            sim.Step(intents);
            intents[Carrier] = then;
            for (int t = 0; t < ticks; t++)
            {
                sim.Step(intents);
                perTick?.Invoke(sim);
            }

            return intents;
        }

        [Test]
        public void Gate_NotEnoughStars_FallsBackToSimplerMoveOfSameFamily()
        {
            Assert.AreEqual(SkillMoveKind.Roulette, SkillMoveCatalog.Gate(SkillMoveKind.Roulette, 4));
            Assert.AreEqual(SkillMoveKind.BallRoll, SkillMoveCatalog.Gate(SkillMoveKind.Roulette, 3));
            Assert.AreEqual(SkillMoveKind.BodyFeint, SkillMoveCatalog.Gate(SkillMoveKind.Roulette, 1));
            Assert.AreEqual(SkillMoveKind.DragBack, SkillMoveCatalog.Gate(SkillMoveKind.HeelChop, 2));
            Assert.AreEqual(SkillMoveKind.BodyFeint, SkillMoveCatalog.Gate(SkillMoveKind.Stepover, 1));
            Assert.AreEqual(SkillMoveKind.None, SkillMoveCatalog.Gate(SkillMoveKind.None, 5));
        }

        [Test]
        public void FromFlick_RelativeToFacing_MapsEightWays()
        {
            Assert.AreEqual(SkillMoveKind.Stepover, SkillMoveCatalog.FromFlick(0f, false));
            Assert.AreEqual(SkillMoveKind.BodyFeint, SkillMoveCatalog.FromFlick(45f, false));
            Assert.AreEqual(SkillMoveKind.BodyFeint, SkillMoveCatalog.FromFlick(-45f, false));
            Assert.AreEqual(SkillMoveKind.BallRoll, SkillMoveCatalog.FromFlick(90f, false));
            Assert.AreEqual(SkillMoveKind.Roulette, SkillMoveCatalog.FromFlick(-90f, true));
            Assert.AreEqual(SkillMoveKind.HeelChop, SkillMoveCatalog.FromFlick(135f, false));
            Assert.AreEqual(SkillMoveKind.DragBack, SkillMoveCatalog.FromFlick(180f, false));
        }

        [Test]
        public void HumanAdapter_RightStickFlick_BecomesGatedSkillIntentRelativeToFacing()
        {
            var sim = Setup(2);
            var ci = new ControlIntent { Action = ControlAction.SkillMove, SkillDirection = Direction8.Right };
            var intent = HumanControlAdapter.ToIntent(ci, 0f, sim, Carrier, null, 0f, 0f);
            Assert.AreEqual(IntentAction.SkillMove, intent.Action);
            Assert.AreEqual(SkillMoveKind.BallRoll, intent.Skill);
            Assert.AreEqual(1f, intent.Aim.X, 1e-4f);

            ci.SkillDirection = Direction8.Down;
            Assert.AreEqual(SkillMoveKind.DragBack, HumanControlAdapter.ToIntent(ci, 0f, sim, Carrier, null, 0f, 0f).Skill);
            // Camera turned 180 deg: "down" on the stick now points up the pitch, the way he faces.
            Assert.AreEqual(SkillMoveKind.Stepover, HumanControlAdapter.ToIntent(ci, 3.14159265f, sim, Carrier, null, 0f, 0f).Skill);
        }

        [Test]
        public void Catalog_EveryMove_HasSaneShape()
        {
            foreach (SkillMoveKind k in System.Enum.GetValues(typeof(SkillMoveKind)))
            {
                if (k == SkillMoveKind.None) continue;
                var p = SkillMoveCatalog.Get(k);
                Assert.That(p.RequiredStars, Is.InRange(1, 5), k.ToString());
                Assert.That(p.Duration, Is.InRange(0.3f, 0.9f), k.ToString());
                Assert.That(p.ExitAngleDeg, Is.InRange(30f, 180f), k.ToString());
                Assert.That(p.BallLead, Is.InRange(0.2f, 1f), k.ToString());
                if (p.HasFake) Assert.That(p.FakeAt, Is.InRange(0.1f, 0.6f), k.ToString());
            }
        }

        [Test]
        public void BodyFeint_FlickRight_ExitsRightAtAboutFiftyDegreesWithTheBall()
        {
            var sim = Setup(3);
            int skillEvents = 0;
            var intents = SimTestHelpers.IdleIntents();
            intents[Carrier] = PlayerIntent.SkillMove(SkillMoveKind.BodyFeint, new SimVec3(1f, 0f, 1f));
            for (int t = 0; t < 40; t++)
            {
                var ev = sim.Step(intents);
                for (int e = 0; e < ev.Count; e++) if (ev[e].Type == MatchEventType.SkillMove) skillEvents++;
                intents[Carrier] = PlayerIntent.Idle;
            }

            Assert.AreEqual(1, skillEvents);
            var p = sim.State.Players[Carrier];
            Assert.AreEqual(SimPlayerActionState.Free, p.ActionState);
            SimVec3 ballDir = (sim.State.Ball.Position - p.Position).Flat;
            float deg = SimMath.Yaw(sim.State.Ball.Velocity.Flat) * SimMath.Rad2Deg;
            Assert.That(sim.State.Ball.Position.X, Is.GreaterThan(0.3f), $"ball should have gone right: {sim.State.Ball.Position}");
            Assert.That(deg, Is.InRange(25f, 75f), $"release heading {deg} deg");
            Assert.That(ballDir.Magnitude, Is.LessThan(sim.Config.Actions.LooseRadius), "still the carrier's ball");
        }

        [Test]
        public void DragBack_TurnsTheCarrierAroundWithTheBall()
        {
            var sim = Setup(4, speed: 3f);
            float maxGap = 0f;
            Run(sim, PlayerIntent.SkillMove(SkillMoveKind.DragBack, new SimVec3(-1f, 0f, -1f)), PlayerIntent.Idle, 45, s =>
            {
                float gap = SimMath.FlatDistance(s.State.Ball.Position, s.State.Players[Carrier].Position);
                if (gap > maxGap) maxGap = gap;
            });

            var p = sim.State.Players[Carrier];
            float facingDeg = SimMath.Abs(SimMath.WrapAngle(p.Facing)) * SimMath.Rad2Deg;
            Assert.That(facingDeg, Is.GreaterThan(140f), $"should now face back up the pitch, facing {facingDeg}");
            Assert.That(sim.State.Ball.Velocity.Z, Is.LessThan(0f), "ball released backwards");
            Assert.That(maxGap, Is.LessThan(2.2f), "ball stays on the foot through the move");
            Assert.AreEqual(Carrier, sim.State.LastTouchPlayer);
        }

        [Test]
        public void Roulette_SpinsTheBodyAFullTurnPlusTheExit()
        {
            var sim = Setup(5, speed: 3f);
            float travelled = 0f, last = sim.State.Players[Carrier].Facing;
            Run(sim, PlayerIntent.SkillMove(SkillMoveKind.Roulette, new SimVec3(1f, 0f, 0f)), PlayerIntent.Idle, 44, s =>
            {
                float f = s.State.Players[Carrier].Facing;
                travelled += SimMath.DeltaAngle(last, f);
                last = f;
            });

            Assert.That(travelled * SimMath.Rad2Deg, Is.GreaterThan(380f), "a roulette spins over 360 deg toward the exit");
        }

        [Test]
        public void SkillMove_WithoutTheBall_DoesNothing()
        {
            var sim = Setup(6);
            sim.State.Owner = -1;
            SimTestHelpers.SetBall(sim, new SimVec3(10f, 0.11f, 10f), SimVec3.Zero);
            var intents = SimTestHelpers.IdleIntents();
            intents[Carrier] = PlayerIntent.SkillMove(SkillMoveKind.Stepover, new SimVec3(1f, 0f, 0f));
            var ev = sim.Step(intents);
            Assert.AreNotEqual(SimPlayerActionState.Skill, sim.State.Players[Carrier].ActionState);
            for (int e = 0; e < ev.Count; e++) Assert.AreNotEqual(MatchEventType.SkillMove, ev[e].Type);
        }

        [Test]
        public void SkillMoves_OffByDefault_UntilClipsExist()
        {
            Assert.IsFalse(new SimActionConfig().SkillMovesEnabled);
            var sim = Setup(8);
            sim.Config.Actions.SkillMovesEnabled = false;
            var intents = SimTestHelpers.IdleIntents();
            intents[Carrier] = PlayerIntent.SkillMove(SkillMoveKind.Stepover, new SimVec3(1f, 0f, 0f));
            Assert.IsFalse(SimTestHelpers.StepUntil(sim, intents, MatchEventType.SkillMove, 5, out _));
        }

        [Test]
        public void SkillMove_Cooldown_NoChainedMoves()
        {
            var sim = Setup(10, speed: 3f);
            var intents = SimTestHelpers.IdleIntents();
            intents[Carrier] = PlayerIntent.SkillMove(SkillMoveKind.Roulette, new SimVec3(1f, 0f, 0f));
            int moves = 0, freeTicks = 0;
            int durTicks = sim.Config.SecondsToTicks(SkillMoveCatalog.Get(SkillMoveKind.Roulette).Duration);
            int coolTicks = sim.Config.SecondsToTicks(sim.Config.Actions.SkillCooldown);
            for (int t = 0; t < durTicks + coolTicks - 2; t++)
            {
                var ev = sim.Step(intents); // keeps asking every tick
                for (int e = 0; e < ev.Count; e++) if (ev[e].Type == MatchEventType.SkillMove) moves++;
                if (sim.State.Players[Carrier].IsFree) freeTicks++;
            }

            Assert.AreEqual(1, moves, "a second move must wait for the cooldown");
            Assert.Greater(freeTicks, 0);
        }

        [Test]
        public void OneStarPlayer_AskingForRoulette_DoesABodyFeint()
        {
            var sim = Setup(7, stars: 1);
            var intents = SimTestHelpers.IdleIntents();
            intents[Carrier] = PlayerIntent.SkillMove(SkillMoveKind.Roulette, new SimVec3(1f, 0f, 0f));
            Assert.IsTrue(SimTestHelpers.StepUntil(sim, intents, MatchEventType.SkillMove, 3, out var e));
            Assert.AreEqual((int)SkillMoveKind.BodyFeint, e.Detail);
        }

        /// <summary>How often a defender 2.5 m in front bites on a stepover, over many seeds.</summary>
        private static int Bites(int dribbling, int stars, int reactions, bool jockey, int seeds)
        {
            int bites = 0;
            for (int seed = 1; seed <= seeds; seed++)
            {
                var sim = Setup((ulong)(100 + seed), speed: 3f, stars: stars, dribbling: dribbling);
                var d = sim.AttributesOf(Defender);
                d.Reactions = reactions;
                d.Agility = reactions;
                d.StandingTackle = reactions;
                SimTestHelpers.Place(sim, Defender, 0f, 2.6f);
                sim.State.Players[Defender].Facing = 3.14159f;
                var intents = SimTestHelpers.IdleIntents();
                intents[Carrier] = PlayerIntent.SkillMove(SkillMoveKind.Stepover, new SimVec3(1f, 0f, 1f));
                if (jockey) intents[Defender] = new PlayerIntent { Action = IntentAction.Jockey, TargetPlayerId = -1 };
                for (int t = 0; t < 40; t++)
                {
                    var ev = sim.Step(intents);
                    intents[Carrier] = PlayerIntent.Idle;
                    for (int e = 0; e < ev.Count; e++)
                        if (ev[e].Type == MatchEventType.WrongFooted && ev[e].PlayerId == Defender) bites++;
                }
            }

            return bites;
        }

        [Test]
        public void Fake_SkilledCarrierVsSlowReader_BitesFarMoreThanAveragePlayerVsSharpJockeyingDefender()
        {
            int skilled = Bites(95, 5, 45, false, 40);
            int poor = Bites(55, 2, 92, true, 40);
            Assert.That(skilled, Is.GreaterThan(26), $"skilled carrier bites {skilled}/40");
            Assert.That(poor, Is.LessThan(14), $"sharp jockeying defender bites {poor}/40");
        }

        [Test]
        public void WrongFootedDefender_IsSluggishToRecover()
        {
            // Same defender, same sprint request toward +X: with and without a wrong-foot window.
            float Run1(bool wrongFooted)
            {
                var sim = SimTestHelpers.NewSim(9);
                SimTestHelpers.ForceOpenPlay(sim);
                SimTestHelpers.SetBall(sim, new SimVec3(20f, 0.11f, 20f), SimVec3.Zero);
                SimTestHelpers.Place(sim, Defender, 0f, 0f);
                sim.State.Players[Defender].Velocity = new SimVec3(-2f, 0f, 0f);
                sim.State.Players[Defender].Facing = -1.5708f;
                if (wrongFooted) sim.State.Players[Defender].WrongFootedUntilTick = sim.State.Tick + 30;
                var intents = SimTestHelpers.IdleIntents();
                intents[Defender] = PlayerIntent.MoveOnly(new SimVec3(1f, 0f, 0f), true);
                for (int t = 0; t < 30; t++) sim.Step(intents);
                return sim.State.Players[Defender].Position.X;
            }

            float normal = Run1(false), bitten = Run1(true);
            Assert.That(bitten, Is.LessThan(normal - 0.5f), $"wrong-footed {bitten} vs normal {normal}");
        }

        [Test]
        public void SkillMove_SameSeedAndIntents_IsDeterministic()
        {
            ulong Hash()
            {
                var sim = Setup(11);
                SimTestHelpers.Place(sim, Defender, 0.4f, 2.5f);
                Run(sim, PlayerIntent.SkillMove(SkillMoveKind.HeelChop, new SimVec3(-1f, 0f, -1f)), PlayerIntent.Idle, 50);
                return sim.ComputeStateHash();
            }

            Assert.AreEqual(Hash(), Hash());
        }
    }
}
