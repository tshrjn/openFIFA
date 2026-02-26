using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("US-023")]
    public class US023_SoundTests
    {
        [Test]
        public void SoundEventMapper_KickoffState_MapsToWhistle()
        {
            var mapper = new SoundEventMapper();
            var sound = mapper.MapMatchStateChange(MatchState.PreKickoff, MatchState.FirstHalf);
            Assert.AreEqual(SoundEventType.Whistle, sound);
        }

        [Test]
        public void SoundEventMapper_HalfTime_MapsToWhistle()
        {
            var mapper = new SoundEventMapper();
            var sound = mapper.MapMatchStateChange(MatchState.FirstHalf, MatchState.HalfTime);
            Assert.AreEqual(SoundEventType.Whistle, sound);
        }

        [Test]
        public void SoundEventMapper_FullTime_MapsToWhistle()
        {
            var mapper = new SoundEventMapper();
            var sound = mapper.MapMatchStateChange(MatchState.SecondHalf, MatchState.FullTime);
            Assert.AreEqual(SoundEventType.Whistle, sound);
        }

        [Test]
        public void SoundEventMapper_GoalCelebration_MapsToGoalCheer()
        {
            var mapper = new SoundEventMapper();
            var sound = mapper.MapMatchStateChange(MatchState.FirstHalf, MatchState.GoalCelebration);
            Assert.AreEqual(SoundEventType.GoalCheer, sound);
        }

        [Test]
        public void SoundEventMapper_Pause_MapsToNone()
        {
            var mapper = new SoundEventMapper();
            var sound = mapper.MapMatchStateChange(MatchState.FirstHalf, MatchState.Paused);
            Assert.AreEqual(SoundEventType.None, sound);
        }

        [Test]
        public void SoundEventMapper_SecondHalfStart_MapsToWhistle()
        {
            var mapper = new SoundEventMapper();
            var sound = mapper.MapMatchStateChange(MatchState.HalfTime, MatchState.SecondHalf);
            Assert.AreEqual(SoundEventType.Whistle, sound);
        }

        [Test]
        public void SoundEventType_EnumValues_HasAllExpectedValues()
        {
            Assert.IsTrue(System.Enum.IsDefined(typeof(SoundEventType), SoundEventType.None));
            Assert.IsTrue(System.Enum.IsDefined(typeof(SoundEventType), SoundEventType.Whistle));
            Assert.IsTrue(System.Enum.IsDefined(typeof(SoundEventType), SoundEventType.Kick));
            Assert.IsTrue(System.Enum.IsDefined(typeof(SoundEventType), SoundEventType.GoalCheer));
            Assert.IsTrue(System.Enum.IsDefined(typeof(SoundEventType), SoundEventType.CrowdAmbient));
        }

        [Test]
        public void SoundEventMapper_GoalCelebrationToPreKickoff_MapsToNone()
        {
            var mapper = new SoundEventMapper();
            var sound = mapper.MapMatchStateChange(MatchState.GoalCelebration, MatchState.PreKickoff);
            Assert.AreEqual(SoundEventType.None, sound);
        }
    }
}
