using System.Collections.Generic;
using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor.LocalMultiplayer
{
    [TestFixture]
    [Category("LOCAL_2P")]
    public class ControllerAssignmentTests
    {
        [Test]
        public void Connect_NewController_JoinsInTheMiddle()
        {
            var a = new ControllerAssignment();
            Assert.AreEqual(0, a.Connect(11));
            Assert.AreEqual(1, a.Connect(22));
            Assert.AreEqual(ControllerSide.Unassigned, a.Seats[0].Side);
            Assert.AreEqual(LocalMatchMode.None, a.Mode);
            Assert.IsFalse(a.CanStart);
        }

        [Test]
        public void Push_LeftAndRight_PicksHomeAndAway_HeadToHead()
        {
            var a = new ControllerAssignment();
            a.Connect(11); a.Connect(22);
            Assert.AreEqual(ControllerSide.Home, a.Push(11, -1));
            Assert.AreEqual(ControllerSide.Away, a.Push(22, +1));
            Assert.AreEqual(LocalMatchMode.HeadToHead, a.Mode);
            Assert.AreEqual(ControllerSide.Unassigned, a.Push(22, -1), "pushing back toward the middle unassigns");
        }

        [Test]
        public void Push_BothSameSide_IsCoopVsAI()
        {
            var a = new ControllerAssignment();
            a.Connect(11); a.Connect(22);
            a.Push(11, -1); a.Push(22, -1);
            Assert.AreEqual(LocalMatchMode.CoopVsAI, a.Mode);
            var home = new List<ControllerSeat>();
            a.SeatsFor(SimTeam.Home, home);
            Assert.AreEqual(2, home.Count);
            Assert.AreEqual(0, home[0].Slot);
        }

        [Test]
        public void Push_SideFull_Refused()
        {
            var a = new ControllerAssignment();
            a.Connect(1); a.Connect(2); a.Connect(3);
            a.Push(1, -1); a.Push(2, -1);
            Assert.AreEqual(ControllerSide.Unassigned, a.Push(3, -1));
            Assert.AreEqual(ControllerSide.Away, a.Push(3, +1));
            Assert.AreEqual(LocalMatchMode.CoopHeadToHead, a.Mode);
        }

        [Test]
        public void Disconnect_InMatch_OrphansSeatUntilAnyControllerReconnects()
        {
            var a = new ControllerAssignment();
            a.Connect(11); a.Connect(22);
            a.Push(11, -1); a.Push(22, +1);
            a.LockForMatch();
            ControllerSeat orphaned = default, restored = default;
            a.SeatOrphaned += s => orphaned = s;
            a.SeatRestored += s => restored = s;

            a.Disconnect(22);
            Assert.IsTrue(a.AwaitingReconnect);
            Assert.AreEqual(1, orphaned.Slot);
            Assert.AreEqual(ControllerSide.Away, a.Seats[1].Side, "the seat keeps its side");

            Assert.AreEqual(1, a.Connect(99), "a different pad can take over the seat");
            Assert.IsFalse(a.AwaitingReconnect);
            Assert.AreEqual(99, restored.DeviceId);
            Assert.AreEqual(ControllerSide.Away, a.Seats[1].Side);
        }

        [Test]
        public void Connect_InMatchWithNoOrphans_Ignored()
        {
            var a = new ControllerAssignment();
            a.Connect(11); a.Push(11, -1);
            a.LockForMatch();
            Assert.AreEqual(-1, a.Connect(22));
            Assert.AreEqual(ControllerSide.Home, a.Push(11, +1), "sides are locked during a match");
        }

        [Test]
        public void Disconnect_OutOfMatch_RemovesSeatAndFreesSlot()
        {
            var a = new ControllerAssignment();
            a.Connect(11); a.Connect(22);
            a.Disconnect(11);
            Assert.AreEqual(1, a.Seats.Count);
            Assert.AreEqual(0, a.Connect(33), "freed slot 0 is reused");
        }

        [Test]
        public void LockForMatch_NobodyOnASide_Throws()
        {
            var a = new ControllerAssignment();
            a.Connect(11);
            Assert.Throws<System.InvalidOperationException>(() => a.LockForMatch());
        }
    }
}
