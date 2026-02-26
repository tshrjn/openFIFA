using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("US-038")]
    public class US038_FeedbackTests
    {
        [Test]
        public void FeedbackIntensity_EnumValues_HasThreeValues()
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
        public void FeedbackMapper_AllEvents_AllIntensitiesUsed()
        {
            var mapper = new FeedbackEventMapper();
            Assert.AreEqual(FeedbackIntensity.Heavy, mapper.MapGoalScored());
            Assert.AreEqual(FeedbackIntensity.Medium, mapper.MapTackle());
            Assert.AreEqual(FeedbackIntensity.Light, mapper.MapWhistle());
        }

        [Test]
        public void FeedbackChannelType_EnumValues_HasCorrectValues()
        {
            Assert.IsTrue(System.Enum.IsDefined(typeof(FeedbackChannelType), FeedbackChannelType.ControllerRumble));
            Assert.IsTrue(System.Enum.IsDefined(typeof(FeedbackChannelType), FeedbackChannelType.ScreenShake));
            Assert.IsTrue(System.Enum.IsDefined(typeof(FeedbackChannelType), FeedbackChannelType.Audio));
        }

        [Test]
        public void RumbleConfig_HeavyGoal_HighMotorValues()
        {
            var mapper = new FeedbackEventMapper();
            var rumble = mapper.GetRumbleConfig(FeedbackIntensity.Heavy);
            Assert.AreEqual(1.0f, rumble.LowFrequency, 0.001f);
            Assert.AreEqual(0.8f, rumble.HighFrequency, 0.001f);
            Assert.AreEqual(0.5f, rumble.Duration, 0.001f);
        }

        [Test]
        public void RumbleConfig_MediumTackle_MediumMotorValues()
        {
            var mapper = new FeedbackEventMapper();
            var rumble = mapper.GetRumbleConfig(FeedbackIntensity.Medium);
            Assert.AreEqual(0.5f, rumble.LowFrequency, 0.001f);
            Assert.AreEqual(0.4f, rumble.HighFrequency, 0.001f);
            Assert.AreEqual(0.3f, rumble.Duration, 0.001f);
        }

        [Test]
        public void RumbleConfig_LightWhistle_LowMotorValues()
        {
            var mapper = new FeedbackEventMapper();
            var rumble = mapper.GetRumbleConfig(FeedbackIntensity.Light);
            Assert.AreEqual(0.2f, rumble.LowFrequency, 0.001f);
            Assert.AreEqual(0.1f, rumble.HighFrequency, 0.001f);
            Assert.AreEqual(0.15f, rumble.Duration, 0.001f);
        }

        [Test]
        public void RumbleConfig_LowFrequency_GreaterThanOrEqualToHighFrequency()
        {
            var mapper = new FeedbackEventMapper();
            // For all intensities, low-frequency motor (deep rumble) should be >= high-frequency (buzz)
            foreach (FeedbackIntensity intensity in System.Enum.GetValues(typeof(FeedbackIntensity)))
            {
                var rumble = mapper.GetRumbleConfig(intensity);
                Assert.GreaterOrEqual(rumble.LowFrequency, rumble.HighFrequency,
                    $"Low-frequency motor should be >= high-frequency for {intensity}");
            }
        }

        [Test]
        public void RumbleConfig_Duration_PositiveForAllIntensities()
        {
            var mapper = new FeedbackEventMapper();
            foreach (FeedbackIntensity intensity in System.Enum.GetValues(typeof(FeedbackIntensity)))
            {
                var rumble = mapper.GetRumbleConfig(intensity);
                Assert.Greater(rumble.Duration, 0f,
                    $"Rumble duration should be positive for {intensity}");
            }
        }
    }
}
