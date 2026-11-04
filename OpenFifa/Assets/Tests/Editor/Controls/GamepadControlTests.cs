using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("FID_CONTROLS")]
    public class GamepadControlTests
    {
        private const float Dt = 1f / 60f;

        /// <summary>Presses <paramref name="face"/> with <paramref name="mods"/> held, holds it, releases, and returns the fired intent.</summary>
        private float _clock;

        private ControlIntent PressAndRelease(GamepadControlResolver r, GamepadButton face, GamepadButton mods,
            float holdSeconds, bool crossingZone = false)
        {
            float t = _clock + 1f;
            r.Tick(new GamepadFrame(mods), ControlContext.Attack(t, true, crossingZone));
            t += Dt;
            r.Tick(new GamepadFrame(mods | face), ControlContext.Attack(t, true, crossingZone));
            t += holdSeconds;
            r.Tick(new GamepadFrame(mods | face), ControlContext.Attack(t, true, crossingZone));
            t += Dt;
            var fired = r.Tick(new GamepadFrame(mods), ControlContext.Attack(t, true, crossingZone));
            _clock = t;
            return fired;
        }

        [Test]
        public void Classic_FaceButtons_ResolveToClassicRoles()
        {
            var r = new GamepadControlResolver(ControlPreset.Classic());
            Assert.AreEqual(ControlAction.Pass, PressAndRelease(r, GamepadButton.South, GamepadButton.None, 0.1f).Action);
            Assert.AreEqual(ControlAction.Shoot, PressAndRelease(r, GamepadButton.East, GamepadButton.None, 0.1f).Action);
            Assert.AreEqual(ControlAction.Lob, PressAndRelease(r, GamepadButton.West, GamepadButton.None, 0.1f).Action);
            Assert.AreEqual(ControlAction.ThroughBall, PressAndRelease(r, GamepadButton.North, GamepadButton.None, 0.1f).Action);
        }

        [Test]
        public void Classic_ShotModifiers_ResolveChipFinesseAndDriven()
        {
            var r = new GamepadControlResolver(ControlPreset.Classic());
            Assert.AreEqual(ControlAction.ChipShot,
                PressAndRelease(r, GamepadButton.East, GamepadButton.LeftShoulder, 0.1f).Action);
            Assert.AreEqual(ControlAction.FinesseShot,
                PressAndRelease(r, GamepadButton.East, GamepadButton.RightShoulder, 0.1f).Action);
            Assert.AreEqual(ControlAction.DrivenShot,
                PressAndRelease(r, GamepadButton.East, GamepadButton.LeftShoulder | GamepadButton.RightShoulder, 0.1f).Action);
        }

        /// <summary>Presses <paramref name="face"/> (with <paramref name="mods"/>), then presses LT during the charge, then releases.</summary>
        private ControlIntent KickThenTrigger(GamepadControlResolver r, GamepadButton face, GamepadButton mods)
        {
            float t = _clock + 1f;
            r.Tick(new GamepadFrame(mods), ControlContext.Attack(t, true));
            t += Dt;
            r.Tick(new GamepadFrame(mods | face), ControlContext.Attack(t, true));
            t += 0.1f;
            r.Tick(new GamepadFrame(mods | face | GamepadButton.LeftTrigger), ControlContext.Attack(t, true));
            t += Dt;
            var fired = r.Tick(new GamepadFrame(mods | GamepadButton.LeftTrigger), ControlContext.Attack(t, true));
            _clock = t;
            return fired;
        }

        [Test]
        public void ShootThenLeftTrigger_IsKnuckleShot_InBothPresets()
        {
            var classic = new GamepadControlResolver(ControlPreset.Classic());
            Assert.AreEqual(ControlAction.KnuckleShot, KickThenTrigger(classic, GamepadButton.East, GamepadButton.None).Action);
            var alt = new GamepadControlResolver(ControlPreset.Alternate());
            Assert.AreEqual(ControlAction.KnuckleShot, KickThenTrigger(alt, GamepadButton.West, GamepadButton.None).Action);
        }

        [Test]
        public void LeftTriggerHeldThenShoot_IsNormalShot_ShieldIsNotKnuckle()
        {
            var r = new GamepadControlResolver(ControlPreset.Classic());
            Assert.AreEqual(ControlAction.Shoot,
                PressAndRelease(r, GamepadButton.East, GamepadButton.LeftTrigger, 0.1f).Action);
            var alt = new GamepadControlResolver(ControlPreset.Alternate());
            Assert.AreEqual(ControlAction.Shoot,
                PressAndRelease(alt, GamepadButton.West, GamepadButton.LeftTrigger, 0.1f).Action);
        }

        [Test]
        public void LeftTriggerHeldThenShoot_KeepsShieldingWhileCharging()
        {
            var r = new GamepadControlResolver(ControlPreset.Classic());
            float t = 1f;
            r.Tick(new GamepadFrame(GamepadButton.LeftTrigger), ControlContext.Attack(t, true));
            t += Dt;
            var charging = r.Tick(new GamepadFrame(GamepadButton.LeftTrigger | GamepadButton.East), ControlContext.Attack(t, true));
            Assert.IsTrue((charging.Held & ControlHeld.Shield) != 0);
            Assert.IsTrue(r.IsCharging);
        }

        [Test]
        public void LeftTriggerAndShoot_SameTick_IsNormalShot()
        {
            var r = new GamepadControlResolver(ControlPreset.Classic());
            float t = 1f;
            r.Tick(new GamepadFrame(GamepadButton.None), ControlContext.Attack(t, true));
            t += Dt;
            r.Tick(new GamepadFrame(GamepadButton.East | GamepadButton.LeftTrigger), ControlContext.Attack(t, true));
            t += 0.1f;
            r.Tick(new GamepadFrame(GamepadButton.East | GamepadButton.LeftTrigger), ControlContext.Attack(t, true));
            t += Dt;
            var fired = r.Tick(new GamepadFrame(GamepadButton.LeftTrigger), ControlContext.Attack(t, true));
            Assert.AreEqual(ControlAction.Shoot, fired.Action);
        }

        [Test]
        public void LeftTriggerHeld_ReleasedAndPressedAgainDuringCharge_IsKnuckle()
        {
            var r = new GamepadControlResolver(ControlPreset.Classic());
            float t = 1f;
            r.Tick(new GamepadFrame(GamepadButton.LeftTrigger), ControlContext.Attack(t, true));
            t += Dt;
            r.Tick(new GamepadFrame(GamepadButton.LeftTrigger | GamepadButton.East), ControlContext.Attack(t, true));
            t += 0.05f;
            r.Tick(new GamepadFrame(GamepadButton.East), ControlContext.Attack(t, true));
            t += 0.05f;
            r.Tick(new GamepadFrame(GamepadButton.East | GamepadButton.LeftTrigger), ControlContext.Attack(t, true));
            t += Dt;
            var fired = r.Tick(new GamepadFrame(GamepadButton.None), ControlContext.Attack(t, true));
            Assert.AreEqual(ControlAction.KnuckleShot, fired.Action);
        }

        [Test]
        public void ShootThenLeftTrigger_OverridesShoulderModifiers()
        {
            var r = new GamepadControlResolver(ControlPreset.Classic());
            Assert.AreEqual(ControlAction.KnuckleShot,
                KickThenTrigger(r, GamepadButton.East, GamepadButton.RightShoulder).Action);
            Assert.AreEqual(ControlAction.KnuckleShot,
                KickThenTrigger(r, GamepadButton.East, GamepadButton.LeftShoulder | GamepadButton.RightShoulder).Action);
        }

        [Test]
        public void LeftTrigger_PressedDuringShotCharge_IsKnuckle()
        {
            var r = new GamepadControlResolver(ControlPreset.Classic());
            float t = 1f;
            r.Tick(new GamepadFrame(GamepadButton.East), ControlContext.Attack(t, true));
            t += 0.1f;
            r.Tick(new GamepadFrame(GamepadButton.East | GamepadButton.LeftTrigger), ControlContext.Attack(t, true));
            t += Dt;
            var fired = r.Tick(new GamepadFrame(GamepadButton.None), ControlContext.Attack(t, true));
            Assert.AreEqual(ControlAction.KnuckleShot, fired.Action);
        }

        [Test]
        public void LeftTrigger_DoesNotChangeOtherKicks()
        {
            var r = new GamepadControlResolver(ControlPreset.Classic());
            Assert.AreEqual(ControlAction.Pass, PressAndRelease(r, GamepadButton.South, GamepadButton.LeftTrigger, 0.1f).Action);
            Assert.AreEqual(ControlAction.ThroughBall, PressAndRelease(r, GamepadButton.North, GamepadButton.LeftTrigger, 0.1f).Action);
            Assert.AreEqual(ControlAction.Lob, PressAndRelease(r, GamepadButton.West, GamepadButton.LeftTrigger, 0.1f).Action);
            Assert.AreEqual(ControlAction.Pass, KickThenTrigger(r, GamepadButton.South, GamepadButton.None).Action);
            Assert.AreEqual(ControlAction.Lob, KickThenTrigger(r, GamepadButton.West, GamepadButton.None).Action);
            Assert.AreEqual(ControlAction.Shoot, PressAndRelease(r, GamepadButton.East, GamepadButton.None, 0.1f).Action);
        }

        [Test]
        public void KnuckleShot_IsAShotKick()
        {
            Assert.IsTrue(ControlIntent.IsShotAction(ControlAction.KnuckleShot));
            Assert.IsTrue(ControlIntent.IsKickAction(ControlAction.KnuckleShot));
            Assert.IsFalse(ControlIntent.IsShotAction(ControlAction.SkillMove));
        }

        [Test]
        public void Alternate_ShotModifiers_ResolveFinesseDrivenAndChip()
        {
            var r = new GamepadControlResolver(ControlPreset.Alternate());
            Assert.AreEqual(ControlAction.Shoot, PressAndRelease(r, GamepadButton.West, GamepadButton.None, 0.1f).Action);
            Assert.AreEqual(ControlAction.FinesseShot,
                PressAndRelease(r, GamepadButton.West, GamepadButton.LeftShoulder, 0.1f).Action);
            Assert.AreEqual(ControlAction.DrivenShot,
                PressAndRelease(r, GamepadButton.West, GamepadButton.RightShoulder, 0.1f).Action);
            Assert.AreEqual(ControlAction.ChipShot,
                PressAndRelease(r, GamepadButton.West, GamepadButton.LeftShoulder | GamepadButton.RightShoulder, 0.1f).Action);
            Assert.AreEqual(ControlAction.Lob, PressAndRelease(r, GamepadButton.East, GamepadButton.None, 0.1f).Action);
        }

        [Test]
        public void PassModifiers_RightShoulder_DrivenPass_LeftShoulderThrough_LobbedThrough()
        {
            var r = new GamepadControlResolver(ControlPreset.Classic());
            Assert.AreEqual(ControlAction.DrivenPass,
                PressAndRelease(r, GamepadButton.South, GamepadButton.RightShoulder, 0.1f).Action);
            Assert.AreEqual(ControlAction.LobbedThrough,
                PressAndRelease(r, GamepadButton.North, GamepadButton.LeftShoulder, 0.1f).Action);
            Assert.AreEqual(ControlAction.DrivenThrough,
                PressAndRelease(r, GamepadButton.North, GamepadButton.LeftShoulder | GamepadButton.RightShoulder, 0.1f).Action);
            Assert.AreEqual(ControlAction.DrivenLob,
                PressAndRelease(r, GamepadButton.West, GamepadButton.LeftShoulder | GamepadButton.RightShoulder, 0.1f).Action);
        }

        [Test]
        public void Lob_InCrossingZone_BecomesCross()
        {
            var r = new GamepadControlResolver();
            Assert.AreEqual(ControlAction.Cross,
                PressAndRelease(r, GamepadButton.West, GamepadButton.None, 0.1f, crossingZone: true).Action);
        }

        [Test]
        public void Modifier_PressedDuringHold_StillApplies()
        {
            var r = new GamepadControlResolver();
            r.Tick(new GamepadFrame(GamepadButton.East), ControlContext.Attack(0f));
            r.Tick(new GamepadFrame(GamepadButton.East | GamepadButton.RightShoulder), ControlContext.Attack(0.05f));
            var fired = r.Tick(new GamepadFrame(GamepadButton.None), ControlContext.Attack(0.1f));
            Assert.AreEqual(ControlAction.FinesseShot, fired.Action);
        }

        [Test]
        public void HoldTime_LongerHold_GivesMorePower()
        {
            var r = new GamepadControlResolver();
            float tap = PressAndRelease(r, GamepadButton.East, GamepadButton.None, 0.05f).Power;
            float full = PressAndRelease(r, GamepadButton.East, GamepadButton.None, 1.5f).Power;
            Assert.Greater(full, tap);
            Assert.AreEqual(1f, full, 1e-5f);
        }

        [Test]
        public void ChargingKick_ReportsChargeForPowerBar()
        {
            var r = new GamepadControlResolver();
            r.Tick(new GamepadFrame(GamepadButton.East), ControlContext.Attack(0f));
            var mid = r.Tick(new GamepadFrame(GamepadButton.East), ControlContext.Attack(0.5f));
            Assert.AreEqual(ControlAction.None, mid.Action);
            Assert.Greater(mid.Charge, 0.1f);
            Assert.Less(mid.Charge, 1f);
        }

        [Test]
        public void ReleaseDuringWindUp_FiresOnContactFrame()
        {
            var r = new GamepadControlResolver();
            r.Tick(new GamepadFrame(GamepadButton.East), ControlContext.Attack(0f, canStrike: false));
            var released = r.Tick(new GamepadFrame(GamepadButton.None), ControlContext.Attack(0.2f, canStrike: false));
            Assert.AreEqual(ControlAction.None, released.Action);
            Assert.IsTrue(released.KickBuffered);

            var waiting = r.Tick(new GamepadFrame(GamepadButton.None), ControlContext.Attack(0.3f, canStrike: false));
            Assert.AreEqual(ControlAction.None, waiting.Action);

            var contact = r.Tick(new GamepadFrame(GamepadButton.None), ControlContext.Attack(0.4f, canStrike: true));
            Assert.AreEqual(ControlAction.Shoot, contact.Action);
            Assert.Greater(contact.Power, 0f);
            Assert.IsFalse(contact.KickBuffered);
        }

        [Test]
        public void BufferedKick_AfterWindowExpires_IsDropped()
        {
            var r = new GamepadControlResolver();
            r.Tick(new GamepadFrame(GamepadButton.South), ControlContext.Attack(0f, canStrike: false));
            r.Tick(new GamepadFrame(GamepadButton.None), ControlContext.Attack(0.1f, canStrike: false));
            var late = r.Tick(new GamepadFrame(GamepadButton.None), ControlContext.Attack(0.1f + r.Settings.BufferWindowSeconds + 0.05f, canStrike: true));
            Assert.AreEqual(ControlAction.None, late.Action);
        }

        [Test]
        public void ShootTwice_BeforeContact_BecomesLowShot()
        {
            var r = new GamepadControlResolver();
            r.Tick(new GamepadFrame(GamepadButton.East), ControlContext.Attack(0f, canStrike: false));
            r.Tick(new GamepadFrame(GamepadButton.None), ControlContext.Attack(0.05f, canStrike: false));
            r.Tick(new GamepadFrame(GamepadButton.East), ControlContext.Attack(0.1f, canStrike: false));
            r.Tick(new GamepadFrame(GamepadButton.None), ControlContext.Attack(0.15f, canStrike: false));
            var contact = r.Tick(new GamepadFrame(GamepadButton.None), ControlContext.Attack(0.2f, canStrike: true));
            Assert.AreEqual(ControlAction.LowShot, contact.Action);
        }

        [Test]
        public void Defence_FaceButtons_TackleSlideAndContain()
        {
            var r = new GamepadControlResolver();
            Assert.AreEqual(ControlAction.Tackle, r.Tick(new GamepadFrame(GamepadButton.East), ControlContext.Defence(0f)).Action);
            r.Tick(new GamepadFrame(GamepadButton.None), ControlContext.Defence(0.1f));
            Assert.AreEqual(ControlAction.SlideTackle, r.Tick(new GamepadFrame(GamepadButton.West), ControlContext.Defence(0.2f)).Action);
            r.Tick(new GamepadFrame(GamepadButton.None), ControlContext.Defence(0.3f));
            var contain = r.Tick(new GamepadFrame(GamepadButton.South), ControlContext.Defence(0.4f));
            Assert.AreEqual(ControlAction.Contain, contain.Action);
            Assert.IsTrue((contain.Held & ControlHeld.Contain) != 0);
        }

        [Test]
        public void Defence_TriggersAndShoulders_JockeyTeammatePressSwitch()
        {
            var r = new GamepadControlResolver();
            var jockey = r.Tick(new GamepadFrame(GamepadButton.LeftTrigger), ControlContext.Defence(0f));
            Assert.AreEqual(ControlAction.Jockey, jockey.Action);
            var press = r.Tick(new GamepadFrame(GamepadButton.RightShoulder), ControlContext.Defence(0.1f));
            Assert.AreEqual(ControlAction.TeammatePress, press.Action);
            var sw = r.Tick(new GamepadFrame(GamepadButton.LeftShoulder), ControlContext.Defence(0.2f));
            Assert.AreEqual(ControlAction.SwitchPlayer, sw.Action);
            Assert.IsFalse(sw.DirectionalSwitch);
        }

        [Test]
        public void RightStickFlick_AttackSkillMove_DefenceDirectionalSwitch()
        {
            var r = new GamepadControlResolver();
            var skill = r.Tick(new GamepadFrame(GamepadButton.None, default, new ControlVec2(1f, 0f)), ControlContext.Attack(0f));
            Assert.AreEqual(ControlAction.SkillMove, skill.Action);
            Assert.AreEqual(Direction8.Right, skill.SkillDirection);

            // Holding the stick does not repeat the flick.
            var hold = r.Tick(new GamepadFrame(GamepadButton.None, default, new ControlVec2(1f, 0f)), ControlContext.Attack(0.1f));
            Assert.AreEqual(ControlAction.None, hold.Action);

            r.Tick(new GamepadFrame(GamepadButton.None), ControlContext.Defence(0.2f));
            var sw = r.Tick(new GamepadFrame(GamepadButton.None, default, new ControlVec2(0f, -1f)), ControlContext.Defence(0.3f));
            Assert.AreEqual(ControlAction.SwitchPlayer, sw.Action);
            Assert.IsTrue(sw.DirectionalSwitch);
            Assert.AreEqual(-1f, sw.Aim.Y, 1e-5f);
        }

        [Test]
        public void Sprint_RightTrigger_SetsSprintInAttackAndDefence()
        {
            var r = new GamepadControlResolver();
            Assert.IsTrue(r.Tick(new GamepadFrame(GamepadButton.RightTrigger), ControlContext.Attack(0f)).Sprint);
            Assert.IsTrue(r.Tick(new GamepadFrame(GamepadButton.RightTrigger), ControlContext.Defence(0.1f)).Sprint);
        }

        [Test]
        public void LosingPossession_WhileCharging_CancelsKick()
        {
            var r = new GamepadControlResolver();
            r.Tick(new GamepadFrame(GamepadButton.East), ControlContext.Attack(0f));
            r.Tick(new GamepadFrame(GamepadButton.East), ControlContext.Defence(0.1f));
            var after = r.Tick(new GamepadFrame(GamepadButton.None), ControlContext.Attack(0.2f));
            Assert.AreEqual(ControlAction.None, after.Action);
        }

        [Test]
        public void Dpad_UpThenLeft_IssuesTeamPress()
        {
            var r = new GamepadControlResolver();
            Assert.AreEqual(TacticCommand.None, r.Tick(new GamepadFrame(GamepadButton.DpadUp), ControlContext.Attack(0f)).Tactic);
            r.Tick(new GamepadFrame(GamepadButton.None), ControlContext.Attack(0.1f));
            Assert.AreEqual(TacticCommand.TeamPress, r.Tick(new GamepadFrame(GamepadButton.DpadLeft), ControlContext.Attack(0.2f)).Tactic);
        }

        [Test]
        public void Dpad_SecondPressTooLate_StartsNewSequence()
        {
            var r = new GamepadControlResolver();
            r.Tick(new GamepadFrame(GamepadButton.DpadUp), ControlContext.Attack(0f));
            r.Tick(new GamepadFrame(GamepadButton.None), ControlContext.Attack(0.1f));
            Assert.AreEqual(TacticCommand.None, r.Tick(new GamepadFrame(GamepadButton.DpadUp), ControlContext.Attack(2f)).Tactic);
        }

        [Test]
        public void LeftStick_InsideDeadZone_IsZero()
        {
            var r = new GamepadControlResolver();
            var i = r.Tick(new GamepadFrame(GamepadButton.None, new ControlVec2(0.1f, 0.05f)), ControlContext.Attack(0f));
            Assert.AreEqual(0f, i.Move.Magnitude, 1e-6f);
            var full = r.Tick(new GamepadFrame(GamepadButton.None, new ControlVec2(0f, 1f)), ControlContext.Attack(0.1f));
            Assert.AreEqual(1f, full.Move.Magnitude, 1e-5f);
        }
    }
}
