using System;
using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("FID-CONTROLS")]
    public class TouchControlTests
    {
        private static TouchPoint[] One(int id, ControlVec2 pos, ControlTouchPhase phase)
            => new[] { new TouchPoint(id, pos, phase) };

        private static ControlIntent TapSlot(TouchControlScheme s, TouchSlot slot, ControlContext down, ControlContext up,
            ControlVec2 travel = default)
        {
            ControlVec2 c = s.Layout.GetButton(slot).Center;
            s.Tick(One(1, c, ControlTouchPhase.Began), down);
            return s.Tick(One(1, c + travel, ControlTouchPhase.Ended), up);
        }

        [Test]
        public void Labels_FlipBetweenAttackAndDefence()
        {
            Assert.AreEqual("Pass", TouchControlScheme.LabelFor(TouchSlot.Primary, true));
            Assert.AreEqual("Switch", TouchControlScheme.LabelFor(TouchSlot.Primary, false));
            Assert.AreEqual("Shoot", TouchControlScheme.LabelFor(TouchSlot.Strike, true));
            Assert.AreEqual("Tackle", TouchControlScheme.LabelFor(TouchSlot.Strike, false));
            Assert.AreEqual("Through", TouchControlScheme.LabelFor(TouchSlot.Through, true));
            Assert.AreEqual("Press", TouchControlScheme.LabelFor(TouchSlot.Through, false));
            Assert.AreEqual("Sprint", TouchControlScheme.LabelFor(TouchSlot.Sprint, false));
        }

        [Test]
        public void StrikeButton_Attack_Shoots_Defence_Tackles()
        {
            var s = new TouchControlScheme();
            Assert.AreEqual(ControlAction.Shoot,
                TapSlot(s, TouchSlot.Strike, ControlContext.Attack(0f), ControlContext.Attack(0.1f)).Action);
            Assert.AreEqual(ControlAction.Tackle,
                TapSlot(s, TouchSlot.Strike, ControlContext.Defence(1f), ControlContext.Defence(1.1f)).Action);
        }

        [Test]
        public void PrimaryButton_Attack_Passes_Defence_Switches()
        {
            var s = new TouchControlScheme();
            Assert.AreEqual(ControlAction.Pass,
                TapSlot(s, TouchSlot.Primary, ControlContext.Attack(0f), ControlContext.Attack(0.1f)).Action);
            var sw = TapSlot(s, TouchSlot.Primary, ControlContext.Defence(1f), ControlContext.Defence(1.1f));
            Assert.AreEqual(ControlAction.SwitchPlayer, sw.Action);
            Assert.IsFalse(sw.DirectionalSwitch);
        }

        [Test]
        public void PossessionChange_BetweenPressAndRelease_DropsPress()
        {
            var s = new TouchControlScheme();
            var i = TapSlot(s, TouchSlot.Strike, ControlContext.Attack(0f), ControlContext.Defence(0.1f));
            Assert.AreEqual(ControlAction.None, i.Action);
        }

        [Test]
        public void ShootSwipes_ChipFinesseLow()
        {
            var s = new TouchControlScheme();
            Assert.AreEqual(ControlAction.ChipShot, TapSlot(s, TouchSlot.Strike,
                ControlContext.Attack(0f), ControlContext.Attack(0.1f), new ControlVec2(0f, 0.1f)).Action);
            Assert.AreEqual(ControlAction.FinesseShot, TapSlot(s, TouchSlot.Strike,
                ControlContext.Attack(1f), ControlContext.Attack(1.1f), new ControlVec2(-0.08f, 0f)).Action);
            Assert.AreEqual(ControlAction.LowShot, TapSlot(s, TouchSlot.Strike,
                ControlContext.Attack(2f), ControlContext.Attack(2.1f), new ControlVec2(0f, -0.1f)).Action);
        }

        [Test]
        public void PassSwipeUp_LobOrCrossInCrossingZone()
        {
            var s = new TouchControlScheme();
            Assert.AreEqual(ControlAction.Lob, TapSlot(s, TouchSlot.Primary,
                ControlContext.Attack(0f), ControlContext.Attack(0.1f), new ControlVec2(0f, 0.1f)).Action);
            Assert.AreEqual(ControlAction.Cross, TapSlot(s, TouchSlot.Primary,
                ControlContext.Attack(1f, true, true), ControlContext.Attack(1.1f, true, true), new ControlVec2(0f, 0.1f)).Action);
        }

        private static ControlIntent ShootWithSprint(TouchControlScheme s, float sprintDown, float shootDown, float release,
            bool releaseSprintFirst = false)
        {
            ControlVec2 sprint = s.Layout.GetButton(TouchSlot.Sprint).Center;
            ControlVec2 shoot = s.Layout.GetButton(TouchSlot.Strike).Center;
            // Feed the two presses in time order; equal times go in the same tick.
            if (sprintDown == shootDown)
            {
                s.Tick(new[] { new TouchPoint(1, sprint, ControlTouchPhase.Began), new TouchPoint(2, shoot, ControlTouchPhase.Began) },
                    ControlContext.Attack(sprintDown));
            }
            else if (sprintDown < shootDown)
            {
                s.Tick(One(1, sprint, ControlTouchPhase.Began), ControlContext.Attack(sprintDown));
                s.Tick(new[] { new TouchPoint(1, sprint, ControlTouchPhase.Stationary), new TouchPoint(2, shoot, ControlTouchPhase.Began) },
                    ControlContext.Attack(shootDown));
            }
            else
            {
                s.Tick(One(2, shoot, ControlTouchPhase.Began), ControlContext.Attack(shootDown));
                s.Tick(new[] { new TouchPoint(2, shoot, ControlTouchPhase.Stationary), new TouchPoint(1, sprint, ControlTouchPhase.Began) },
                    ControlContext.Attack(sprintDown));
            }
            if (releaseSprintFirst)
                s.Tick(new[] { new TouchPoint(1, sprint, ControlTouchPhase.Ended), new TouchPoint(2, shoot, ControlTouchPhase.Stationary) },
                    ControlContext.Attack((Math.Max(sprintDown, shootDown) + release) * 0.5f));
            if (releaseSprintFirst) return s.Tick(One(2, shoot, ControlTouchPhase.Ended), ControlContext.Attack(release));
            return s.Tick(new[] { new TouchPoint(1, sprint, ControlTouchPhase.Stationary),
                    new TouchPoint(2, shoot, ControlTouchPhase.Ended) }, ControlContext.Attack(release));
        }

        [Test]
        public void SprintHeldBeforeShot_GivesNormalShot()
        {
            var s = new TouchControlScheme();
            var i = ShootWithSprint(s, sprintDown: 0f, shootDown: 0.5f, release: 0.7f);
            Assert.AreEqual(ControlAction.Shoot, i.Action);
            Assert.IsTrue(i.Sprint);
        }

        [Test]
        public void SprintPressedTogetherWithShot_GivesNormalShot()
        {
            var s = new TouchControlScheme();
            Assert.AreEqual(ControlAction.Shoot, ShootWithSprint(s, 0f, 0f, 0.3f).Action);
            // Inside the simultaneity threshold, sprint slightly after the shot still counts as together.
            var s2 = new TouchControlScheme();
            Assert.AreEqual(ControlAction.Shoot, ShootWithSprint(s2, 0.03f, 0f, 0.3f).Action);
        }

        [Test]
        public void SprintPressedDuringCharge_MakesShotDriven()
        {
            var s = new TouchControlScheme();
            var i = ShootWithSprint(s, sprintDown: 0.15f, shootDown: 0f, release: 0.4f);
            Assert.AreEqual(ControlAction.DrivenShot, i.Action);
        }

        [Test]
        public void SprintTappedDuringCharge_StaysDrivenAfterSprintLifts()
        {
            var s = new TouchControlScheme();
            var i = ShootWithSprint(s, sprintDown: 0.15f, shootDown: 0f, release: 0.4f, releaseSprintFirst: true);
            Assert.AreEqual(ControlAction.DrivenShot, i.Action);
        }

        [Test]
        public void DrivenThreshold_IsInclusiveAndConfigurable()
        {
            float d = TouchControlScheme.DefaultDrivenSprintMinDelaySeconds;
            Assert.AreEqual(ControlAction.DrivenShot, ShootWithSprint(new TouchControlScheme(), d, 0f, 0.4f).Action);
            Assert.AreEqual(ControlAction.Shoot, ShootWithSprint(new TouchControlScheme(), d * 0.5f, 0f, 0.4f).Action);
            var strict = new TouchControlScheme { DrivenSprintMinDelaySeconds = 0.2f };
            Assert.AreEqual(ControlAction.Shoot, ShootWithSprint(strict, 0.15f, 0f, 0.4f).Action);
        }

        [Test]
        public void SprintDuringCharge_DrivesPassAndThrough()
        {
            var s = new TouchControlScheme();
            ControlVec2 sprint = s.Layout.GetButton(TouchSlot.Sprint).Center;
            foreach (var pair in new[] { (TouchSlot.Primary, ControlAction.DrivenPass), (TouchSlot.Through, ControlAction.DrivenThrough) })
            {
                ControlVec2 kick = s.Layout.GetButton(pair.Item1).Center;
                s.Tick(One(2, kick, ControlTouchPhase.Began), ControlContext.Attack(10f));
                s.Tick(new[] { new TouchPoint(2, kick, ControlTouchPhase.Stationary), new TouchPoint(1, sprint, ControlTouchPhase.Began) },
                    ControlContext.Attack(10.2f));
                var i = s.Tick(new[] { new TouchPoint(1, sprint, ControlTouchPhase.Ended), new TouchPoint(2, kick, ControlTouchPhase.Ended) },
                    ControlContext.Attack(10.4f));
                Assert.AreEqual(pair.Item2, i.Action);
            }
        }

        [Test]
        public void ResolveAttack_DrivenFlag_OnlyChangesTapKicks()
        {
            Assert.AreEqual(ControlAction.DrivenShot, TouchControlScheme.ResolveAttack(TouchSlot.Strike, TouchGesture.Tap, true, false));
            Assert.AreEqual(ControlAction.Shoot, TouchControlScheme.ResolveAttack(TouchSlot.Strike, TouchGesture.Tap, false, false));
            Assert.AreEqual(ControlAction.ChipShot, TouchControlScheme.ResolveAttack(TouchSlot.Strike, TouchGesture.SwipeUp, true, false));
        }

        [Test]
        public void HoldToCharge_LongerHoldMorePower()
        {
            var s = new TouchControlScheme();
            float shortP = TapSlot(s, TouchSlot.Strike, ControlContext.Attack(0f), ControlContext.Attack(0.05f)).Power;
            float longP = TapSlot(s, TouchSlot.Strike, ControlContext.Attack(1f), ControlContext.Attack(2.2f)).Power;
            Assert.Greater(longP, shortP);
            Assert.AreEqual(1f, longP, 1e-5f);
        }

        [Test]
        public void TouchRelease_DuringWindUp_FiresOnContact()
        {
            var s = new TouchControlScheme();
            var released = TapSlot(s, TouchSlot.Primary, ControlContext.Attack(0f, false), ControlContext.Attack(0.1f, false));
            Assert.AreEqual(ControlAction.None, released.Action);
            Assert.IsTrue(released.KickBuffered);
            var contact = s.Tick(new TouchPoint[0], ControlContext.Attack(0.2f, true));
            Assert.AreEqual(ControlAction.Pass, contact.Action);
        }

        [Test]
        public void Defence_TackleSwipe_IsSlide_SwitchSwipe_IsDirectional()
        {
            var s = new TouchControlScheme();
            Assert.AreEqual(ControlAction.SlideTackle, TapSlot(s, TouchSlot.Strike,
                ControlContext.Defence(0f), ControlContext.Defence(0.1f), new ControlVec2(0.08f, 0f)).Action);
            var sw = TapSlot(s, TouchSlot.Primary, ControlContext.Defence(1f), ControlContext.Defence(1.1f), new ControlVec2(-0.08f, 0f));
            Assert.AreEqual(ControlAction.SwitchPlayer, sw.Action);
            Assert.IsTrue(sw.DirectionalSwitch);
            Assert.Less(sw.Aim.X, -0.9f);
        }

        [Test]
        public void Defence_PressButton_HoldContain_SwipeUpTeammatePress()
        {
            var s = new TouchControlScheme();
            ControlVec2 c = s.Layout.GetButton(TouchSlot.Through).Center;
            var hold = s.Tick(One(1, c, ControlTouchPhase.Began), ControlContext.Defence(0f));
            Assert.AreEqual(ControlAction.Contain, hold.Action);
            var up = s.Tick(One(1, c + new ControlVec2(0f, 0.1f), ControlTouchPhase.Moved), ControlContext.Defence(0.1f));
            Assert.AreEqual(ControlAction.TeammatePress, up.Action);
            var rel = s.Tick(One(1, c, ControlTouchPhase.Ended), ControlContext.Defence(0.2f));
            Assert.AreEqual(ControlAction.None, rel.Action);
        }

        [Test]
        public void FloatingStick_SpawnsWhereThumbLands()
        {
            var s = new TouchControlScheme();
            var start = new ControlVec2(0.2f, 0.3f);
            s.Tick(One(5, start, ControlTouchPhase.Began), ControlContext.Attack(0f));
            Assert.IsTrue(s.StickActive);
            Assert.AreEqual(0.2f, s.StickOrigin.X, 1e-6f);
            var i = s.Tick(One(5, start + new ControlVec2(0f, 0.11f), ControlTouchPhase.Moved), ControlContext.Attack(0.1f));
            Assert.AreEqual(1f, i.Move.Y, 1e-4f);
            var small = s.Tick(One(5, start + new ControlVec2(0f, 0.005f), ControlTouchPhase.Moved), ControlContext.Attack(0.2f));
            Assert.AreEqual(0f, small.Move.Magnitude, 1e-6f);
        }

        [Test]
        public void TouchOutsideStickZone_DoesNotSpawnStick()
        {
            var s = new TouchControlScheme();
            s.Tick(One(5, new ControlVec2(0.6f, 0.9f), ControlTouchPhase.Began), ControlContext.Attack(0f));
            Assert.IsFalse(s.StickActive);
        }

        [Test]
        public void Layouts_4x3And3x2_ButtonsOnScreenNotOverlappingAndClearOfStick()
        {
            foreach (float aspect in new[] { 4f / 3f, 1.5f })
            {
                foreach (bool left in new[] { false, true })
                {
                    var l = TouchLayout.ForAspect(aspect, left);
                    for (int i = 0; i < l.Buttons.Length; i++)
                    {
                        var b = l.Buttons[i];
                        float rx = b.Radius / aspect;
                        Assert.GreaterOrEqual(b.Center.X - rx, 0f);
                        Assert.LessOrEqual(b.Center.X + rx, 1f);
                        Assert.GreaterOrEqual(b.Center.Y - b.Radius, 0f);
                        Assert.IsFalse(l.StickZone.Contains(b.Center));
                        Assert.AreEqual(b.Slot, l.HitTest(b.Center));
                        for (int j = i + 1; j < l.Buttons.Length; j++)
                        {
                            var o = l.Buttons[j];
                            Assert.Greater(l.Distance(b.Center, o.Center), (b.Radius + o.Radius) * l.HitSlop);
                        }
                    }
                }
            }
        }

        [Test]
        public void Layout_SamePhysicalOffsetFromRightEdge_OnBothAspects()
        {
            var a = TouchLayout.IPad4x3().GetButton(TouchSlot.Strike);
            var b = TouchLayout.IPad3x2().GetButton(TouchSlot.Strike);
            Assert.AreEqual((1f - a.Center.X) * (4f / 3f), (1f - b.Center.X) * 1.5f, 1e-5f);
            Assert.AreEqual(a.Radius, b.Radius, 1e-6f);
        }
    }
}
