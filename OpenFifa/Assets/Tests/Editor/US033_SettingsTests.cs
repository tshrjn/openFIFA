using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("US-033")]
    public class US033_SettingsTests
    {
        [Test]
        public void SettingsLogic_DefaultSFXVolume_Is75()
        {
            var logic = new SettingsLogic();
            Assert.AreEqual(75f, logic.SFXVolume);
        }

        [Test]
        public void SettingsLogic_DefaultMusicVolume_Is75()
        {
            var logic = new SettingsLogic();
            Assert.AreEqual(75f, logic.MusicVolume);
        }

        [Test]
        public void SettingsLogic_DefaultDifficulty_IsMedium()
        {
            var logic = new SettingsLogic();
            Assert.AreEqual(GameDifficulty.Medium, logic.Difficulty);
        }

        [Test]
        public void SettingsLogic_SetSFXVolume_Clamped()
        {
            var logic = new SettingsLogic();
            logic.SetSFXVolume(150f);
            Assert.AreEqual(100f, logic.SFXVolume);

            logic.SetSFXVolume(-10f);
            Assert.AreEqual(0f, logic.SFXVolume);
        }

        [Test]
        public void SettingsLogic_VolumeToDb_ZeroIsMinusEighty()
        {
            float db = SettingsLogic.VolumeToDb(0f);
            Assert.AreEqual(-80f, db, 0.1f, "0 volume should map to -80dB");
        }

        [Test]
        public void SettingsLogic_VolumeToDb_HundredIsZero()
        {
            float db = SettingsLogic.VolumeToDb(100f);
            Assert.AreEqual(0f, db, 0.1f, "100 volume should map to 0dB");
        }

        [Test]
        public void SettingsLogic_VolumeToDb_FiftyIsAboutNeg6()
        {
            float db = SettingsLogic.VolumeToDb(50f);
            Assert.Greater(db, -7f);
            Assert.Less(db, -5f);
        }

        [Test]
        public void GameDifficulty_HasThreeValues()
        {
            var values = System.Enum.GetValues(typeof(GameDifficulty));
            Assert.AreEqual(3, values.Length);
        }

        [Test]
        public void SettingsLogic_PlayerPrefsKeys_Correct()
        {
            Assert.AreEqual("SFXVolume", SettingsLogic.SFXVolumeKey);
            Assert.AreEqual("MusicVolume", SettingsLogic.MusicVolumeKey);
            Assert.AreEqual("Difficulty", SettingsLogic.DifficultyKey);
        }
    }
}
