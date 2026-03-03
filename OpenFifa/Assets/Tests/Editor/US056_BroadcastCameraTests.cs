using System;
using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("US056")]
    public class US056_BroadcastCameraTests
    {
        // =====================================================================
        // CameraPreset and CameraAngle Tests
        // =====================================================================

        [Test]
        public void CameraPreset_DefaultWide_HasReasonableFOV()
        {
            var preset = CameraPresetFactory.GetDefaultPreset(CameraAngle.Wide);
            Assert.That(preset.FieldOfView, Is.InRange(50f, 80f),
                $"Wide camera FOV should be in [50, 80] but was {preset.FieldOfView}");
        }

        [Test]
        public void CameraPreset_CloseAngle_HasNarrowerFOVThanWide()
        {
            var wide = CameraPresetFactory.GetDefaultPreset(CameraAngle.Wide);
            var close = CameraPresetFactory.GetDefaultPreset(CameraAngle.Close);
            Assert.Less(close.FieldOfView, wide.FieldOfView,
                $"Close FOV ({close.FieldOfView}) should be less than Wide FOV ({wide.FieldOfView})");
        }

        [Test]
        public void CameraPreset_AllAngles_HavePositiveHeightOffset()
        {
            foreach (CameraAngle angle in Enum.GetValues(typeof(CameraAngle)))
            {
                var preset = CameraPresetFactory.GetDefaultPreset(angle);
                Assert.Greater(preset.PositionY, 0f,
                    $"{angle} camera Y position ({preset.PositionY}) should be above ground");
            }
        }

        [Test]
        public void CameraPreset_TacticalAngle_HasHighElevation()
        {
            var tactical = CameraPresetFactory.GetDefaultPreset(CameraAngle.Tactical);
            Assert.That(tactical.PositionY, Is.GreaterThanOrEqualTo(30f),
                $"Tactical camera should be high up, was at Y={tactical.PositionY}");
            Assert.That(tactical.RotationX, Is.GreaterThanOrEqualTo(80f),
                $"Tactical camera should look steeply down, rotX was {tactical.RotationX}");
        }

        [Test]
        public void CameraPreset_CelebrationAngle_HasLowPosition()
        {
            var celebration = CameraPresetFactory.GetDefaultPreset(CameraAngle.Celebration);
            var wide = CameraPresetFactory.GetDefaultPreset(CameraAngle.Wide);
            Assert.Less(celebration.PositionY, wide.PositionY,
                $"Celebration camera Y ({celebration.PositionY}) should be lower than Wide ({wide.PositionY})");
        }

        [Test]
        public void CameraPreset_CustomValues_AreRetained()
        {
            var preset = new CameraPreset(
                angle: CameraAngle.Medium,
                positionX: 1f, positionY: 2f, positionZ: 3f,
                rotationX: 10f, rotationY: 20f, rotationZ: 30f,
                fieldOfView: 42f, dollySpeed: 7f, trackSpeed: 4f);

            Assert.AreEqual(CameraAngle.Medium, preset.Angle);
            Assert.AreEqual(1f, preset.PositionX, 0.01f);
            Assert.AreEqual(2f, preset.PositionY, 0.01f);
            Assert.AreEqual(3f, preset.PositionZ, 0.01f);
            Assert.AreEqual(42f, preset.FieldOfView, 0.01f);
            Assert.AreEqual(7f, preset.DollySpeed, 0.01f);
        }

        // =====================================================================
        // AutoCutConfig Tests
        // =====================================================================

        [Test]
        public void AutoCutConfig_DefaultMinCutDuration_IsPositive()
        {
            var config = new AutoCutConfig();
            Assert.Greater(config.MinCutDuration, 0f,
                $"Min cut duration should be > 0 but was {config.MinCutDuration}");
        }

        [Test]
        public void AutoCutConfig_MaxCutDuration_IsGreaterThanMin()
        {
            var config = new AutoCutConfig();
            Assert.GreaterOrEqual(config.MaxCutDuration, config.MinCutDuration,
                $"Max cut ({config.MaxCutDuration}) should be >= min cut ({config.MinCutDuration})");
        }

        [Test]
        public void AutoCutConfig_InvalidMinDuration_ThrowsException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new AutoCutConfig(minCutDuration: -1f));
        }

        [Test]
        public void AutoCutConfig_MaxLessThanMin_ThrowsException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new AutoCutConfig(minCutDuration: 5f, maxCutDuration: 2f));
        }

        // =====================================================================
        // AngleSelector Tests
        // =====================================================================

        [Test]
        public void AngleSelector_BallAtCenter_ReturnsMidfield()
        {
            var selector = new AngleSelector(50f, 30f, 4f);
            var zone = selector.GetPitchZone(0f, 0f);
            Assert.AreEqual(PitchZone.Midfield, zone,
                $"Ball at center (0,0) should be Midfield but was {zone}");
        }

        [Test]
        public void AngleSelector_BallNearGoalA_ReturnsPenaltyAreaA()
        {
            var selector = new AngleSelector(50f, 30f, 4f);
            // Penalty area A is within 4m of the -25m end
            var zone = selector.GetPitchZone(-23f, 0f);
            Assert.AreEqual(PitchZone.PenaltyAreaA, zone,
                $"Ball at x=-23 should be PenaltyAreaA but was {zone}");
        }

        [Test]
        public void AngleSelector_BallNearGoalB_ReturnsPenaltyAreaB()
        {
            var selector = new AngleSelector(50f, 30f, 4f);
            var zone = selector.GetPitchZone(23f, 0f);
            Assert.AreEqual(PitchZone.PenaltyAreaB, zone,
                $"Ball at x=23 should be PenaltyAreaB but was {zone}");
        }

        [Test]
        public void AngleSelector_BallInAttackingThird_ReturnsAttackingThird()
        {
            var selector = new AngleSelector(50f, 30f, 4f);
            // Attacking third threshold = 25 * 0.6 = 15m from center
            var zone = selector.GetPitchZone(18f, 0f);
            Assert.AreEqual(PitchZone.AttackingThirdB, zone,
                $"Ball at x=18 should be AttackingThirdB but was {zone}");
        }

        [Test]
        public void AngleSelector_Midfield_SelectsWideAngle()
        {
            var selector = new AngleSelector();
            var angle = selector.SelectAngle(PitchZone.Midfield);
            Assert.AreEqual(CameraAngle.Wide, angle,
                $"Midfield zone should select Wide angle but selected {angle}");
        }

        [Test]
        public void AngleSelector_PenaltyArea_SelectsCloseAngle()
        {
            var selector = new AngleSelector();
            var angle = selector.SelectAngle(PitchZone.PenaltyAreaA);
            Assert.AreEqual(CameraAngle.Close, angle,
                $"Penalty area should select Close angle but selected {angle}");
        }

        [Test]
        public void AngleSelector_AttackingThird_SelectsMediumAngle()
        {
            var selector = new AngleSelector();
            var angle = selector.SelectAngle(PitchZone.AttackingThirdA);
            Assert.AreEqual(CameraAngle.Medium, angle,
                $"Attacking third should select Medium angle but selected {angle}");
        }

        // =====================================================================
        // MomentumTracker Tests
        // =====================================================================

        [Test]
        public void MomentumTracker_InitialMomentum_IsZero()
        {
            var config = new DirectorConfig();
            var tracker = new MomentumTracker(config);
            Assert.AreEqual(0f, tracker.CurrentMomentum, 0.001f,
                "Initial momentum should be 0");
        }

        [Test]
        public void MomentumTracker_GoalEvent_IncreaseMomentum()
        {
            var config = new DirectorConfig(goalMomentumBoost: 1.0f);
            var tracker = new MomentumTracker(config);
            tracker.ApplyEvent(BroadcastGameEvent.Goal);
            Assert.AreEqual(1.0f, tracker.CurrentMomentum, 0.001f,
                $"Goal should set momentum to 1.0, was {tracker.CurrentMomentum}");
        }

        [Test]
        public void MomentumTracker_MomentumDecays_OverTime()
        {
            var config = new DirectorConfig(momentumDecayRate: 0.15f);
            var tracker = new MomentumTracker(config);
            tracker.ApplyEvent(BroadcastGameEvent.Goal);
            float initial = tracker.CurrentMomentum;

            tracker.Update(2f); // 2 seconds of decay

            Assert.Less(tracker.CurrentMomentum, initial,
                $"Momentum should decay over time. Initial={initial}, Current={tracker.CurrentMomentum}");
        }

        [Test]
        public void MomentumTracker_MomentumClamped_AtOne()
        {
            var config = new DirectorConfig(goalMomentumBoost: 1.0f);
            var tracker = new MomentumTracker(config);
            tracker.ApplyEvent(BroadcastGameEvent.Goal);
            tracker.ApplyEvent(BroadcastGameEvent.Goal);
            Assert.AreEqual(1.0f, tracker.CurrentMomentum, 0.001f,
                "Momentum should clamp at 1.0");
        }

        [Test]
        public void MomentumTracker_MomentumClamped_AtZero()
        {
            var config = new DirectorConfig(momentumDecayRate: 0.15f);
            var tracker = new MomentumTracker(config);
            tracker.Update(100f); // Large time step
            Assert.AreEqual(0f, tracker.CurrentMomentum, 0.001f,
                "Momentum should not go below 0");
        }

        [Test]
        public void MomentumTracker_HighIntensity_WhenAboveThreshold()
        {
            var config = new DirectorConfig(momentumThreshold: 0.5f, goalMomentumBoost: 1.0f);
            var tracker = new MomentumTracker(config);
            tracker.ApplyEvent(BroadcastGameEvent.Goal);
            Assert.IsTrue(tracker.IsHighIntensity,
                $"Should be high intensity at momentum {tracker.CurrentMomentum} with threshold 0.5");
        }

        [Test]
        public void MomentumTracker_GetCutInterval_DecreasesWithMomentum()
        {
            var config = new DirectorConfig(baseCutFrequency: 5f, maxCutFrequency: 12f, goalMomentumBoost: 1.0f);
            var tracker = new MomentumTracker(config);

            float calmInterval = tracker.GetCurrentCutInterval();
            tracker.ApplyEvent(BroadcastGameEvent.Goal);
            float intenseInterval = tracker.GetCurrentCutInterval();

            Assert.Less(intenseInterval, calmInterval,
                $"Intense interval ({intenseInterval}s) should be shorter than calm ({calmInterval}s)");
        }

        // =====================================================================
        // ReplaySequencer Tests
        // =====================================================================

        [Test]
        public void ReplaySequencer_InitialState_IsNotActive()
        {
            var config = new ReplayCameraConfig();
            var sequencer = new ReplaySequencer(config);
            Assert.IsFalse(sequencer.IsActive, "Sequencer should start inactive");
        }

        [Test]
        public void ReplaySequencer_Start_BecomesActive()
        {
            var config = new ReplayCameraConfig();
            var sequencer = new ReplaySequencer(config);
            sequencer.Start();
            Assert.IsTrue(sequencer.IsActive, "Sequencer should be active after Start");
        }

        [Test]
        public void ReplaySequencer_StepCount_MatchesMultiAngleConfig()
        {
            var config = new ReplayCameraConfig(multiAngleCount: 3);
            var sequencer = new ReplaySequencer(config);
            Assert.AreEqual(3, sequencer.StepCount,
                $"Step count should be 3 but was {sequencer.StepCount}");
        }

        [Test]
        public void ReplaySequencer_Start_BeginsAtStepZero()
        {
            var config = new ReplayCameraConfig(multiAngleCount: 3);
            var sequencer = new ReplaySequencer(config);
            sequencer.Start();
            Assert.AreEqual(0, sequencer.CurrentStepIndex);
        }

        [Test]
        public void ReplaySequencer_FirstStep_UsesSlowMoSpeedFirst()
        {
            var config = new ReplayCameraConfig(slowMoSpeedFirst: 0.25f, multiAngleCount: 3, replayDuration: 6f);
            var sequencer = new ReplaySequencer(config);
            sequencer.Start();
            Assert.AreEqual(0.25f, sequencer.CurrentPlaybackSpeed, 0.001f,
                $"First step should use slowMoSpeedFirst (0.25), was {sequencer.CurrentPlaybackSpeed}");
        }

        [Test]
        public void ReplaySequencer_AdvancesStep_AfterStepDuration()
        {
            var config = new ReplayCameraConfig(multiAngleCount: 3, replayDuration: 6f);
            var sequencer = new ReplaySequencer(config);
            sequencer.Start();

            // Each step is 6/3 = 2 seconds
            sequencer.Update(2.1f);
            Assert.AreEqual(1, sequencer.CurrentStepIndex,
                $"Should advance to step 1 after 2.1s, was at {sequencer.CurrentStepIndex}");
        }

        [Test]
        public void ReplaySequencer_CompletesSequence_AfterTotalDuration()
        {
            var config = new ReplayCameraConfig(multiAngleCount: 3, replayDuration: 6f);
            var sequencer = new ReplaySequencer(config);
            bool completed = false;
            sequencer.OnReplayComplete += () => completed = true;

            sequencer.Start();
            // Advance through all 3 steps (2s each)
            sequencer.Update(2.1f);
            sequencer.Update(2.1f);
            sequencer.Update(2.1f);

            Assert.IsTrue(completed, "Replay should fire OnReplayComplete after all steps");
            Assert.IsFalse(sequencer.IsActive, "Sequencer should be inactive after completion");
        }

        [Test]
        public void ReplaySequencer_Stop_DeactivatesImmediately()
        {
            var config = new ReplayCameraConfig();
            var sequencer = new ReplaySequencer(config);
            sequencer.Start();
            sequencer.Stop();
            Assert.IsFalse(sequencer.IsActive, "Sequencer should be inactive after Stop");
        }

        [Test]
        public void ReplaySequencer_StateTransition_LiveToReplayToLive()
        {
            var config = new ReplayCameraConfig(multiAngleCount: 2, replayDuration: 4f);
            var sequencer = new ReplaySequencer(config);

            Assert.IsFalse(sequencer.IsActive, "Should start as Live (inactive)");

            sequencer.Start();
            Assert.IsTrue(sequencer.IsActive, "Should be in Replay (active)");

            // Complete the sequence
            sequencer.Update(2.1f);
            sequencer.Update(2.1f);

            Assert.IsFalse(sequencer.IsActive, "Should return to Live (inactive) after replay");
        }

        // =====================================================================
        // AutoCutLogic Tests
        // =====================================================================

        [Test]
        public void AutoCutLogic_NoCut_BeforeMinDuration()
        {
            var cutConfig = new AutoCutConfig(minCutDuration: 3f, maxCutDuration: 12f);
            var dirConfig = new DirectorConfig();
            var momentum = new MomentumTracker(dirConfig);
            var autoCut = new AutoCutLogic(cutConfig, momentum);

            bool cut = autoCut.Update(1f, CameraAngle.Medium); // 1 second, different angle
            Assert.IsFalse(cut, "Should not cut before min duration (3s), only 1s elapsed");
        }

        [Test]
        public void AutoCutLogic_ForceCut_AtMaxDuration()
        {
            var cutConfig = new AutoCutConfig(minCutDuration: 3f, maxCutDuration: 12f);
            var dirConfig = new DirectorConfig();
            var momentum = new MomentumTracker(dirConfig);
            var autoCut = new AutoCutLogic(cutConfig, momentum);

            // Advance to max duration with same angle
            bool cut = autoCut.Update(12.5f, CameraAngle.Wide);
            Assert.IsTrue(cut, "Should force cut after max duration (12s)");
        }

        // =====================================================================
        // BroadcastDirectorLogic Tests
        // =====================================================================

        [Test]
        public void DirectorLogic_InitialState_IsLive()
        {
            var director = new BroadcastDirectorLogic();
            Assert.AreEqual(DirectorState.Live, director.State,
                $"Director should start in Live state but was {director.State}");
        }

        [Test]
        public void DirectorLogic_GoalEvent_TransitionsToCelebration()
        {
            var director = new BroadcastDirectorLogic();
            DirectorState? newState = null;
            director.OnStateChanged += (oldS, newS) => newState = newS;

            director.NotifyEvent(BroadcastGameEvent.Goal);
            Assert.AreEqual(DirectorState.Celebration, director.State,
                $"Goal event should transition to Celebration, was {director.State}");
        }

        [Test]
        public void DirectorLogic_GoalEvent_SetsCelebrationAngle()
        {
            var director = new BroadcastDirectorLogic();
            director.NotifyEvent(BroadcastGameEvent.Goal);
            Assert.AreEqual(CameraAngle.Celebration, director.ActiveAngle,
                $"Goal should set Celebration angle, was {director.ActiveAngle}");
        }

        [Test]
        public void DirectorLogic_CelebrationEnds_TransitionsToReplay()
        {
            var director = new BroadcastDirectorLogic();
            director.SetCelebrationDuration(2f);

            director.NotifyEvent(BroadcastGameEvent.Goal);
            Assert.AreEqual(DirectorState.Celebration, director.State);

            // Update past celebration duration
            director.Update(2.5f, 0f, 0f);
            Assert.AreEqual(DirectorState.Replay, director.State,
                $"After celebration, should transition to Replay, was {director.State}");
        }

        [Test]
        public void DirectorLogic_KickoffEvent_ReturnsToLive()
        {
            var director = new BroadcastDirectorLogic();
            director.NotifyEvent(BroadcastGameEvent.Goal);
            Assert.AreEqual(DirectorState.Celebration, director.State);

            director.NotifyEvent(BroadcastGameEvent.Kickoff);
            Assert.AreEqual(DirectorState.Live, director.State,
                "Kickoff event should return to Live state");
        }

        [Test]
        public void DirectorLogic_StartTacticalView_TransitionsToTactical()
        {
            var director = new BroadcastDirectorLogic();
            director.StartTacticalView(5f);
            Assert.AreEqual(DirectorState.Tactical, director.State);
            Assert.AreEqual(CameraAngle.Tactical, director.ActiveAngle);
        }

        [Test]
        public void DirectorLogic_TacticalView_ReturnsToLiveAfterDuration()
        {
            var director = new BroadcastDirectorLogic();
            director.StartTacticalView(3f);

            director.Update(3.5f, 0f, 0f);
            Assert.AreEqual(DirectorState.Live, director.State,
                "Should return to Live after tactical duration expires");
        }

        [Test]
        public void DirectorLogic_ReturnToLive_ForcesLiveState()
        {
            var director = new BroadcastDirectorLogic();
            director.StartReplay();
            Assert.AreEqual(DirectorState.Replay, director.State);

            director.ReturnToLive();
            Assert.AreEqual(DirectorState.Live, director.State,
                "ReturnToLive should force Live state");
        }

        // =====================================================================
        // TVOverlayConfig Tests
        // =====================================================================

        [Test]
        public void TVOverlayConfig_DefaultValues_AreCentered()
        {
            var config = new TVOverlayConfig();
            Assert.AreEqual(0.5f, config.ScoreBugPositionX, 0.01f,
                "Score bug X should default to center (0.5)");
            Assert.That(config.ScoreBugPositionY, Is.InRange(0.85f, 1f),
                $"Score bug Y should be near top, was {config.ScoreBugPositionY}");
        }

        [Test]
        public void TVOverlayConfig_ValuesClampedToZeroOne()
        {
            var config = new TVOverlayConfig(scoreBugPositionX: -0.5f, scoreBugPositionY: 1.5f);
            Assert.AreEqual(0f, config.ScoreBugPositionX, 0.01f,
                "Negative position should clamp to 0");
            Assert.AreEqual(1f, config.ScoreBugPositionY, 0.01f,
                "Position > 1 should clamp to 1");
        }

        // =====================================================================
        // ReplayCameraConfig Validation Tests
        // =====================================================================

        [Test]
        public void ReplayCameraConfig_InvalidSlowMoSpeed_ThrowsException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new ReplayCameraConfig(slowMoSpeedFirst: 0f));
        }

        [Test]
        public void ReplayCameraConfig_InvalidOverlayOpacity_ThrowsException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new ReplayCameraConfig(overlayOpacity: 1.5f));
        }

        [Test]
        public void ReplayCameraConfig_InvalidAngleCount_ThrowsException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new ReplayCameraConfig(multiAngleCount: 0));
        }

        // =====================================================================
        // DirectorConfig Validation Tests
        // =====================================================================

        [Test]
        public void DirectorConfig_InvalidBaseCutFrequency_ThrowsException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new DirectorConfig(baseCutFrequency: 0f));
        }

        [Test]
        public void DirectorConfig_MaxLessThanBase_ThrowsException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new DirectorConfig(baseCutFrequency: 10f, maxCutFrequency: 5f));
        }

        [Test]
        public void DirectorConfig_InvalidMomentumThreshold_ThrowsException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new DirectorConfig(momentumThreshold: 1.5f));
        }
    }
}
