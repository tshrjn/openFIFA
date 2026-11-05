using NUnit.Framework;
using OpenFifa.Core;
using OpenFifa.Gameplay;
using UnityEngine;

namespace OpenFifa.Tests.Editor.Integration
{
    /// <summary>Banner content -> OffsideBannerInfo for the HUD lower-third (offender's team colour).</summary>
    [TestFixture]
    [Category("FID_INTEGRATION")]
    public class OffsideBannerInfoTests
    {
        [Test]
        public void BannerInfo_FromContent_PicksOffenderTeamColour()
        {
            var home = new Color(0.8f, 0.1f, 0.1f);
            var away = new Color(0.1f, 0.2f, 0.9f);
            var info = OffsideBannerInfo.From(new OffsideBannerContent(7, "Saka", "ARS", false), home, away);
            Assert.AreEqual(7, info.ShirtNumber);
            Assert.AreEqual("Saka", info.PlayerName);
            Assert.AreEqual("ARS", info.TeamLabel);
            Assert.AreEqual(away, info.TeamColor);
            Assert.AreEqual(home, OffsideBannerInfo.From(new OffsideBannerContent(9, "X", "H", true), home, away).TeamColor);
        }
    }
}
