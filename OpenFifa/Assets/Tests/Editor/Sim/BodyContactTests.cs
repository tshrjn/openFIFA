using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor.Sim
{
    /// <summary>Body contact: mass-weighted momentum exchange, stumbles, falls and tackle knock-downs.</summary>
    [TestFixture]
    [Category("BODY_CONTACT")]
    public class BodyContactTests
    {
        private static MatchSim Open(ulong seed)
        {
            var sim = SimTestHelpers.NewSim(seed);
            SimTestHelpers.ForceOpenPlay(sim);
            SimTestHelpers.ParkTeam(sim, SimTeam.Home, -45f);
            SimTestHelpers.ParkTeam(sim, SimTeam.Away, 45f);
            SimTestHelpers.SetBall(sim, new SimVec3(25f, 0.11f, 25f), SimVec3.Zero);
            return sim;
        }

        [Test]
        public void BodyMass_DerivedFromHeightAndStrength_InPlausibleRange()
        {
            var small = new SimPlayerAttributes { Height = 1.68f, Strength = 40 };
            var big = new SimPlayerAttributes { Height = 1.94f, Strength = 92 };
            Assert.That(small.BodyMass, Is.InRange(55f, 70f));
            Assert.That(big.BodyMass, Is.InRange(85f, 100f));
            Assert.AreEqual(77f, new SimPlayerAttributes { Weight = 77f }.BodyMass, 1e-4f);
        }

        [Test]
        public void LightRunnerIntoHeavyStandingDefender_RunnerLosesMostOfTheSpeed()
        {
            var sim = Open(1);
            var light = sim.AttributesOf(9);
            light.Height = 1.68f; light.Strength = 40; light.Balance = 99;
            var heavy = sim.AttributesOf(15);
            heavy.Height = 1.94f; heavy.Strength = 95;
            SimTestHelpers.Place(sim, 9, 0f, 0f);
            SimTestHelpers.Place(sim, 15, 0f, 0.9f);
            sim.State.Players[9].Velocity = new SimVec3(0f, 0f, 5f);
            sim.State.Players[9].Facing = 0f;
            var intents = SimTestHelpers.IdleIntents();
            intents[9] = PlayerIntent.MoveOnly(new SimVec3(0f, 0f, 1f), true);
            for (int t = 0; t < 6; t++) sim.Step(intents);

            float runner = sim.State.Players[9].Velocity.Z;
            float pushed = sim.State.Players[15].Velocity.Z;
            Assert.That(runner, Is.LessThan(3.5f), $"runner kept {runner} m/s");
            Assert.That(pushed, Is.LessThan(2.5f), $"heavy defender shoved to {pushed} m/s");
            Assert.GreaterOrEqual(SimMath.FlatDistance(sim.State.Players[9].Position, sim.State.Players[15].Position),
                2f * sim.Config.Actions.PlayerSeparationRadius - 1e-3f);
        }

        [Test]
        public void HeadOnSprintCollision_CarrierWithPoorBalance_StumblesAndSpillsTheBall()
        {
            var sim = Open(2);
            var c = sim.AttributesOf(9);
            c.Balance = 20; c.Strength = 30; c.Height = 1.70f; c.BallControl = 50;
            var d = sim.AttributesOf(15);
            d.Strength = 95; d.Height = 1.92f;
            SimTestHelpers.Place(sim, 9, 0f, 0f);
            SimTestHelpers.Place(sim, 15, 0f, 0.85f);
            sim.State.Players[9].Velocity = new SimVec3(0f, 0f, 6f);
            sim.State.Players[15].Velocity = new SimVec3(0f, 0f, -5f);
            sim.State.Players[15].Facing = 3.1416f;
            SimTestHelpers.SetBall(sim, new SimVec3(0f, 0.11f, 0.4f), new SimVec3(0f, 0f, 6f));
            sim.State.Owner = 9;
            sim.State.Players[9].NextTouchTick = sim.State.Tick + 1000;
            var intents = SimTestHelpers.IdleIntents();
            intents[9] = PlayerIntent.MoveOnly(new SimVec3(0f, 0f, 1f), true);
            intents[15] = PlayerIntent.MoveOnly(new SimVec3(0f, 0f, -1f), true);

            Assert.IsTrue(SimTestHelpers.StepUntil(sim, intents, MatchEventType.BodyContact, 5, out var e));
            Assert.AreEqual(9, e.PlayerId, "the lighter, less balanced carrier takes the knock");
            Assert.That(e.Detail, Is.GreaterThanOrEqualTo(1));
            Assert.AreNotEqual(9, sim.State.Owner, "a stumbling carrier loses the ball");
        }

        [Test]
        public void BallCarrier_DrivesThroughAChallenge_KeepsHisRun()
        {
            var sim = Open(7);
            var c = sim.AttributesOf(9);
            c.Balance = 99; c.Strength = 99; c.BallControl = 99;
            SimTestHelpers.Place(sim, 9, 0f, 0f);
            SimTestHelpers.Place(sim, 15, 0.3f, 0.75f);
            sim.State.Players[9].Velocity = new SimVec3(0f, 0f, 5f);
            sim.State.Players[9].Facing = 0f;
            SimTestHelpers.SetBall(sim, new SimVec3(0f, 0.11f, 0.4f), new SimVec3(0f, 0f, 5f));
            sim.State.Owner = 9;
            sim.State.Players[9].NextTouchTick = sim.State.Tick + 1000;
            var intents = SimTestHelpers.IdleIntents();
            intents[9] = PlayerIntent.MoveOnly(new SimVec3(0f, 0f, 1f), true);
            for (int t = 0; t < 4; t++) sim.Step(intents);
            Assert.That(sim.State.Players[9].Velocity.Flat.Magnitude, Is.GreaterThan(4.5f));
            Assert.AreEqual(9, sim.State.Owner);
        }

        [Test]
        public void PlayerOnTheGround_IsNotShovedByARunner()
        {
            var sim = Open(5);
            SimTestHelpers.Place(sim, 15, 0f, 0.6f);
            sim.State.Players[15].ActionState = SimPlayerActionState.Grounded;
            sim.State.Players[15].StateEndTick = sim.State.Tick + 600;
            SimTestHelpers.Place(sim, 9, 0f, 0f);
            sim.State.Players[9].Velocity = new SimVec3(0f, 0f, 6f);
            var intents = SimTestHelpers.IdleIntents();
            intents[9] = PlayerIntent.MoveOnly(new SimVec3(0f, 0f, 1f), true);
            for (int t = 0; t < 20; t++) sim.Step(intents);
            Assert.AreEqual(0.6f, sim.State.Players[15].Position.Z, 1e-4f);
            Assert.AreEqual(0f, sim.State.Players[15].Position.X, 1e-4f);
        }

        [Test]
        public void KnockDown_LastsForFallPlusGetUp_AndGivesNoFoul()
        {
            var ac = new SimActionConfig();
            Assert.GreaterOrEqual(ac.ContactFallGroundTime, 0.83f * 0.95f + 1.53f * 0.9f);
            Assert.GreaterOrEqual(ac.TackleFallGroundTime, 0.83f * 0.95f + 1.53f * 0.9f);

            var sim = Open(6);
            var c = sim.AttributesOf(9);
            c.Balance = 1; c.Strength = 1; c.Height = 1.60f;
            var d = sim.AttributesOf(15);
            d.Strength = 99; d.Height = 1.98f;
            SimTestHelpers.Place(sim, 9, 0f, 0f);
            SimTestHelpers.Place(sim, 15, 0f, -0.85f); // from behind
            sim.State.Players[9].Facing = 0f;
            sim.State.Players[9].Velocity = new SimVec3(0f, 0f, -6f);
            sim.State.Players[15].Velocity = new SimVec3(0f, 0f, 9f);
            var intents = SimTestHelpers.IdleIntents();
            bool down = false, foul = false;
            for (int t = 0; t < 5; t++)
            {
                var ev = sim.Step(intents);
                for (int i = 0; i < ev.Count; i++)
                {
                    if (ev[i].Type == MatchEventType.BodyContact && ev[i].Detail == 2) down = true;
                    if (ev[i].Type == MatchEventType.Foul) foul = true;
                }
            }

            Assert.IsTrue(down, "a 9 m/s hit on a 1-balance player floors him");
            Assert.IsFalse(foul, "body contact alone is never a foul");
            Assert.AreEqual(SimMatchPhase.InPlay, sim.State.Phase);
        }

        [Test]
        public void TeammatesBumping_NoContactEvents()
        {
            var sim = Open(3);
            SimTestHelpers.Place(sim, 9, 0f, 0f);
            SimTestHelpers.Place(sim, 8, 0f, 0.8f);
            sim.State.Players[9].Velocity = new SimVec3(0f, 0f, 7f);
            sim.State.Players[8].Velocity = new SimVec3(0f, 0f, -7f);
            var intents = SimTestHelpers.IdleIntents();
            for (int t = 0; t < 10; t++)
            {
                var ev = sim.Step(intents);
                for (int i = 0; i < ev.Count; i++) Assert.AreNotEqual(MatchEventType.BodyContact, ev[i].Type);
            }
        }

        [Test]
        public void GentleContact_BelowThreshold_IsJustALean()
        {
            var sim = Open(4);
            SimTestHelpers.Place(sim, 9, 0f, 0f);
            SimTestHelpers.Place(sim, 15, 0f, 0.8f);
            sim.State.Players[9].Velocity = new SimVec3(0f, 0f, 1f);
            var intents = SimTestHelpers.IdleIntents();
            intents[9] = PlayerIntent.MoveOnly(new SimVec3(0f, 0f, 0.3f));
            for (int t = 0; t < 10; t++)
            {
                var ev = sim.Step(intents);
                for (int i = 0; i < ev.Count; i++)
                    Assert.AreNotEqual(MatchEventType.BodyContact, ev[i].Type,
                        $"t{t} p{ev[i].PlayerId} closing {ev[i].Speed} dv {ev[i].Value} v9 {sim.State.Players[9].Velocity} v15 {sim.State.Players[15].Velocity}");
            }

            Assert.AreEqual(SimPlayerActionState.Free, sim.State.Players[9].ActionState);
        }

        [Test]
        public void WonSlideTackles_SometimesPutTheCarrierDown_MoreOftenThanStandingTackles()
        {
            int Falls(bool slide)
            {
                int falls = 0;
                for (int seed = 1; seed <= 60; seed++)
                {
                    var sim = Open((ulong)(500 + seed));
                    sim.AttributesOf(15).SlidingTackle = 99;
                    sim.AttributesOf(15).StandingTackle = 99;
                    sim.AttributesOf(9).Dribbling = 30;
                    sim.AttributesOf(9).BallControl = 30;
                    sim.AttributesOf(9).Balance = 40;
                    SimTestHelpers.Place(sim, 9, 0f, 0f);
                    sim.State.Players[9].Velocity = new SimVec3(0f, 0f, 5f);
                    SimTestHelpers.SetBall(sim, new SimVec3(0f, 0.11f, 0.5f), new SimVec3(0f, 0f, 5f));
                    sim.State.Owner = 9;
                    SimTestHelpers.Place(sim, 15, 1.0f, 1.4f);
                    sim.State.Players[15].Facing = -1.5708f;
                    var intents = SimTestHelpers.IdleIntents();
                    intents[9] = PlayerIntent.MoveOnly(new SimVec3(0f, 0f, 1f));
                    intents[15] = new PlayerIntent
                    {
                        Action = slide ? IntentAction.SlideTackle : IntentAction.Tackle,
                        Aim = new SimVec3(-1f, 0f, 0.3f), TargetPlayerId = -1,
                    };
                    for (int t = 0; t < 30; t++)
                    {
                        var ev = sim.Step(intents);
                        intents[15] = PlayerIntent.Idle;
                        for (int i = 0; i < ev.Count; i++)
                            if (ev[i].Type == MatchEventType.BodyContact && ev[i].PlayerId == 9 && ev[i].Detail == 2) falls++;
                    }
                }

                return falls;
            }

            int slideFalls = Falls(true), standFalls = Falls(false);
            Assert.That(slideFalls, Is.GreaterThan(5), $"slide falls {slideFalls}/60");
            Assert.That(slideFalls, Is.GreaterThan(standFalls), $"slide {slideFalls} vs standing {standFalls}");
        }
    }
}
