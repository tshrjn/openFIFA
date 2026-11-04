using System.Collections.Generic;
using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor.LocalMultiplayer
{
    [TestFixture]
    [Category("LOCAL_2P")]
    public class CoopSwitchArbiterTests
    {
        private static List<ControlPlayerInfo> Team()
        {
            var l = new List<ControlPlayerInfo>();
            for (int i = 1; i <= 10; i++) l.Add(new ControlPlayerInfo(i, new SimVec3(i * 5f - 25f, 0f, 0f), 7.5f));
            return l;
        }

        [Test]
        public void Resolve_NoClash_NothingChanges()
        {
            var prev = new[] { 3, 7 };
            var wanted = new[] { 4, 7 };
            int moved = CoopSwitchArbiter.Resolve(prev, wanted, new bool[2], null, Team(), SimVec3.Zero, SimVec3.Zero, 2);
            Assert.AreEqual(0, moved);
            CollectionAssert.AreEqual(new[] { 4, 7 }, wanted);
        }

        [Test]
        public void Resolve_PassToPartner_PartnerKeepsReceiverPasserStaysOnOwnPlayer()
        {
            // Human 0 on player 3 passes to player 7 (human 1's player); his selector follows the ball to 7.
            var prev = new[] { 3, 7 };
            var wanted = new[] { 7, 7 };
            CoopSwitchArbiter.Resolve(prev, wanted, new bool[2], null, Team(), SimVec3.Zero, SimVec3.Zero, 2);
            CollectionAssert.AreEqual(new[] { 3, 7 }, wanted);
        }

        [Test]
        public void Resolve_ManualSwitchOntoPartner_GetsNextBestFreePlayer()
        {
            var prev = new[] { 3, 5 };
            var wanted = new[] { 5, 5 };
            var ball = new SimVec3(0f, 0f, 0f); // player 5 sits on the ball, 4 and 6 next to it
            CoopSwitchArbiter.Resolve(prev, wanted, new[] { true, false }, null, Team(), ball, SimVec3.Zero, 2);
            Assert.AreEqual(5, wanted[1]);
            Assert.AreNotEqual(5, wanted[0]);
            Assert.AreNotEqual(3, wanted[0], "a manual switch should move him");
            Assert.That(wanted[0], Is.EqualTo(4).Or.EqualTo(6));
        }

        [Test]
        public void Resolve_BothAutoSwitchToNewPlayer_ManualWinsElseLowerSlot()
        {
            var prev = new[] { 2, 9 };
            var wanted = new[] { 5, 5 };
            CoopSwitchArbiter.Resolve(prev, wanted, new[] { false, true }, null, Team(), SimVec3.Zero, SimVec3.Zero, 2);
            Assert.AreEqual(5, wanted[1]);
            Assert.AreEqual(2, wanted[0]);

            wanted = new[] { 5, 5 };
            CoopSwitchArbiter.Resolve(prev, wanted, new bool[2], null, Team(), SimVec3.Zero, SimVec3.Zero, 2);
            Assert.AreEqual(5, wanted[0]);
            Assert.AreEqual(9, wanted[1]);
        }

        [Test]
        public void Resolve_DirectionalSwitchClash_PicksFreePlayerInFlickDirection()
        {
            var prev = new[] { 5, 6 };
            var wanted = new[] { 6, 6 };
            var flick = new[] { new SimVec3(1f, 0f, 0f), SimVec3.Zero };
            CoopSwitchArbiter.Resolve(prev, wanted, new[] { true, false }, flick, Team(), SimVec3.Zero, SimVec3.Zero, 2);
            Assert.AreEqual(6, wanted[1]);
            Assert.AreEqual(7, wanted[0], "next free player to the right");
        }
    }
}
