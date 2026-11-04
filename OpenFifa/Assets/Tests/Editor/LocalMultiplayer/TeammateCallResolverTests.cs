using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor.LocalMultiplayer
{
    [TestFixture]
    [Category("LOCAL-2P")]
    public class TeammateCallResolverTests
    {
        private const float Dt = 1f / 60f;
        private TeammateCallResolver _r;
        private TeamAIUserDirectives _d;
        private float _t;

        [SetUp]
        public void SetUp()
        {
            _r = new TeammateCallResolver();
            _d = TeamAIUserDirectives.None;
            _t = 10f;
        }

        private GamepadFrame Tick(GamepadButton b, bool attack = true, bool mateOnBall = true, bool charging = false,
            ControlVec2 rs = default)
        {
            _t += Dt;
            var ctx = attack ? ControlContext.Attack(_t, false) : ControlContext.Defence(_t);
            return _r.Filter(new GamepadFrame(b, default, rs), ctx, 7, mateOnBall, new SimVec3(0f, 0f, 0f), 0f, charging, ref _d);
        }

        [Test]
        public void OffBall_PassButton_CallsForPassAndHidesThePress()
        {
            var f = Tick(GamepadButton.South);
            Assert.AreEqual(TeammateCall.PassToFeet, _r.LastCall);
            Assert.IsTrue(_d.PassCallActive(_t));
            Assert.AreEqual(7, _d.PassCallerId);
            Assert.IsFalse(_d.PassCallThrough);
            Assert.AreEqual(GamepadButton.None, f.Buttons & GamepadButton.South, "kick resolver must not see the press");
            f = Tick(GamepadButton.South);
            Assert.AreEqual(GamepadButton.None, f.Buttons & GamepadButton.South, "nor the hold");
            Assert.IsFalse(_d.PassCallActive(_t + TeammateCallResolver.PassCallSeconds + 0.1f), "call expires");
        }

        [Test]
        public void OffBall_ThroughButton_CallsForThroughBall()
        {
            Tick(GamepadButton.North);
            Assert.AreEqual(TeammateCall.PassInBehind, _r.LastCall);
            Assert.IsTrue(_d.PassCallThrough);
        }

        [Test]
        public void BallInFlight_PassButton_IsLeftForFirstTimePass()
        {
            var f = Tick(GamepadButton.South, mateOnBall: false);
            Assert.AreEqual(TeammateCall.None, _r.LastCall);
            Assert.AreEqual(GamepadButton.South, f.Buttons & GamepadButton.South);
        }

        [Test]
        public void LBTap_RequestsRun()
        {
            Tick(GamepadButton.LeftShoulder, mateOnBall: false);
            Tick(GamepadButton.LeftShoulder, mateOnBall: false);
            Tick(GamepadButton.None, mateOnBall: false);
            Assert.AreEqual(TeammateCall.Run, _r.LastCall);
            Assert.IsTrue(_d.RunActive(_t));
            Assert.IsFalse(_d.RunHasTarget);
        }

        [Test]
        public void LBHeldTooLong_NoRun()
        {
            for (int i = 0; i < 30; i++) Tick(GamepadButton.LeftShoulder, mateOnBall: false);
            Tick(GamepadButton.None, mateOnBall: false);
            Assert.IsFalse(_d.RunActive(_t));
        }

        [Test]
        public void LBWithKick_IsModifierNotRun()
        {
            Tick(GamepadButton.LeftShoulder, mateOnBall: false);
            Tick(GamepadButton.LeftShoulder | GamepadButton.East, mateOnBall: false, charging: true);
            Tick(GamepadButton.None, mateOnBall: false);
            Assert.IsFalse(_d.RunActive(_t));
        }

        [Test]
        public void LBPlusFlick_DirectedRunAndNoSkillMove()
        {
            Tick(GamepadButton.LeftShoulder, mateOnBall: false);
            var f = Tick(GamepadButton.LeftShoulder, mateOnBall: false, rs: new ControlVec2(1f, 0f));
            Assert.AreEqual(TeammateCall.DirectedRun, _r.LastCall);
            Assert.IsTrue(_d.RunHasTarget);
            Assert.Greater(_d.RunTarget.X, 10f, "flick right with camera yaw 0 is +X");
            Assert.AreEqual(0f, f.RightStick.Magnitude, 1e-5f, "right stick hidden from the skill-move resolver");
            Tick(GamepadButton.None, mateOnBall: false, rs: new ControlVec2(1f, 0f));
            Assert.AreEqual(TeammateCall.None, _r.LastCall, "LB release after a directed run is not also a tap");
        }

        [Test]
        public void FlickWithoutLB_PassesThroughForSkillMove()
        {
            var f = Tick(GamepadButton.None, mateOnBall: false, rs: new ControlVec2(1f, 0f));
            Assert.AreEqual(1f, f.RightStick.Magnitude, 1e-5f);
        }

        [Test]
        public void Defence_RBHeld_RequestsPressOnlyWhileHeld()
        {
            Tick(GamepadButton.RightShoulder, attack: false);
            Assert.AreEqual(TeammateCall.TeammatePress, _r.LastCall);
            Assert.IsTrue(_d.PressActive(_t));
            for (int i = 0; i < 30; i++) Tick(GamepadButton.None, attack: false);
            Assert.IsFalse(_d.PressActive(_t));
        }

        [Test]
        public void ResolveArrival_CallerGetsBall_ClearsCall()
        {
            _d.CallForPass(7, false, 100f);
            TeammateCallResolver.ResolveArrival(7, 3, true, ref _d);
            Assert.IsTrue(_d.PassCallActive(_t));
            TeammateCallResolver.ResolveArrival(7, 7, true, ref _d);
            Assert.IsFalse(_d.PassCallActive(_t));
        }
    }
}
