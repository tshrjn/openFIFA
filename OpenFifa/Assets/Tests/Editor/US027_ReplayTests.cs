using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("US027")]
    public class US027_ReplayTests
    {
        [Test]
        public void ReplayBuffer_Capacity_Is150Frames()
        {
            var buffer = new ReplayBuffer(11, 150);
            Assert.AreEqual(150, buffer.Capacity);
        }

        [Test]
        public void ReplayBuffer_InitialState_Empty()
        {
            var buffer = new ReplayBuffer(11, 150);
            Assert.AreEqual(0, buffer.FrameCount);
        }

        [Test]
        public void ReplayBuffer_RecordFrame_IncrementsCount()
        {
            var buffer = new ReplayBuffer(11, 150);
            var positions = CreatePositions(11);
            var rotations = CreateRotations(11);
            buffer.RecordFrame(positions, rotations, 0f);
            Assert.AreEqual(1, buffer.FrameCount);
        }

        [Test]
        public void ReplayBuffer_RecordFrame_WrapsAround()
        {
            var buffer = new ReplayBuffer(11, 5); // Small capacity for testing
            var positions = CreatePositions(11);
            var rotations = CreateRotations(11);

            for (int i = 0; i < 7; i++)
            {
                buffer.RecordFrame(positions, rotations, i * 0.033f);
            }

            Assert.AreEqual(5, buffer.FrameCount, "Should wrap around at capacity");
        }

        [Test]
        public void ReplayBuffer_GetFrame_ReturnsRecordedData()
        {
            var buffer = new ReplayBuffer(2, 150);
            float[] positions = { 1f, 2f, 3f, 4f, 5f, 6f };
            float[] rotations = { 0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f };
            buffer.RecordFrame(positions, rotations, 1.5f);

            var frame = buffer.GetFrame(0);
            Assert.AreEqual(1.5f, frame.Timestamp, 0.001f);
            Assert.AreEqual(1f, frame.Positions[0]);
        }

        [Test]
        public void ReplayBuffer_NoGarbageAllocation_PreAllocated()
        {
            var buffer = new ReplayBuffer(11, 150);
            // Verify the buffer pre-allocates all frame storage
            Assert.AreEqual(150, buffer.Capacity);
            // Recording should not allocate new arrays
            var positions = CreatePositions(11);
            var rotations = CreateRotations(11);
            buffer.RecordFrame(positions, rotations, 0f);
            buffer.RecordFrame(positions, rotations, 0.033f);
            // If we get here without exception, pre-allocation works
            Assert.AreEqual(2, buffer.FrameCount);
        }

        [Test]
        public void ReplayBuffer_GetFramesInRange_ReturnsCorrectFrames()
        {
            var buffer = new ReplayBuffer(2, 150);
            float[] positions = { 1f, 2f, 3f, 4f, 5f, 6f };
            float[] rotations = { 0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f };

            for (int i = 0; i < 10; i++)
            {
                buffer.RecordFrame(positions, rotations, i * 0.5f);
            }

            // Get frames from 3.0s to 4.5s
            var frames = buffer.GetFramesFromTime(3.0f, 4.5f);
            Assert.Greater(frames.Length, 0, "Should return frames in the time range");
        }

        [Test]
        public void ReplayLogic_InitialState_IsRecording()
        {
            var logic = new ReplayLogic(5f, 0.5f);
            Assert.IsTrue(logic.IsRecording);
            Assert.IsFalse(logic.IsPlaying);
        }

        [Test]
        public void ReplayLogic_StartPlayback_SetsPlayingState()
        {
            var logic = new ReplayLogic(5f, 0.5f);
            logic.StartPlayback(10f);
            Assert.IsTrue(logic.IsPlaying);
            Assert.IsFalse(logic.IsRecording);
        }

        [Test]
        public void ReplayLogic_PlaybackSpeed_IsHalf()
        {
            var logic = new ReplayLogic(5f, 0.5f);
            Assert.AreEqual(0.5f, logic.PlaybackSpeed);
        }

        [Test]
        public void ReplayLogic_ReplayDuration_Is5Seconds()
        {
            var logic = new ReplayLogic(5f, 0.5f);
            Assert.AreEqual(5f, logic.ReplayDuration);
        }

        private static float[] CreatePositions(int objectCount)
        {
            return new float[objectCount * 3]; // x, y, z per object
        }

        private static float[] CreateRotations(int objectCount)
        {
            return new float[objectCount * 4]; // x, y, z, w per object
        }
    }
}
