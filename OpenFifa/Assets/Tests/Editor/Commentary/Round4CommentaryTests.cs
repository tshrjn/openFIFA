using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    /// <summary>Fourth-round review: Push/Tick return engine-owned, reused lists; callers must not be able to keep or mutate them.</summary>
    [TestFixture]
    [Category("FID_COMMENTARY")]
    public class Round4CommentaryTests
    {
        private static CommentaryEngine NewEngine()
        {
            var bank = CommentaryLineBank.Load(File.ReadAllText(RosterTestData.LinesPath), out var errors);
            Assert.IsNotNull(bank, string.Join("\n", errors.ToArray()));
            return new CommentaryEngine(bank, new CommentaryConfig { AnalystChance = 0 }, 7u);
        }

        private static CommentaryEvent Ev(CommentaryEventType t, double time) =>
            new CommentaryEvent { Type = t, Time = time, Minute = 30, PlayerName = "Okafor", CrowdIntensity = 0.5f };

        [Test]
        public void PushAndTick_ReturnReadOnlyViews_NotMutableLists()
        {
            Assert.AreEqual(typeof(IReadOnlyList<CommentaryCue>), typeof(CommentaryEngine).GetMethod("Push").ReturnType);
            Assert.AreEqual(typeof(IReadOnlyList<CommentaryCue>), typeof(CommentaryEngine).GetMethod("Tick").ReturnType);
            var e = NewEngine();
            var pushed = e.Push(Ev(CommentaryEventType.ShotSaved, 0));
            Assert.IsNotInstanceOf<List<CommentaryCue>>(pushed, "a List can be cast back and mutated by the caller");
            Assert.IsNotInstanceOf<List<CommentaryCue>>(e.Tick(0.1));
            Assert.Throws<System.NotSupportedException>(() => ((ICollection<CommentaryCue>)pushed).Clear());
        }

        [Test]
        public void Push_ResultSurvivesTick_ButIsReusedByNextPush_SoCallersCopy()
        {
            var e = NewEngine();
            var pushed = e.Push(Ev(CommentaryEventType.ShotSaved, 0));
            Assert.Greater(pushed.Count, 0, "scenario: the save must start a line");
            var copy = new List<CommentaryCue>(pushed);
            e.Tick(0.05);
            CollectionAssert.AreEqual(copy, pushed, "a Tick must not touch the Push list");
            var again = e.Push(Ev(CommentaryEventType.Goal, 0.1));
            Assert.AreSame(pushed, again, "documented lifetime: the same view is reused by the next Push");
        }
    }
}
