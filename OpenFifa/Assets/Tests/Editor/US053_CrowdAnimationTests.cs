using System.Collections.Generic;
using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("US053")]
    public class US053_CrowdAnimationTests
    {
        // ============================================================
        // CrowdAnimState Enum
        // ============================================================

        [Test]
        public void CrowdAnimState_HasAllExpectedValues()
        {
            Assert.AreEqual(0, (int)CrowdAnimState.Seated);
            Assert.AreEqual(1, (int)CrowdAnimState.Idle);
            Assert.AreEqual(2, (int)CrowdAnimState.Cheering);
            Assert.AreEqual(3, (int)CrowdAnimState.Booing);
            Assert.AreEqual(4, (int)CrowdAnimState.Standing);
            Assert.AreEqual(5, (int)CrowdAnimState.WaveLeft);
            Assert.AreEqual(6, (int)CrowdAnimState.WaveRight);
            Assert.AreEqual(7, (int)CrowdAnimState.Celebrating);
            Assert.AreEqual(8, (int)CrowdAnimState.Dejected);
        }

        [Test]
        public void CrowdAnimState_EnumCount_IsNine()
        {
            var values = System.Enum.GetValues(typeof(CrowdAnimState));
            Assert.AreEqual(9, values.Length,
                "CrowdAnimState should have exactly 9 values");
        }

        // ============================================================
        // CrowdAnimationConfig Defaults and Ranges
        // ============================================================

        [Test]
        public void Config_DefaultSectionCount_IsEight()
        {
            var config = new CrowdAnimationConfig();
            Assert.AreEqual(8, config.SectionCount);
        }

        [Test]
        public void Config_CheeringDuration_InRange3To5()
        {
            var config = new CrowdAnimationConfig();
            Assert.AreEqual(3f, config.CheeringDurationMin, 0.01f);
            Assert.AreEqual(5f, config.CheeringDurationMax, 0.01f);
        }

        [Test]
        public void Config_CelebratingDuration_InRange8To10()
        {
            var config = new CrowdAnimationConfig();
            Assert.AreEqual(8f, config.CelebratingDurationMin, 0.01f);
            Assert.AreEqual(10f, config.CelebratingDurationMax, 0.01f);
        }

        [Test]
        public void Config_WaveDurationPerSection_IsTwo()
        {
            var config = new CrowdAnimationConfig();
            Assert.AreEqual(2f, config.WaveDurationPerSection, 0.01f);
        }

        [Test]
        public void Config_TransitionBlendTime_IsPositive()
        {
            var config = new CrowdAnimationConfig();
            Assert.Greater(config.TransitionBlendTime, 0f,
                "TransitionBlendTime should be positive for smooth state transitions");
        }

        [Test]
        public void Config_GetStateDuration_Cheering_ReturnsMidpoint()
        {
            var config = new CrowdAnimationConfig();
            float expected = (3f + 5f) / 2f;
            Assert.AreEqual(expected, config.GetStateDuration(CrowdAnimState.Cheering), 0.01f);
        }

        [Test]
        public void Config_GetStateDuration_IdleAndSeated_ReturnZero()
        {
            var config = new CrowdAnimationConfig();
            Assert.AreEqual(0f, config.GetStateDuration(CrowdAnimState.Idle),
                "Idle has no timeout");
            Assert.AreEqual(0f, config.GetStateDuration(CrowdAnimState.Seated),
                "Seated has no timeout");
        }

        [Test]
        public void Config_MexicanWaveMinSections_MeetsParticipation()
        {
            var config = new CrowdAnimationConfig(sectionCount: 8, mexicanWaveMinParticipation: 0.75f);
            Assert.AreEqual(6, config.MexicanWaveMinSections,
                "75% of 8 sections = 6 sections minimum");
        }

        [Test]
        public void Config_InvalidSectionCount_Throws()
        {
            Assert.Throws<System.ArgumentException>(() => new CrowdAnimationConfig(sectionCount: 0));
        }

        // ============================================================
        // CrowdSectionBehavior State Tracking
        // ============================================================

        [Test]
        public void SectionBehavior_InitialState_IsIdle()
        {
            var section = new CrowdSectionBehavior(0, SectionAffiliation.Home);
            Assert.AreEqual(CrowdAnimState.Idle, section.CurrentState);
        }

        [Test]
        public void SectionBehavior_TeamAffiliation_IsPreserved()
        {
            var home = new CrowdSectionBehavior(0, SectionAffiliation.Home);
            var away = new CrowdSectionBehavior(1, SectionAffiliation.Away);
            var neutral = new CrowdSectionBehavior(2, SectionAffiliation.Neutral);

            Assert.AreEqual(SectionAffiliation.Home, home.Affiliation);
            Assert.AreEqual(SectionAffiliation.Away, away.Affiliation);
            Assert.AreEqual(SectionAffiliation.Neutral, neutral.Affiliation);
        }

        [Test]
        public void SectionBehavior_SetState_UpdatesCurrentAndPrevious()
        {
            var section = new CrowdSectionBehavior(0, SectionAffiliation.Home);
            section.SetState(CrowdAnimState.Cheering, 3f, 0.8f);

            Assert.AreEqual(CrowdAnimState.Cheering, section.CurrentState);
            Assert.AreEqual(CrowdAnimState.Idle, section.PreviousState);
            Assert.AreEqual(0.8f, section.Intensity, 0.01f);
        }

        [Test]
        public void SectionBehavior_SetState_ResetsElapsed()
        {
            var section = new CrowdSectionBehavior(0, SectionAffiliation.Home);
            section.SetState(CrowdAnimState.Cheering, 3f, 0.8f);
            section.Update(1f, 0.3f);

            Assert.Greater(section.StateElapsed, 0f);

            section.SetState(CrowdAnimState.Standing, 2f, 0.5f);
            Assert.AreEqual(0f, section.StateElapsed, 0.01f,
                "StateElapsed should reset on SetState");
        }

        [Test]
        public void SectionBehavior_IntensityClamped_Between0And1()
        {
            var section = new CrowdSectionBehavior(0, SectionAffiliation.Home);
            section.SetState(CrowdAnimState.Cheering, 3f, 2f); // exceeds 1
            Assert.LessOrEqual(section.Intensity, 1f);

            section.SetState(CrowdAnimState.Cheering, 3f, -0.5f); // below 0
            Assert.GreaterOrEqual(section.Intensity, 0f);
        }

        // ============================================================
        // CrowdDirector Event Responses
        // ============================================================

        [Test]
        public void Director_GoalEvent_HomeSections_Celebrate()
        {
            var config = new CrowdAnimationConfig(sectionCount: 4);
            var affiliations = new List<SectionAffiliation>
            {
                SectionAffiliation.Home,
                SectionAffiliation.Away,
                SectionAffiliation.Home,
                SectionAffiliation.Neutral
            };
            var director = new CrowdDirector(config, affiliations);

            director.HandleEvent(CrowdGameEvent.Goal, TeamIdentifier.TeamA);
            // Process queued states
            director.Update(1f);

            // Home sections should be Celebrating (TeamA = home)
            Assert.AreEqual(CrowdAnimState.Celebrating, director.Sections[0].CurrentState,
                "Home section should celebrate when home team scores");
            Assert.AreEqual(CrowdAnimState.Celebrating, director.Sections[2].CurrentState,
                "Home section should celebrate when home team scores");
        }

        [Test]
        public void Director_GoalEvent_AwaySections_Dejected()
        {
            var config = new CrowdAnimationConfig(sectionCount: 4);
            var affiliations = new List<SectionAffiliation>
            {
                SectionAffiliation.Home,
                SectionAffiliation.Away,
                SectionAffiliation.Home,
                SectionAffiliation.Neutral
            };
            var director = new CrowdDirector(config, affiliations);

            director.HandleEvent(CrowdGameEvent.Goal, TeamIdentifier.TeamA);
            director.Update(1f);

            // Away sections should be Dejected when home team scores
            Assert.AreEqual(CrowdAnimState.Dejected, director.Sections[1].CurrentState,
                "Away section should be dejected when home team scores");
        }

        [Test]
        public void Director_FoulEvent_AllSections_Boo()
        {
            var config = new CrowdAnimationConfig(sectionCount: 4);
            var affiliations = new List<SectionAffiliation>
            {
                SectionAffiliation.Home,
                SectionAffiliation.Away,
                SectionAffiliation.Neutral,
                SectionAffiliation.Neutral
            };
            var director = new CrowdDirector(config, affiliations);

            director.HandleEvent(CrowdGameEvent.Foul);
            director.Update(1f);

            for (int i = 0; i < director.Sections.Count; i++)
            {
                Assert.AreEqual(CrowdAnimState.Booing, director.Sections[i].CurrentState,
                    $"Section {i} should be booing after a foul");
            }
        }

        [Test]
        public void Director_KickoffEvent_AllSections_GoToIdle()
        {
            var config = new CrowdAnimationConfig(sectionCount: 4);
            var director = new CrowdDirector(config);

            // First put sections in non-idle state
            director.HandleEvent(CrowdGameEvent.Foul);
            director.Update(1f);

            // Kickoff should reset to Idle
            director.HandleEvent(CrowdGameEvent.Kickoff);

            for (int i = 0; i < director.Sections.Count; i++)
            {
                Assert.AreEqual(CrowdAnimState.Idle, director.Sections[i].CurrentState,
                    $"Section {i} should be Idle after kickoff");
            }
        }

        [Test]
        public void Director_NearMissEvent_AllSections_Stand()
        {
            var config = new CrowdAnimationConfig(sectionCount: 4);
            var director = new CrowdDirector(config);

            director.HandleEvent(CrowdGameEvent.NearMiss, eventPositionX: 24f, eventPositionZ: 0f);
            director.Update(1f);

            for (int i = 0; i < director.Sections.Count; i++)
            {
                Assert.AreEqual(CrowdAnimState.Standing, director.Sections[i].CurrentState,
                    $"Section {i} should be Standing after near miss");
            }
        }

        // ============================================================
        // WaveOrchestrator
        // ============================================================

        [Test]
        public void WaveOrchestrator_StartWave_SetsActive()
        {
            var config = new CrowdAnimationConfig(sectionCount: 8);
            var sections = CreateSections(8);
            var wave = new WaveOrchestrator(config, sections);

            wave.StartWave(true);

            Assert.IsTrue(wave.IsWaveActive, "Wave should be active after StartWave");
        }

        [Test]
        public void WaveOrchestrator_WaveOrder_HasCorrectCount()
        {
            var config = new CrowdAnimationConfig(sectionCount: 8);
            var sections = CreateSections(8);
            var wave = new WaveOrchestrator(config, sections);

            wave.StartWave(true);

            Assert.AreEqual(8, wave.WaveOrder.Count,
                "Wave order should include all 8 sections");
        }

        [Test]
        public void WaveOrchestrator_TimingOffsets_IncreasePerSection()
        {
            var config = new CrowdAnimationConfig(sectionCount: 8, mexicanWaveDelay: 0.5f);
            var sections = CreateSections(8);
            var wave = new WaveOrchestrator(config, sections);

            wave.StartWave(true);

            // Verify timing offsets increase
            float lastOffset = -1f;
            for (int i = 0; i < wave.WaveOrder.Count; i++)
            {
                float offset = wave.GetSectionTimingOffset(wave.WaveOrder[i]);
                Assert.Greater(offset, lastOffset,
                    $"Section {wave.WaveOrder[i]} offset ({offset}) should be > previous ({lastOffset})");
                lastOffset = offset;
            }
        }

        [Test]
        public void WaveOrchestrator_CounterClockwise_ReversesOrder()
        {
            var config = new CrowdAnimationConfig(sectionCount: 8);
            var sectionsA = CreateSections(8);
            var sectionsB = CreateSections(8);
            var waveCW = new WaveOrchestrator(config, sectionsA);
            var waveCCW = new WaveOrchestrator(config, sectionsB);

            waveCW.StartWave(true);
            waveCCW.StartWave(false);

            // First section in CW should be last in CCW (or vice versa)
            Assert.AreEqual(waveCW.WaveOrder[0], waveCCW.WaveOrder[waveCCW.WaveOrder.Count - 1],
                "Counter-clockwise wave should reverse the section order");
        }

        [Test]
        public void WaveOrchestrator_Update_TriggersFirstSection_Immediately()
        {
            var config = new CrowdAnimationConfig(sectionCount: 8, mexicanWaveDelay: 0.5f);
            var sections = CreateSections(8);
            var wave = new WaveOrchestrator(config, sections);

            wave.StartWave(true);
            wave.Update(0.01f); // Small time step

            // First section should have been triggered (index 0 triggers at time 0)
            int firstSection = wave.WaveOrder[0];
            var state = sections[firstSection].CurrentState;
            Assert.That(state == CrowdAnimState.WaveRight || state == CrowdAnimState.WaveLeft,
                $"First section should be in wave state, got {state}");
        }

        [Test]
        public void WaveOrchestrator_CompleteWave_BecomesInactive()
        {
            var config = new CrowdAnimationConfig(sectionCount: 8, mexicanWaveDelay: 0.5f, mexicanWaveUpDuration: 0.8f);
            var sections = CreateSections(8);
            var wave = new WaveOrchestrator(config, sections);

            wave.StartWave(true);

            // Advance past total duration
            float totalDuration = config.MexicanWaveTotalDuration;
            wave.Update(totalDuration + 0.1f);

            Assert.IsFalse(wave.IsWaveActive,
                "Wave should become inactive after total duration elapses");
        }

        // ============================================================
        // ProximityReactor
        // ============================================================

        [Test]
        public void ProximityReactor_CloserSection_HigherIntensity()
        {
            var config = new CrowdAnimationConfig();
            var reactor = new ProximityReactor(config);

            // Event at east goal (positive X): East section (2) is closest
            float eastIntensity = reactor.CalculateIntensity(2, 8, 25f, 0f);
            // North section (0) is further
            float northIntensity = reactor.CalculateIntensity(0, 8, 25f, 0f);

            Assert.Greater(eastIntensity, northIntensity,
                $"East section ({eastIntensity:F3}) should have higher intensity " +
                $"than North section ({northIntensity:F3}) for event at east goal");
        }

        [Test]
        public void ProximityReactor_Intensity_WithinConfigRange()
        {
            var config = new CrowdAnimationConfig(minIntensity: 0.2f, maxIntensity: 1f);
            var reactor = new ProximityReactor(config);

            for (int i = 0; i < 8; i++)
            {
                float intensity = reactor.CalculateIntensity(i, 8, 0f, 0f);
                Assert.GreaterOrEqual(intensity, config.MinIntensity,
                    $"Section {i} intensity should be >= MinIntensity");
                Assert.LessOrEqual(intensity, config.MaxIntensity,
                    $"Section {i} intensity should be <= MaxIntensity");
            }
        }

        [Test]
        public void ProximityReactor_Delay_IncreasesWithDistance()
        {
            var config = new CrowdAnimationConfig();
            var reactor = new ProximityReactor(config);

            // Event at east goal: east section should have smaller delay than west
            float eastDelay = reactor.CalculateDelay(2, 8, 25f, 0f);
            float westDelay = reactor.CalculateDelay(3, 8, 25f, 0f);

            Assert.Less(eastDelay, westDelay,
                $"East section delay ({eastDelay:F3}s) should be less than " +
                $"West section delay ({westDelay:F3}s) for event at east goal");
        }

        // ============================================================
        // HomeFanBias
        // ============================================================

        [Test]
        public void HomeFanBias_HomeGoal_HomeSectionMoreIntense()
        {
            var config = new CrowdAnimationConfig(sectionCount: 4);
            var affiliations = new List<SectionAffiliation>
            {
                SectionAffiliation.Home,
                SectionAffiliation.Away,
                SectionAffiliation.Home,
                SectionAffiliation.Neutral
            };
            var director = new CrowdDirector(config, affiliations);

            director.HandleEvent(CrowdGameEvent.Goal, TeamIdentifier.TeamA);
            director.Update(1f);

            float homeIntensity = director.Sections[0].Intensity;
            float neutralIntensity = director.Sections[3].Intensity;

            Assert.GreaterOrEqual(homeIntensity, neutralIntensity,
                $"Home section intensity ({homeIntensity:F3}) should be >= " +
                $"neutral section intensity ({neutralIntensity:F3}) on home goal");
        }

        // ============================================================
        // State Timeout Back to Idle
        // ============================================================

        [Test]
        public void SectionBehavior_StateTimeout_RevertsToIdle()
        {
            var section = new CrowdSectionBehavior(0, SectionAffiliation.Home);
            section.SetState(CrowdAnimState.Cheering, 2f, 0.8f); // 2 second duration

            // Advance past duration
            section.Update(2.1f, 0.3f);

            Assert.AreEqual(CrowdAnimState.Idle, section.CurrentState,
                "Section should revert to Idle after state duration expires");
        }

        [Test]
        public void SectionBehavior_IdleState_DoesNotTimeout()
        {
            var section = new CrowdSectionBehavior(0, SectionAffiliation.Home);
            // Idle has duration 0 (no timeout)
            section.SetState(CrowdAnimState.Idle, 0f, 0.3f);

            // Advance a long time
            section.Update(100f, 0.3f);

            Assert.AreEqual(CrowdAnimState.Idle, section.CurrentState,
                "Idle state should persist indefinitely");
        }

        // ============================================================
        // Section Reaction Delay
        // ============================================================

        [Test]
        public void SectionBehavior_QueuedState_AppliesAfterDelay()
        {
            var section = new CrowdSectionBehavior(0, SectionAffiliation.Home);
            section.QueueState(CrowdAnimState.Cheering, 0.5f, 3f, 0.8f);

            Assert.IsTrue(section.HasQueuedState, "Should have queued state");
            Assert.AreEqual(CrowdAnimState.Idle, section.CurrentState,
                "Should still be Idle before delay expires");

            // Advance past delay
            section.Update(0.6f, 0.3f);

            Assert.AreEqual(CrowdAnimState.Cheering, section.CurrentState,
                "Should transition to queued state after delay");
            Assert.IsFalse(section.HasQueuedState, "Queue should be cleared");
        }

        [Test]
        public void SectionBehavior_QueuedState_NotApplied_BeforeDelay()
        {
            var section = new CrowdSectionBehavior(0, SectionAffiliation.Home);
            section.QueueState(CrowdAnimState.Booing, 1.0f, 4f, 0.7f);

            section.Update(0.3f, 0.3f);

            Assert.AreEqual(CrowdAnimState.Idle, section.CurrentState,
                "Should not transition before delay expires");
            Assert.IsTrue(section.HasQueuedState, "Queue should still be pending");
        }

        // ============================================================
        // Blend Progress
        // ============================================================

        [Test]
        public void SectionBehavior_BlendProgress_StartsAtZero_OnStateChange()
        {
            var section = new CrowdSectionBehavior(0, SectionAffiliation.Home);
            section.SetState(CrowdAnimState.Cheering, 3f, 0.8f);

            Assert.AreEqual(0f, section.BlendProgress, 0.01f,
                "BlendProgress should start at 0 on state change");
        }

        [Test]
        public void SectionBehavior_BlendProgress_ReachesOne_OverTime()
        {
            var section = new CrowdSectionBehavior(0, SectionAffiliation.Home);
            float blendTime = 0.3f;
            section.SetState(CrowdAnimState.Cheering, 3f, 0.8f);

            // Advance past blend time
            section.Update(0.5f, blendTime);

            Assert.AreEqual(1f, section.BlendProgress, 0.01f,
                "BlendProgress should reach 1.0 after blend time");
        }

        // ============================================================
        // Helper methods
        // ============================================================

        private static List<CrowdSectionBehavior> CreateSections(int count)
        {
            var sections = new List<CrowdSectionBehavior>();
            for (int i = 0; i < count; i++)
            {
                sections.Add(new CrowdSectionBehavior(i, SectionAffiliation.Neutral));
            }
            return sections;
        }
    }
}
