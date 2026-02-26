using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("US-038")]
    public class US038_HapticTests
    {
        [Test]
        public void FeedbackIntensity_HasThreeValues()
        {
            var values = System.Enum.GetValues(typeof(FeedbackIntensity));
            Assert.AreEqual(3, values.Length);
        }

        [Test]
        public void FeedbackMapper_GoalScored_MapsToHeavy()
        {
            var mapper = new FeedbackEventMapper();
            var intensity = mapper.MapGoalScored();
            Assert.AreEqual(FeedbackIntensity.Heavy, intensity);
        }

        [Test]
        public void FeedbackMapper_Tackle_MapsToMedium()
        {
            var mapper = new FeedbackEventMapper();
            var intensity = mapper.MapTackle();
            Assert.AreEqual(FeedbackIntensity.Medium, intensity);
        }

        [Test]
        public void FeedbackMapper_Whistle_MapsToLight()
        {
            var mapper = new FeedbackEventMapper();
            var intensity = mapper.MapWhistle();
            Assert.AreEqual(FeedbackIntensity.Light, intensity);
        }

        [Test]
        public void FeedbackMapper_AllIntensitiesUsed()
        {
            var mapper = new FeedbackEventMapper();
            Assert.AreEqual(FeedbackIntensity.Heavy, mapper.MapGoalScored());
            Assert.AreEqual(FeedbackIntensity.Medium, mapper.MapTackle());
            Assert.AreEqual(FeedbackIntensity.Light, mapper.MapWhistle());
        }

        [Test]
        public void PlatformFeedbackType_HasCorrectValues()
        {
            Assert.IsTrue(System.Enum.IsDefined(typeof(PlatformFeedbackType), PlatformFeedbackType.Haptic));
            Assert.IsTrue(System.Enum.IsDefined(typeof(PlatformFeedbackType), PlatformFeedbackType.ScreenShake));
            Assert.IsTrue(System.Enum.IsDefined(typeof(PlatformFeedbackType), PlatformFeedbackType.Audio));
        }
    }
}
