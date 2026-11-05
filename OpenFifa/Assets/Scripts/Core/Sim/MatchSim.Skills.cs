namespace OpenFifa.Core
{
    /// <summary>
    /// Skill moves: a carrier commits to a scripted body-and-ball sequence (<see cref="SkillMoveCatalog"/>). The body
    /// follows the move's path, the ball is carried on the foot (it is steered every tick, never teleported), the fake
    /// may wrong-foot defenders, and the move ends with a release touch onto the exit heading.
    /// </summary>
    public sealed partial class MatchSim
    {
        /// <summary>Starts a skill move for the carrier, gated by his star rating. False if he cannot do one now.</summary>
        private bool TryStartSkill(ref SimPlayer p, PlayerIntent intent)
        {
            var ac = _cfg.Actions;
            if (_s.Phase != SimMatchPhase.InPlay || _s.Owner != p.Id || _s.KeeperHolding) return false;
            SimPlayerAttributes a = AttributesOf(p.Id);
            if (a.IsGoalkeeper) return false;
            SimVec3 toBall = (_s.Ball.Position - p.Position).Flat;
            if (toBall.Magnitude > ac.SkillStartReach || _s.Ball.Position.Y > 0.5f) return false;

            SkillMoveKind kind = SkillMoveCatalog.Gate(intent.Skill, a.SkillMoves);
            if (kind == SkillMoveKind.None) return false;
            SkillMoveProfile prof = SkillMoveCatalog.Get(kind);

            float entryYaw = p.Speed > 0.5f ? SimMath.Yaw(p.Velocity.Flat) : p.Facing;
            SimVec3 aim = intent.Aim.Flat;
            float side;
            if (aim.SqrMagnitude > 1e-4f)
            {
                float rel = SimMath.DeltaAngle(p.Facing, SimMath.Yaw(aim));
                side = SimMath.Abs(rel) < 1e-3f ? DefaultSkillSide(a) : SimMath.Sign(rel);
            }
            else
            {
                side = DefaultSkillSide(a);
            }

            float pressure = PressureOn(p.Id);
            float q = 0.50f * SimPlayerAttributes.N(a.Dribbling) + 0.25f * SimPlayerAttributes.N(a.Agility)
                      + 0.25f * SimPlayerAttributes.N(a.BallControl)
                      + 0.04f * (SimMath.Clamp(a.SkillMoves, 1, 5) - prof.RequiredStars)
                      - 0.20f * pressure * (1f - SimPlayerAttributes.N(a.Composure))
                      + 0.06f * _s.Rng.NextGaussian();

            p.ActionState = SimPlayerActionState.Skill;
            p.Skill = kind;
            p.SkillStartTick = _s.Tick;
            p.StateEndTick = _s.Tick + SimMath.Clamp(_cfg.SecondsToTicks(prof.Duration), 2, 1000);
            p.SkillEntryYaw = entryYaw;
            p.SkillExitYaw = entryYaw + side * prof.ExitAngleDeg * SimMath.Deg2Rad;
            p.SkillEntryFacing = p.Facing;
            p.SkillEntrySpeed = p.Speed;
            p.SkillBallFrom = toBall;
            p.SkillQuality = SimMath.Clamp01(q);
            p.Jockeying = false;
            p.Shielding = false;
            Emit(MatchEventType.SkillMove, p.Id, -1, p.Team, p.Position, prof.Duration, p.SkillQuality, (int)kind,
                IntentAction.SkillMove);
            return true;
        }

        /// <summary>No side given: go toward the stronger foot's outside (a right-footer drops the left shoulder, goes right).</summary>
        private static float DefaultSkillSide(SimPlayerAttributes a) => a.PreferredFoot == SimFoot.Left ? -1f : 1f;

        /// <summary>Exit side of the move in progress: +1 right of the entry heading, -1 left.</summary>
        private static float SkillSide(in SimPlayer p) => SimMath.Sign(p.SkillExitYaw - p.SkillEntryYaw);

        private static float Smooth(float u)
        {
            u = SimMath.Clamp01(u);
            return u * u * (3f - 2f * u);
        }

        /// <summary>Advances one tick of the move: body path, facing, the fake and the carried ball.</summary>
        private void StepSkill(int id, float dt)
        {
            ref SimPlayer p = ref _s.Players[id];
            var ac = _cfg.Actions;
            if (_s.Owner != id || _s.Phase != SimMatchPhase.InPlay)
            {
                // Lost the ball (poked away, out of play): abandon the move.
                p.ActionState = SimPlayerActionState.Recovery;
                p.StateEndTick = _s.Tick + _cfg.SecondsToTicks(0.15f);
                p.Skill = SkillMoveKind.None;
                return;
            }

            SkillMoveProfile prof = SkillMoveCatalog.Get(p.Skill);
            SimPlayerAttributes a = AttributesOf(id);
            int total = SimMath.Clamp(p.StateEndTick - p.SkillStartTick, 1, 100000);
            int k = _s.Tick - p.SkillStartTick;
            float u = SimMath.Clamp01(k / (float)total);
            float side = SkillSide(p);

            // Body path: hold the entry heading while setting the move up, then swing onto the exit and burst.
            float turnU = Smooth((u - prof.TurnStart) / SimMath.Max(1e-3f, 1f - prof.TurnStart));
            float heading = p.SkillEntryYaw + (p.SkillExitYaw - p.SkillEntryYaw) * turnU;
            float setup = p.SkillEntrySpeed * prof.SetupSpeedFactor;
            float exit = PlayerKinematics.SprintSpeed(a, _cfg.Kinematics, p.Stamina) * prof.ExitSpeedFraction
                         * (0.85f + 0.15f * SimPlayerAttributes.N(a.Acceleration));
            float speed = u < prof.TurnStart
                ? SimMath.Lerp(p.SkillEntrySpeed, setup, u / SimMath.Max(1e-3f, prof.TurnStart))
                : SimMath.Lerp(setup, exit, turnU);
            SimVec3 vel = SimMath.FromYaw(heading) * speed;

            float sway = 0f;
            if (prof.HasFake && u < prof.FakeAt)
            {
                // Selling the fake: the hips and shoulders dip toward the other side.
                sway = (float)System.Math.Sin(System.Math.PI * u / prof.FakeAt);
                vel += SimMath.FromYaw(p.SkillEntryYaw - side * 1.5707964f) * (0.9f * sway);
            }

            p.Velocity = vel;
            p.Position = new SimVec3(p.Position.X + vel.X * dt, 0f, p.Position.Z + vel.Z * dt);
            ClampToArea(ref p);

            if (prof.KeepFacing)
            {
                p.Facing = p.SkillEntryFacing;
            }
            else
            {
                float spin = side * prof.SpinTurns * 6.2831853f * turnU;
                float swing = (p.SkillExitYaw - p.SkillEntryYaw) * turnU;
                p.Facing = SimMath.WrapAngle(p.SkillEntryFacing + swing + spin - side * 0.35f * sway);
            }

            if (prof.HasFake && k == SimMath.Clamp((int)(prof.FakeAt * total + 0.5f), 1, total - 1)) SellFake(id);

            // The ball rides the foot from where it was to the exit lead, steered by velocity so the integrator and the
            // ball-vs-body collisions see a continuous path.
            if (_s.Tick >= p.StateEndTick)
            {
                ReleaseSkill(id);
                return;
            }

            float bu = Smooth((k + 1) / (float)total);
            SimVec3 off = p.SkillBallFrom * (1f - bu) + SimMath.FromYaw(p.SkillExitYaw) * (prof.BallLead * bu);
            SimVec3 target = p.Position + vel * dt + off;
            float r = _cfg.Ball.Radius;
            SimVec3 bv = ((target - _s.Ball.Position) / dt).Flat;
            bv = SimVec3.ClampMagnitude(bv, speed + 6f);
            _s.Ball.Position = new SimVec3(_s.Ball.Position.X, r, _s.Ball.Position.Z);
            _s.Ball.Velocity = bv;
            _s.Ball.Spin = BallIntegrator.RollingSpin(bv, r, 0f);
            _s.Ball.Grounded = true;
            _s.Ball.ClearKnuckle();
            _s.LastTouchPlayer = id;
            _s.LastTouchTeam = p.Team;
        }

        /// <summary>End of the move: a release touch onto the exit heading. A poor move is a heavy, wayward touch.</summary>
        private void ReleaseSkill(int id)
        {
            ref SimPlayer p = ref _s.Players[id];
            var ac = _cfg.Actions;
            SkillMoveProfile prof = SkillMoveCatalog.Get(p.Skill);
            float miss = 1f - p.SkillQuality;
            float err = _s.Rng.NextGaussian() * miss * ac.SkillReleaseErrorDeg * SimMath.Deg2Rad;
            float exitYaw = SimMath.WrapAngle(p.SkillExitYaw);
            SimVec3 dir = SimMath.FromYaw(exitYaw + err);
            float botch = miss * miss * ac.SkillBotchLead;
            float speed = SimMath.Min(p.Speed + prof.ReleasePush + 2f * botch, p.Speed + ac.MaxTouchSpeedOverPlayer + 2f * botch);
            float r = _cfg.Ball.Radius;
            SimVec3 v = dir * speed;
            _s.Ball.Position = new SimVec3(_s.Ball.Position.X, r, _s.Ball.Position.Z);
            _s.Ball.Velocity = v;
            _s.Ball.Spin = BallIntegrator.RollingSpin(v, r, 0f);
            _s.Ball.Grounded = true;
            _s.Ball.ClearKnuckle();
            _s.LastTouchPlayer = id;
            _s.LastTouchTeam = p.Team;

            p.ActionState = SimPlayerActionState.Free;
            p.Skill = SkillMoveKind.None;
            p.SkillExitYaw = exitYaw;
            p.NextTouchTick = _s.Tick + SimMath.Clamp(_cfg.SecondsToTicks(ac.SkillReleaseTouchGap), 1, 1000);
            p.LastTouchTick = _s.Tick;
            p.LastTouchYaw = exitYaw;
            Emit(MatchEventType.DribbleTouch, id, -1, p.Team, _s.Ball.Position, speed, p.SkillQuality);
        }

        /// <summary>
        /// The fake frame: each nearby defender in front of or beside the carrier reads it or bites. A biter is
        /// wrong-footed: he lunges toward the fake and turns and accelerates sluggishly for a moment. Defenders are
        /// tested in id order with the seeded RNG, so replays match.
        /// </summary>
        private void SellFake(int id)
        {
            ref SimPlayer c = ref _s.Players[id];
            var ac = _cfg.Actions;
            SkillMoveProfile prof = SkillMoveCatalog.Get(c.Skill);
            SimPlayerAttributes ca = AttributesOf(id);
            float side = SkillSide(c);
            SimVec3 entry = SimMath.FromYaw(c.SkillEntryYaw);
            float sell = 0.5f * ca.Dribbling + 0.3f * ca.Agility + 0.2f * ca.Balance + 6f * (SimMath.Clamp(ca.SkillMoves, 1, 5) - 2);
            SimTeam opp = Opponent(c.Team);
            int first = FirstId(opp);
            for (int j = first; j < first + MatchSetup.PlayersPerTeam; j++)
            {
                ref SimPlayer d = ref _s.Players[j];
                if (d.SentOff) continue;
                if (d.ActionState != SimPlayerActionState.Free && d.ActionState != SimPlayerActionState.Recovery) continue;
                SimVec3 to = (d.Position - c.Position).Flat;
                float dist = to.Magnitude;
                if (dist > ac.SkillBiteRadius || dist < 1e-3f) continue;
                if (SimVec3.Dot(to / dist, entry) < -0.3f) continue; // behind the carrier: he cannot see the fake

                SimPlayerAttributes da = AttributesOf(j);
                float read = 0.5f * da.Reactions + 0.25f * da.Agility + 0.25f * da.StandingTackle;
                float z = ac.SkillBiteBase * prof.FakeStrength + (sell - read) / ac.SkillBiteSkillScale
                          + (c.SkillQuality - 0.5f) + (0.4f - 0.3f * dist);
                if (d.Jockeying) z -= ac.SkillBiteJockeyMalus;
                if (_s.Rng.NextFloat() >= SimMath.Logistic(z)) continue;

                d.WrongFootedUntilTick = _s.Tick + _cfg.SecondsToTicks(ac.WrongFootSeconds);
                SimVec3 lunge = c.Skill == SkillMoveKind.FakeShot
                    ? (-to / dist) // throws himself at the shot
                    : SimMath.FromYaw(c.SkillEntryYaw - side * 1.5707964f);
                d.Velocity = d.Velocity.Flat + lunge * ac.WrongFootLunge;
                d.Facing = SimMath.WrapAngle(d.Facing + SimMath.Clamp(SimMath.DeltaAngle(d.Facing, SimMath.Yaw(lunge)), -0.45f, 0.45f));
                Emit(MatchEventType.WrongFooted, j, id, d.Team, d.Position, value: c.SkillQuality, detail: (int)c.Skill);
            }
        }

        /// <summary>Turn/acceleration multiplier for a player who is wrong-footed or off balance this tick.</summary>
        private float Responsiveness(in SimPlayer p) =>
            _s.Tick < p.WrongFootedUntilTick || _s.Tick < p.OffBalanceUntilTick ? _cfg.Actions.WrongFootResponsiveness : 1f;
    }
}
