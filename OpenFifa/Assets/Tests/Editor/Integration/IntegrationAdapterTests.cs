using System.Collections.Generic;
using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor.Integration
{
    [TestFixture]
    [Category("FID_INTEGRATION")] // Unity NUnit rejects "-" in category names
    public class IntegrationAdapterTests
    {
        private const float Eps = 1e-4f;

        private static MatchSim NewSim(ulong seed = 1) => new MatchSim(new MatchSimConfig(), MatchSetup.CreateDefault(seed));

        // ------------------------------------------------------------------ formation / roles

        [Test]
        public void FormationMapper_DefaultSetup_FitsFourFourTwo()
        {
            Assert.AreEqual(TeamAIFormation.F442, SimFormationMapper.BestFit(MatchSetup.DefaultFormation()));
        }

        [Test]
        public void Adapter_DefaultSetup_AssignsRolesBySlotGeometry()
        {
            var a = new SimTeamAIAdapter(NewSim());
            Assert.AreEqual(TeamAIRole.GK, a.RoleOfPlayer[0]);
            Assert.AreEqual(TeamAIRole.FB, a.RoleOfPlayer[1]); // (-24, -34): left back
            Assert.AreEqual(TeamAIRole.CB, a.RoleOfPlayer[2]);
            Assert.AreEqual(TeamAIRole.CB, a.RoleOfPlayer[3]);
            Assert.AreEqual(TeamAIRole.FB, a.RoleOfPlayer[4]);
            Assert.AreEqual(TeamAIRole.ST, a.RoleOfPlayer[9]);
            Assert.AreEqual(TeamAIRole.ST, a.RoleOfPlayer[10]);
            Assert.AreEqual(TeamAIRole.GK, a.RoleOfPlayer[11]);
            // Left back maps to the template's left full-back slot (negative lateral).
            Assert.Less(TeamAIFormations.GetSlot(a.HomeTactics.Formation, a.SlotOfPlayer[1]).Lateral, 0f);
            // Every template slot used exactly once per team.
            var seen = new HashSet<int>();
            for (int i = 0; i < 11; i++) Assert.IsTrue(seen.Add(a.SlotOfPlayer[i]));
        }

        [Test]
        public void Adapter_Fill_CopiesSimStateIntoWorld()
        {
            var sim = NewSim();
            var a = new SimTeamAIAdapter(sim);
            a.Fill(1L << 5);
            var s = sim.State;
            Assert.AreEqual(22, a.World.PlayerCount);
            for (int i = 0; i < 22; i++)
            {
                Assert.AreEqual(i, a.World.Players[i].Id);
                Assert.AreEqual(s.Players[i].Position.X, a.World.Players[i].Position.X, Eps);
                Assert.AreEqual(s.Players[i].Position.Z, a.World.Players[i].Position.Z, Eps);
                Assert.AreEqual(i == 5, a.World.Players[i].HumanControlled);
                Assert.AreEqual(i < 11 ? 0 : 1, a.World.Players[i].Team);
            }
            Assert.AreEqual(s.Owner, a.World.Ball.OwnerId);
            Assert.AreEqual(1, a.World.HomeAttackSign);
            Assert.AreEqual(TeamAIMatchPhase.Kickoff, a.World.MatchPhase);
            Assert.AreEqual(0, a.World.RestartTeam);
        }

        [Test]
        public void MapRestart_AllRestartTypes_MapToTeamAIPhases()
        {
            Assert.AreEqual(TeamAIMatchPhase.Corner, SimTeamAIAdapter.MapRestart(RestartType.Corner));
            Assert.AreEqual(TeamAIMatchPhase.FreeKick, SimTeamAIAdapter.MapRestart(RestartType.IndirectFreeKick));
            Assert.AreEqual(TeamAIMatchPhase.FreeKick, SimTeamAIAdapter.MapRestart(RestartType.DirectFreeKick));
            Assert.AreEqual(TeamAIMatchPhase.Penalty, SimTeamAIAdapter.MapRestart(RestartType.Penalty));
            Assert.AreEqual(TeamAIMatchPhase.ThrowIn, SimTeamAIAdapter.MapRestart(RestartType.ThrowIn));
            Assert.AreEqual(TeamAIMatchPhase.GoalKick, SimTeamAIAdapter.MapRestart(RestartType.GoalKick));
        }

        // ------------------------------------------------------------------ AI command -> intent

        [Test]
        public void AIToIntent_PassCommand_MapsActionAimAndTarget()
        {
            var p = new SimPlayer { Id = 3, Position = new SimVec3(0f, 0f, 0f) };
            var cmd = AIPlayerCommand.Idle(3, p.Position);
            cmd.MoveTarget = new SimVec3(0f, 0f, 10f);
            cmd.MoveDirection = new SimVec3(0f, 0f, 1f);
            cmd.Action = TeamAIAction.LobPass;
            cmd.Power = 0.7f;
            cmd.Aim = new SimVec3(1f, 0f, 0f);
            cmd.TargetPlayerId = 7;
            var intent = SimTeamAIAdapter.ToIntent(cmd, p, true);
            Assert.AreEqual(IntentAction.Lob, intent.Action);
            Assert.AreEqual(0.7f, intent.Power, Eps);
            Assert.AreEqual(1f, intent.Aim.X, Eps);
            Assert.AreEqual(7, intent.TargetPlayerId);
            Assert.AreEqual(1f, intent.Move.Z, Eps);
        }

        [Test]
        public void AIToIntent_OneShotNotAllowed_DropsKickKeepsJockey()
        {
            var p = new SimPlayer { Id = 3 };
            var cmd = AIPlayerCommand.Idle(3, p.Position);
            cmd.Action = TeamAIAction.Shoot;
            Assert.AreEqual(IntentAction.None, SimTeamAIAdapter.ToIntent(cmd, p, false).Action);
            cmd.Action = TeamAIAction.Jockey;
            Assert.AreEqual(IntentAction.Jockey, SimTeamAIAdapter.ToIntent(cmd, p, false).Action);
        }

        [Test]
        public void AIToIntent_AtTarget_StandsStill()
        {
            var p = new SimPlayer { Id = 3, Position = new SimVec3(5f, 0f, 5f) };
            var cmd = AIPlayerCommand.Idle(3, new SimVec3(5.2f, 0f, 5f));
            Assert.AreEqual(0f, SimTeamAIAdapter.ToIntent(cmd, p, true).Move.Magnitude, Eps);
            cmd.MoveTarget = new SimVec3(6.5f, 0f, 5f); // 1.5 m: slowing down, not full throttle
            float m = SimTeamAIAdapter.ToIntent(cmd, p, true).Move.Magnitude;
            Assert.Greater(m, 0f);
            Assert.Less(m, 1f);
        }

        // ------------------------------------------------------------------ human control

        [Test]
        public void StickToPitch_CameraYaws_RotateStick()
        {
            var up = new ControlVec2(0f, 1f);
            var right = new ControlVec2(1f, 0f);
            var a = HumanControlAdapter.StickToPitch(up, 0f);
            Assert.AreEqual(1f, a.Z, Eps);
            Assert.AreEqual(0f, a.X, Eps);
            var b = HumanControlAdapter.StickToPitch(right, 0f);
            Assert.AreEqual(1f, b.X, Eps);
            // Side-on broadcast camera looking down +X: stick up = +X, stick right = -Z.
            float yaw = SimMath.Pi * 0.5f;
            var c = HumanControlAdapter.StickToPitch(up, yaw);
            Assert.AreEqual(1f, c.X, Eps);
            var d = HumanControlAdapter.StickToPitch(right, yaw);
            Assert.AreEqual(-1f, d.Z, Eps);
        }

        [Test]
        public void PitchToStick_IsInverseOfStickToPitch()
        {
            var stick = new ControlVec2(0.3f, -0.8f);
            for (float yaw = -3f; yaw < 3f; yaw += 0.7f)
            {
                var back = HumanControlAdapter.PitchToStick(HumanControlAdapter.StickToPitch(stick, yaw), yaw);
                Assert.AreEqual(stick.X, back.X, 1e-4f);
                Assert.AreEqual(stick.Y, back.Y, 1e-4f);
            }
        }

        [Test]
        public void MapAction_ControlActions_MapToSimActions()
        {
            Assert.AreEqual(IntentAction.Pass, HumanControlAdapter.MapAction(ControlAction.DrivenPass));
            Assert.AreEqual(IntentAction.ThroughPass, HumanControlAdapter.MapAction(ControlAction.ThroughBall));
            Assert.AreEqual(IntentAction.Lob, HumanControlAdapter.MapAction(ControlAction.LobbedThrough));
            Assert.AreEqual(IntentAction.Cross, HumanControlAdapter.MapAction(ControlAction.Cross));
            Assert.AreEqual(IntentAction.Shoot, HumanControlAdapter.MapAction(ControlAction.LowShot));
            Assert.AreEqual(IntentAction.FinesseShot, HumanControlAdapter.MapAction(ControlAction.FinesseShot));
            Assert.AreEqual(IntentAction.DrivenShot, HumanControlAdapter.MapAction(ControlAction.DrivenShot));
            Assert.AreEqual(IntentAction.SlideTackle, HumanControlAdapter.MapAction(ControlAction.SlideTackle));
            Assert.AreEqual(IntentAction.Jockey, HumanControlAdapter.MapAction(ControlAction.Contain));
            Assert.AreEqual(IntentAction.None, HumanControlAdapter.MapAction(ControlAction.SwitchPlayer));
            Assert.AreEqual(IntentAction.None, HumanControlAdapter.MapAction(ControlAction.SkillMove));
        }

        [Test]
        public void BuildContext_KickOffTaker_CanStrikeInPossession()
        {
            var sim = NewSim();
            int taker = sim.State.RestartTakerId;
            Assert.GreaterOrEqual(taker, 0);
            var ctx = HumanControlAdapter.BuildContext(sim, SimTeam.Home, taker);
            Assert.IsTrue(ctx.InPossession);
            Assert.IsTrue(ctx.CanStrike);
            Assert.IsFalse(ctx.InCrossingZone);
            var def = HumanControlAdapter.BuildContext(sim, SimTeam.Away, 20);
            Assert.IsFalse(def.InPossession);
            Assert.IsFalse(def.CanStrike);
        }

        [Test]
        public void BuildContext_WideInFinalThird_IsCrossingZone()
        {
            var sim = NewSim();
            sim.State.Players[8].Position = new SimVec3(30f, 0f, 40f);
            Assert.IsTrue(HumanControlAdapter.BuildContext(sim, SimTeam.Home, 8).InCrossingZone);
            sim.State.Players[8].Position = new SimVec3(0f, 0f, 40f);
            Assert.IsFalse(HumanControlAdapter.BuildContext(sim, SimTeam.Home, 8).InCrossingZone);
        }

        [Test]
        public void HumanToIntent_PassWithAssist_TargetsTeammateInStickDirection()
        {
            var sim = NewSim();
            var s = sim.State;
            s.Players[5].Position = new SimVec3(0f, 0f, 0f);
            s.Players[6].Position = new SimVec3(2f, 0f, 15f);   // ahead
            s.Players[7].Position = new SimVec3(-15f, 0f, 0f);  // left
            var mates = new List<ControlPlayerInfo>
            {
                new ControlPlayerInfo(5, s.Players[5].Position), new ControlPlayerInfo(6, s.Players[6].Position),
                new ControlPlayerInfo(7, s.Players[7].Position),
            };
            var ci = new ControlIntent { Action = ControlAction.Pass, Power = 0.4f, Aim = new ControlVec2(0f, 1f) };
            var intent = HumanControlAdapter.ToIntent(ci, 0f, sim, 5, mates, 1f, 1f);
            Assert.AreEqual(IntentAction.Pass, intent.Action);
            Assert.AreEqual(6, intent.TargetPlayerId);
            Assert.Greater(intent.Aim.Z, 0.9f);
        }

        [Test]
        public void HumanToIntent_ShotFullAssist_AimsInsidePosts()
        {
            var sim = NewSim();
            sim.State.Players[9].Position = new SimVec3(-20f, 0f, 35f);
            var ci = new ControlIntent { Action = ControlAction.Shoot, Power = 0.8f, Aim = new ControlVec2(-1f, 0f) };
            var intent = HumanControlAdapter.ToIntent(ci, 0f, sim, 9, null, 1f, 1f);
            // Ray from the shooter along Aim must cross the goal line between the posts.
            var p = sim.State.Players[9].Position;
            float t = (52.5f - p.Z) / intent.Aim.Z;
            float x = p.X + intent.Aim.X * t;
            Assert.Less(System.Math.Abs(x), 3.66f);
        }

        // ------------------------------------------------------------------ selector

        [Test]
        public void Selector_PassToTeammate_AutoSwitchesToReceiver()
        {
            var sim = NewSim();
            var sel = new HumanPlayerSelector(SimTeam.Home, 9);
            sim.State.Phase = SimMatchPhase.InPlay;
            sim.State.Owner = -1;
            var events = new List<MatchEvent>
            {
                new MatchEvent { Type = MatchEventType.Pass, PlayerId = 9, OtherPlayerId = 6, Team = SimTeam.Home },
            };
            sel.Update(sim, events, default, true, 0f);
            Assert.AreEqual(6, sel.ControlledId);
            Assert.AreEqual(1, sel.SwitchCount);
        }

        [Test]
        public void Selector_ManualSwitch_PicksAnotherOutfielderNeverKeeper()
        {
            var sim = NewSim();
            var s = sim.State;
            s.Phase = SimMatchPhase.InPlay;
            s.Owner = 15; // opponent on the ball
            s.Ball.Position = new SimVec3(0f, 0.11f, -45f); // right in front of our keeper
            var sel = new HumanPlayerSelector(SimTeam.Home, 10);
            var ci = new ControlIntent { Action = ControlAction.SwitchPlayer };
            sel.Update(sim, null, ci, true, 0f);
            Assert.AreNotEqual(10, sel.ControlledId);
            Assert.AreNotEqual(0, sel.ControlledId);
            Assert.Less(sel.ControlledId, 11);
        }

        [Test]
        public void Selector_OtherSeatPlayer_IsNeverTaken()
        {
            var sim = NewSim();
            var sel = new HumanPlayerSelector(SimTeam.Home, 9);
            sim.State.Phase = SimMatchPhase.InPlay;
            sim.State.Owner = -1;
            var events = new List<MatchEvent>
            {
                new MatchEvent { Type = MatchEventType.Pass, PlayerId = 9, OtherPlayerId = 6, Team = SimTeam.Home },
            };
            sel.Update(sim, events, default, true, 0f, 1L << 6);
            Assert.AreEqual(9, sel.ControlledId);
        }

        // ------------------------------------------------------------------ animation

        [Test]
        public void AnimTracker_KickWindup_ReportsContactOnSimClock()
        {
            var sim = NewSim();
            var tracker = new SimAnimActionTracker();
            int taker = sim.State.RestartTakerId;
            var intents = new PlayerIntent[22];
            for (int i = 0; i < 22; i++) intents[i] = PlayerIntent.Idle;
            intents[taker] = PlayerIntent.Kick(IntentAction.Pass, 0.5f, new SimVec3(-1f, 0f, -0.2f));
            var events = sim.Step(intents);
            tracker.Update(sim, events);
            var a = tracker.Get(taker);
            Assert.IsFalse(a.IsNone);
            Assert.AreEqual(AnimActionKind.Pass, a.Kind);
            Assert.AreEqual(1, a.Id);
            Assert.AreEqual(sim.State.Players[taker].ContactTick, a.ContactTick);
            Assert.AreEqual(a.ContactTick * sim.Dt, a.ContactTime, 1e-5f);
            Assert.Greater(a.ContactTime, tracker.SimTime);
            Assert.IsTrue(a.HasRootAtContact);
            Assert.Less(a.Direction.X, 0f);
            Assert.IsTrue(tracker.Get(3).IsNone);
        }

        [Test]
        public void AnimTracker_KindOf_HighBallIsHeader()
        {
            Assert.AreEqual(AnimActionKind.Header, SimAnimActionTracker.KindOf(IntentAction.Shoot, 1.8f));
            Assert.AreEqual(AnimActionKind.Shot, SimAnimActionTracker.KindOf(IntentAction.DrivenShot, 0.11f));
            Assert.AreEqual(AnimActionKind.StandingTackle, SimAnimActionTracker.KindOf(IntentAction.Tackle, 0.11f));
            Assert.AreEqual(AnimActionKind.Cross, SimAnimActionTracker.KindOf(IntentAction.Cross, 0.11f));
        }

        // ------------------------------------------------------------------ commentary

        [Test]
        public void Commentary_ShotThenSave_EmitsShotSavedWithNames()
        {
            var sim = NewSim();
            var names = new string[22];
            for (int i = 0; i < 22; i++) names[i] = "P" + i;
            var c = new SimCommentaryAdapter(sim, names);
            var ev = new List<MatchEvent>
            {
                new MatchEvent { Type = MatchEventType.Shot, PlayerId = 9, Team = SimTeam.Home, Position = new SimVec3(0f, 0.1f, 30f), Value = 0.2f },
            };
            var struck = c.Convert(ev, 1.0);
            Assert.AreEqual(1, struck.Count);
            Assert.AreEqual(CommentaryEventType.ShotTaken, struck[0].Type, "the strike is called before the outcome");
            Assert.AreEqual("P9", struck[0].PlayerName);
            ev[0] = new MatchEvent { Type = MatchEventType.Save, PlayerId = 11, OtherPlayerId = 9, Team = SimTeam.Away };
            var outp = c.Convert(ev, 1.5);
            Assert.AreEqual(1, outp.Count);
            Assert.AreEqual(CommentaryEventType.ShotSaved, outp[0].Type);
            Assert.AreEqual("P9", outp[0].PlayerName);
            Assert.AreEqual("P11", outp[0].SecondaryPlayerName);
            Assert.AreEqual(0, outp[0].Team);
            Assert.AreEqual(0.2f, outp[0].ShotQuality, Eps);
            Assert.AreEqual(22.5f, outp[0].DistanceMetres, 0.01f);
        }

        [Test]
        public void Commentary_LongShotGoal_IsLongRange_OwnGoalIsOwnGoal()
        {
            var sim = NewSim();
            var c = new SimCommentaryAdapter(sim);
            var ev = new List<MatchEvent>
            {
                new MatchEvent { Type = MatchEventType.Shot, PlayerId = 9, Team = SimTeam.Home, Position = new SimVec3(0f, 0.1f, 25f) },
                new MatchEvent { Type = MatchEventType.Goal, PlayerId = 9, OtherPlayerId = -1, Team = SimTeam.Home },
            };
            var outp = c.Convert(ev, 2.0);
            Assert.AreEqual(2, outp.Count);
            Assert.AreEqual(CommentaryEventType.ShotTaken, outp[0].Type);
            Assert.AreEqual(CommentaryEventType.Goal, outp[1].Type);
            Assert.AreEqual(GoalSituation.LongRange, outp[1].Situation);
            ev.Clear();
            ev.Add(new MatchEvent { Type = MatchEventType.Goal, PlayerId = 3, Team = SimTeam.Away, Detail = 1 });
            outp = c.Convert(ev, 3.0);
            Assert.AreEqual(GoalSituation.OwnGoal, outp[0].Situation);
            Assert.AreEqual(1, outp[0].Team);
        }

        [Test]
        public void Commentary_RealEngine_ProducesSubtitleForMatchEvents()
        {
            var bank = CommentaryLineBank.Load(System.IO.File.ReadAllText(RosterTestData.LinesPath), out var errors);
            Assert.IsNotNull(bank, string.Join("; ", errors));
            var session = new SinglePlayerMatchSession(new SinglePlayerMatchOptions { Seed = 3, Config = new MatchSimConfig { HalfDurationSeconds = 60f } });
            var engine = new CommentaryEngine(bank, null, 7);
            session.AttachCommentaryEngine(engine);
            var bot = new ScriptedGamepadBot();
            int cues = 0, events = 0;
            while (!session.IsFinished)
            {
                session.Advance(1.0 / 30.0, bot);
                events += session.FrameCommentary.Count;
                foreach (var cue in session.FrameCues)
                    if (cue.Kind == CommentaryCueKind.Start && cue.Utterance.Transcript.Length > 0) cues++;
            }
            Assert.Greater(events, 5);
            Assert.Greater(cues, 2);
        }

        // ------------------------------------------------------------------ rosters

        [Test]
        public void RosterRating_Scale_MapsEnds()
        {
            Assert.AreEqual(15, RosterMatchBuilder.ToSimRating(1));
            Assert.AreEqual(99, RosterMatchBuilder.ToSimRating(20));
            Assert.AreEqual(72, RosterMatchBuilder.ToSimRating(14));
            Assert.AreEqual(TeamAIFormation.F4231, RosterMatchBuilder.ParseFormation("4-2-3-1"));
            Assert.AreEqual(TeamAIFormation.F352, RosterMatchBuilder.ParseFormation("3-5-2"));
        }

        [Test]
        public void RosterSetup_BundledLeague_BuildsPlayableMatch()
        {
            var problems = new List<string>();
            var leagues = RosterMatchBuilder.LoadLeagues(RosterTestData.StreamingAssets, null, problems);
            Assert.AreEqual(0, problems.Count, string.Join("\n", problems));
            Assert.GreaterOrEqual(leagues.Count, 1);
            var clubs = leagues[0].Clubs;
            var setup = RosterMatchBuilder.BuildSetup(clubs[0], clubs[1], 5, out var home, out var away, out var names, out var ids);
            Assert.IsTrue(setup.Home[0].IsGoalkeeper);
            Assert.AreEqual(RosterPosition.GK, home.Starters[0].PrimaryPosition);
            var unique = new HashSet<string>(ids);
            Assert.AreEqual(22, unique.Count);
            for (int i = 0; i < 22; i++) Assert.IsFalse(string.IsNullOrEmpty(names[i]));
            for (int s = 1; s < 11; s++)
            {
                Assert.Less(setup.HomeFormation[s].Z, 0f);
                float r = (float)System.Math.Sqrt(setup.HomeFormation[s].X * setup.HomeFormation[s].X + setup.HomeFormation[s].Z * setup.HomeFormation[s].Z);
                Assert.Greater(r, 9.15f);
            }
            var session = new SinglePlayerMatchSession(new SinglePlayerMatchOptions
            {
                Setup = setup, Seed = 5, HomeTactics = home.Tactics, AwayTactics = away.Tactics, PlayerNames = names, PlayerIds = ids,
                Config = new MatchSimConfig { HalfDurationSeconds = 30f },
            });
            var bot = new ScriptedGamepadBot();
            while (!session.IsFinished) session.StepOnce(bot);
            Assert.IsTrue(session.Result.Finished);
        }
    }
}
