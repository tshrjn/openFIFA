using System.Collections.Generic;
using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("ExternalDisplay")]
    public class DesktopDisplayPolicyTests
    {
        private static readonly DisplayCandidate MacBookPanel = new DisplayCandidate("Built-in Retina Display", 3024, 1964);
        private static readonly DisplayCandidate Monitor4K = new DisplayCandidate("LG UltraFine", 3840, 2160);
        private static readonly DisplayCandidate Monitor1080 = new DisplayCandidate("Dell P2419H", 1920, 1080);

        [Test]
        public void ChooseDisplay_Auto_MacBookOnly_PicksBuiltIn()
        {
            var displays = new List<DisplayCandidate> { MacBookPanel };
            Assert.AreEqual(0, DesktopDisplayPolicy.ChooseDisplay(displays, DesktopDisplayPolicy.AutoPreference, 0, 1440));
        }

        [Test]
        public void ChooseDisplay_Auto_4KPluggedIn_MovesToMonitor()
        {
            var displays = new List<DisplayCandidate> { MacBookPanel, Monitor4K };
            Assert.AreEqual(1, DesktopDisplayPolicy.ChooseDisplay(displays, DesktopDisplayPolicy.AutoPreference, 0, 1440));
        }

        [Test]
        public void ChooseDisplay_Auto_SmallerMonitor_StaysOnRetinaPanel()
        {
            var displays = new List<DisplayCandidate> { MacBookPanel, Monitor1080 };
            Assert.AreEqual(0, DesktopDisplayPolicy.ChooseDisplay(displays, DesktopDisplayPolicy.AutoPreference, 0, 1440));
        }

        [Test]
        public void ChooseDisplay_PinnedMonitor_WinsWhileConnected()
        {
            var displays = new List<DisplayCandidate> { MacBookPanel, Monitor4K, Monitor1080 };
            Assert.AreEqual(2, DesktopDisplayPolicy.ChooseDisplay(displays, "Dell P2419H", 0, 1440));
        }

        [Test]
        public void ChooseDisplay_PinnedMonitorUnplugged_FallsBackToLargest()
        {
            var displays = new List<DisplayCandidate> { MacBookPanel };
            Assert.AreEqual(0, DesktopDisplayPolicy.ChooseDisplay(displays, "LG UltraFine", 0, 1440));
        }

        [Test]
        public void ChooseDisplay_Empty_ReturnsMinusOne()
        {
            Assert.AreEqual(-1, DesktopDisplayPolicy.ChooseDisplay(new List<DisplayCandidate>(), null, 0, 1440));
        }

        [Test]
        public void ChooseDisplay_Auto_MacBookPlus1440pMonitor_MovesToMonitor()
        {
            var air = new DisplayCandidate("Built-in Liquid Retina Display", 2560, 1664);
            var qhd = new DisplayCandidate("DELL U2723QE", 2560, 1440);
            var displays = new List<DisplayCandidate> { air, qhd };
            Assert.AreEqual(1, DesktopDisplayPolicy.ChooseDisplay(displays, DesktopDisplayPolicy.AutoPreference, 0, 1440),
                "a 1440p monitor beats the laptop panel even though the panel has more pixels");
        }

        [Test]
        public void ChooseDisplay_PinnedSecondOfTwoIdenticalMonitors_PicksIt()
        {
            var displays = new List<DisplayCandidate> { MacBookPanel, Monitor4K, Monitor4K };
            Assert.AreEqual(2, DesktopDisplayPolicy.ChooseDisplay(displays, "LG UltraFine", 1, 1440));
            Assert.AreEqual(1, DesktopDisplayPolicy.Occurrence(displays, 2));
        }
    }
}
