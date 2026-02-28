using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("US018")]
    public class US018_BallOwnershipTests
    {
        [Test]
        public void BallOwnershipLogic_InitialOwner_IsNull()
        {
            var logic = new BallOwnershipLogic();
            Assert.AreEqual(-1, logic.CurrentOwnerId, "Initial owner should be -1 (no owner)");
        }

        [Test]
        public void BallOwnershipLogic_SetOwner_UpdatesCurrentOwner()
        {
            var logic = new BallOwnershipLogic();
            logic.SetOwner(3);
            Assert.AreEqual(3, logic.CurrentOwnerId);
        }

        [Test]
        public void BallOwnershipLogic_Release_ClearsOwner()
        {
            var logic = new BallOwnershipLogic();
            logic.SetOwner(2);
            logic.Release();
            Assert.AreEqual(-1, logic.CurrentOwnerId, "Release should clear owner to -1");
        }

        [Test]
        public void BallOwnershipLogic_SetOwner_FiresOwnerChangedEvent()
        {
            var logic = new BallOwnershipLogic();
            int oldOwner = -99, newOwner = -99;
            logic.OnOwnerChanged += (o, n) => { oldOwner = o; newOwner = n; };

            logic.SetOwner(5);
            Assert.AreEqual(-1, oldOwner, "Previous owner should be -1");
            Assert.AreEqual(5, newOwner, "New owner should be 5");
        }

        [Test]
        public void BallOwnershipLogic_Release_FiresOwnerChangedEvent()
        {
            var logic = new BallOwnershipLogic();
            logic.SetOwner(3);

            int oldOwner = -99, newOwner = -99;
            logic.OnOwnerChanged += (o, n) => { oldOwner = o; newOwner = n; };

            logic.Release();
            Assert.AreEqual(3, oldOwner);
            Assert.AreEqual(-1, newOwner);
        }

        [Test]
        public void BallOwnershipLogic_SetSameOwner_DoesNotFireEvent()
        {
            var logic = new BallOwnershipLogic();
            logic.SetOwner(2);

            bool eventFired = false;
            logic.OnOwnerChanged += (o, n) => { eventFired = true; };

            logic.SetOwner(2);
            Assert.IsFalse(eventFired, "Setting same owner should not fire event");
        }

        [Test]
        public void BallOwnershipLogic_IsOwned_ReturnsTrueWhenOwned()
        {
            var logic = new BallOwnershipLogic();
            Assert.IsFalse(logic.IsOwned);

            logic.SetOwner(1);
            Assert.IsTrue(logic.IsOwned);

            logic.Release();
            Assert.IsFalse(logic.IsOwned);
        }

        [Test]
        public void BallOwnershipLogic_CanClaim_TrueWhenNoOwner()
        {
            var logic = new BallOwnershipLogic();
            float claimRadius = 1f;

            // Player is within radius
            bool canClaim = logic.CanClaim(0.5f, claimRadius);
            Assert.IsTrue(canClaim, "Should be able to claim when no owner and within radius");
        }

        [Test]
        public void BallOwnershipLogic_CanClaim_FalseWhenOwned()
        {
            var logic = new BallOwnershipLogic();
            logic.SetOwner(1);
            float claimRadius = 1f;

            bool canClaim = logic.CanClaim(0.5f, claimRadius);
            Assert.IsFalse(canClaim, "Should not be able to claim when ball is already owned");
        }

        [Test]
        public void BallOwnershipLogic_CanClaim_FalseWhenOutOfRadius()
        {
            var logic = new BallOwnershipLogic();
            float claimRadius = 1f;

            bool canClaim = logic.CanClaim(2f, claimRadius);
            Assert.IsFalse(canClaim, "Should not be able to claim when outside radius");
        }

        [Test]
        public void BallOwnershipLogic_Transfer_ChangesOwnerDirectly()
        {
            var logic = new BallOwnershipLogic();
            logic.SetOwner(1);

            int oldOwner = -99, newOwner = -99;
            logic.OnOwnerChanged += (o, n) => { oldOwner = o; newOwner = n; };

            logic.Transfer(3);
            Assert.AreEqual(1, oldOwner);
            Assert.AreEqual(3, newOwner);
            Assert.AreEqual(3, logic.CurrentOwnerId);
        }

        [Test]
        public void BallOwnershipLogic_SequentialSetOwner_TwoPlayersCannotOwnSimultaneously()
        {
            var logic = new BallOwnershipLogic();
            logic.SetOwner(1);
            logic.SetOwner(2);
            Assert.AreEqual(2, logic.CurrentOwnerId, "Only latest owner should be set");
        }
    }
}
