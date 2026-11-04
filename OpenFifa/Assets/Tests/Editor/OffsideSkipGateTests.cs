using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    /// <summary>Only a new press skips the offside presentation: held buttons / keys / touches never do.</summary>
    [TestFixture]
    [Category("Offside")]
    public class OffsideSkipGateTests
    {
        private const ulong Sprint = 1UL << 9;   // right trigger, held while sprinting
        private const ulong Pass = 1UL << 0;     // button south
        private const ulong TouchBegan = 1UL << 19;

        [Test]
        public void Update_ButtonHeldFromStart_NeverSkips()
        {
            var gate = new OffsideSkipGate(0.25f);
            gate.Begin(Sprint);
            for (int f = 1; f <= 150; f++)
                Assert.IsFalse(gate.Update(Sprint, f / 60f), $"held sprint skipped at frame {f}");
        }

        [Test]
        public void Update_HeldThenReleasedThenPressed_Skips()
        {
            var gate = new OffsideSkipGate(0.25f);
            gate.Begin(Sprint);
            Assert.IsFalse(gate.Update(Sprint, 0.5f));
            Assert.IsFalse(gate.Update(0, 0.52f), "a release is not a press");
            Assert.IsTrue(gate.Update(Sprint, 0.54f), "a fresh press after the release skips");
        }

        [Test]
        public void Update_NewPressWhileAnotherHeld_Skips()
        {
            var gate = new OffsideSkipGate(0.25f);
            gate.Begin(Sprint);
            Assert.IsTrue(gate.Update(Sprint | Pass, 0.6f));
        }

        [Test]
        public void Update_PressDuringGrace_SwallowedUntilPressedAgain()
        {
            var gate = new OffsideSkipGate(0.25f);
            gate.Begin(0);
            Assert.IsFalse(gate.Update(Pass, 0.1f), "inside the grace time");
            Assert.IsFalse(gate.Update(Pass, 0.4f), "still the same held press");
            Assert.IsFalse(gate.Update(0, 0.45f));
            Assert.IsTrue(gate.Update(Pass, 0.5f));
        }

        [Test]
        public void Update_NothingPressed_NeverSkips()
        {
            var gate = new OffsideSkipGate(0.25f);
            gate.Begin(0);
            for (int f = 1; f <= 150; f++) Assert.IsFalse(gate.Update(0, f / 60f));
        }

        [Test]
        public void Update_TouchBeganPulse_SkipsOnceAfterGrace()
        {
            var gate = new OffsideSkipGate(0.25f);
            gate.Begin(0);
            Assert.IsTrue(gate.Update(TouchBegan, 0.3f));
            Assert.IsFalse(gate.Update(0, 0.32f), "a finger kept down (no new Began) does not skip again");
        }

        [Test]
        public void Begin_ResetsHeldStateBetweenSequences()
        {
            var gate = new OffsideSkipGate(0.25f);
            gate.Begin(0);
            Assert.IsTrue(gate.Update(Pass, 0.5f));
            gate.Begin(Pass); // next sequence starts with the button still held
            Assert.IsFalse(gate.Update(Pass, 0.5f));
        }
    }
}
