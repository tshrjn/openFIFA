namespace OpenFifa.Core
{
    /// <summary>Intent handling: movement, action requests, wind-ups and contact-frame resolution.</summary>
    public sealed partial class MatchSim
    {
        private void UpdatePlayers(PlayerIntent[] intents)
        {
            float dt = _cfg.Dt;
            var kc = _cfg.Kinematics;
            for (int i = 0; i < MatchSimState.PlayerCount; i++)
            {
                ref SimPlayer p = ref _s.Players[i];
                if (p.SentOff) continue;
                PlayerIntent intent = intents != null && i < intents.Length ? intents[i] : PlayerIntent.Idle;
                SimPlayerAttributes a = AttributesOf(i);

                if ((p.ActionState == SimPlayerActionState.Recovery || p.ActionState == SimPlayerActionState.Grounded)
                    && _s.Tick >= p.StateEndTick)
                {
                    p.ActionState = SimPlayerActionState.Free;
                }

                if (p.ActionState == SimPlayerActionState.Sliding)
                {
                    StepSlide(ref p, dt);
                    continue;
                }

                if (p.ActionState == SimPlayerActionState.Grounded)
                {
                    p.Velocity = SimVec3.Zero;
                    continue;
                }

                p.Jockeying = intent.Action == IntentAction.Jockey && _s.Owner != i;
                p.Shielding = intent.Action == IntentAction.Shield && _s.Owner == i;

                bool isTaker = _s.Phase == SimMatchPhase.AwaitingRestart && i == _s.RestartTakerId;
                if (isTaker)
                {
                    // The taker stands at the ball; only the facing follows the aim.
                    p.Velocity = SimVec3.Zero;
                    SimVec3 aim = intent.Aim.Flat;
                    if (aim.SqrMagnitude > 1e-4f) p.Facing = SimMath.Yaw(aim);
                }
                else
                {
                    float cap = float.MaxValue;
                    SimVec3 face = SimVec3.Zero;
                    bool sprint = intent.Sprint;
                    float sprintSpeed = PlayerKinematics.SprintSpeed(a, kc, p.Stamina);
                    if (_s.Owner == i)
                    {
                        cap = sprintSpeed * (sprint ? kc.DribbleSprintFraction : kc.DribbleJogFraction);
                        if (_s.KeeperHolding) cap = SimMath.Min(cap, 3f);
                    }

                    if (p.Shielding) { cap = SimMath.Min(cap, kc.ShieldSpeed); sprint = false; }
                    if (p.Jockeying)
                    {
                        cap = SimMath.Min(cap, kc.JockeySpeed);
                        sprint = false;
                        face = (_s.Ball.Position - p.Position).Flat;
                    }

                    if (p.ActionState == SimPlayerActionState.WindUp || p.ActionState == SimPlayerActionState.Recovery)
                        cap = SimMath.Min(cap, SimMath.Max(1.5f, p.Speed * 0.97f));

                    PlayerKinematics.Step(ref p, a, kc, intent.Move, sprint, cap, face, dt);
                    ClampToArea(ref p);
                }

                if (p.IsFree && intent.Action != IntentAction.None) TryStartAction(ref p, intent);
            }
        }

        private void ClampToArea(ref SimPlayer p)
        {
            float mx = _cfg.Pitch.HalfWidth + 4f;
            float mz = _cfg.Pitch.HalfLength + 4f;
            p.Position = new SimVec3(SimMath.Clamp(p.Position.X, -mx, mx), 0f, SimMath.Clamp(p.Position.Z, -mz, mz));
        }

        private void TryStartAction(ref SimPlayer p, PlayerIntent intent)
        {
            var ac = _cfg.Actions;
            IntentAction act = intent.Action;
            float power = SimMath.Clamp01(intent.Power);
            bool restartTaker = _s.Phase == SimMatchPhase.AwaitingRestart && p.Id == _s.RestartTakerId;

            if (PlayerIntent.IsKickAction(act))
            {
                if (!restartTaker)
                {
                    if (_s.Phase != SimMatchPhase.InPlay) return;
                    bool owner = _s.Owner == p.Id;
                    float d = SimMath.FlatDistance(p.Position, _s.Ball.Position);
                    // Non-owners may only strike first time / volley a ball that is close.
                    if (!owner && d > ac.KickReach * 2f) return;
                }

                float windup;
                switch (act)
                {
                    case IntentAction.Lob: windup = ac.LobWindup; break;
                    case IntentAction.Cross: windup = ac.CrossWindup; break;
                    case IntentAction.Shoot: windup = ac.ShotWindup + ac.ShotWindupPerPower * power; break;
                    case IntentAction.FinesseShot: windup = ac.FinesseWindup; break;
                    case IntentAction.DrivenShot: windup = ac.DrivenWindup + ac.ShotWindupPerPower * power * 0.5f; break;
                    default: windup = ac.PassWindup + ac.PassWindupPerPower * power; break;
                }

                BeginWindUp(ref p, act, power, intent.Aim, intent.TargetPlayerId, windup, intent.Style);
                return;
            }

            if (_s.Phase != SimMatchPhase.InPlay || _s.Owner == p.Id) return;

            if (act == IntentAction.Tackle)
            {
                BeginWindUp(ref p, act, 0f, intent.Aim, -1, ac.TackleWindup);
            }
            else if (act == IntentAction.SlideTackle)
            {
                SimVec3 dir = intent.Aim.Flat.SqrMagnitude > 1e-4f ? intent.Aim.Flat.Normalized
                    : (intent.Move.Flat.SqrMagnitude > 1e-4f ? intent.Move.Flat.Normalized : p.FacingDir);
                p.ActionState = SimPlayerActionState.Sliding;
                p.PendingAction = IntentAction.SlideTackle;
                p.PendingAim = dir;
                p.Facing = SimMath.Yaw(dir);
                p.ContactTick = _s.Tick + _cfg.SecondsToTicks(ac.SlideWindup);
                p.StateEndTick = _s.Tick + _cfg.SecondsToTicks(ac.SlideDuration);
                p.Velocity = dir * SimMath.Max(ac.SlideSpeed, p.Speed);
                Emit(MatchEventType.KickWindup, p.Id, -1, p.Team, p.Position, detail: p.ContactTick, action: act);
            }
        }

        private void BeginWindUp(ref SimPlayer p, IntentAction act, float power, SimVec3 aim, int target, float windup,
            KickStyle style = KickStyle.Default)
        {
            p.ActionState = SimPlayerActionState.WindUp;
            p.PendingAction = act;
            p.PendingStyle = PlayerIntent.IsKickAction(act) ? style : KickStyle.Default;
            p.PendingPower = power;
            p.PendingAim = aim.Flat;
            p.PendingTarget = target;
            p.ContactTick = _s.Tick + SimMath.Clamp(_cfg.SecondsToTicks(windup), 1, 1000);
            if (aim.Flat.SqrMagnitude > 1e-4f) p.Facing = SimMath.Yaw(aim.Flat);
            Emit(MatchEventType.KickWindup, p.Id, target, p.Team, p.Position, detail: p.ContactTick, action: act,
                style: p.PendingStyle);
        }

        /// <summary>
        /// Resolves every wind-up and slide whose contact frame has come. Contacts due on the same tick are taken in
        /// a seeded random order (drawn only when two or more coincide), so simultaneous challenges do not always
        /// favour the lower player id (Home).
        /// </summary>
        private void ResolveContacts()
        {
            int n = 0;
            for (int i = 0; i < MatchSimState.PlayerCount; i++)
                if (ContactDue(i)) _scratchContacts[n++] = i;
            for (int k = n - 1; k > 0; k--)
            {
                int j = _s.Rng.Range(0, k + 1);
                int tmp = _scratchContacts[k];
                _scratchContacts[k] = _scratchContacts[j];
                _scratchContacts[j] = tmp;
            }

            for (int k = 0; k < n; k++)
            {
                int i = _scratchContacts[k];
                if (!ContactDue(i)) continue; // an earlier contact this tick changed his state
                ref SimPlayer p = ref _s.Players[i];
                if (p.ActionState == SimPlayerActionState.WindUp)
                {
                    IntentAction act = p.PendingAction;
                    KickStyle style = p.PendingStyle;
                    p.PendingAction = IntentAction.None;
                    p.PendingStyle = KickStyle.Default;
                    p.ActionState = SimPlayerActionState.Recovery;
                    p.StateEndTick = _s.Tick + _cfg.SecondsToTicks(_cfg.Actions.KickRecovery);
                    if (act == IntentAction.Tackle) ResolveTackle(i, false);
                    else ResolveKick(i, act, style);
                }
                else
                {
                    TrySlideContact(i);
                }

                if (_s.Phase != SimMatchPhase.InPlay && _s.Phase != SimMatchPhase.AwaitingRestart) return;
            }
        }

        private bool ContactDue(int i)
        {
            ref SimPlayer p = ref _s.Players[i];
            if (p.SentOff) return false;
            if (p.ActionState == SimPlayerActionState.WindUp) return _s.Tick >= p.ContactTick;
            return p.ActionState == SimPlayerActionState.Sliding && p.PendingAction == IntentAction.SlideTackle
                   && _s.Tick >= p.ContactTick && _s.Phase == SimMatchPhase.InPlay;
        }

        private float PressureOn(int id)
        {
            SimTeam opp = Opponent(TeamOf(id));
            float best = float.MaxValue;
            int first = FirstId(opp);
            for (int j = first; j < first + MatchSetup.PlayersPerTeam; j++)
            {
                if (_s.Players[j].SentOff) continue;
                float d = SimMath.FlatDistance(_s.Players[j].Position, _s.Players[id].Position);
                if (d < best) best = d;
            }

            float r = _cfg.Actions.PressureRadius;
            return SimMath.Clamp01((r - best) / (r - 0.5f));
        }

        private void ResolveKick(int id, IntentAction act, KickStyle style = KickStyle.Default)
        {
            ref SimPlayer p = ref _s.Players[id];
            var ac = _cfg.Actions;
            float r = _cfg.Ball.Radius;
            bool taker = _s.Phase == SimMatchPhase.AwaitingRestart && id == _s.RestartTakerId;
            if (_s.Phase == SimMatchPhase.AwaitingRestart && !taker) return;
            if (_s.Phase != SimMatchPhase.InPlay && !taker) return;

            float flat = SimMath.FlatDistance(p.Position, _s.Ball.Position);
            float h = _s.Ball.Position.Y;
            bool holding = _s.Owner == id && _s.KeeperHolding;
            bool header = false;
            if (!taker && !holding)
            {
                bool foot = flat <= ac.KickReach && h <= ac.KickMaxHeight;
                header = !foot && flat <= ac.KickReach * 0.8f && h >= ac.HeaderMinHeight && h <= ac.HeaderMaxHeight;
                if (!foot && !header)
                {
                    Emit(MatchEventType.Whiff, id, -1, p.Team, p.Position, action: act);
                    return;
                }
            }

            RestartType restart = taker ? _s.PendingRestart : RestartType.None;
            bool throwIn = restart == RestartType.ThrowIn;
            if (throwIn && PlayerIntent.IsShotAction(act)) act = IntentAction.Pass;
            if (throwIn || header) style = KickStyle.Default;

            int sign = AttackSign(p.Team);
            SimVec3 aim = p.PendingAim.SqrMagnitude > 1e-4f ? p.PendingAim.Normalized : p.FacingDir;
            var req = new SimKickRequest
            {
                Action = act,
                Style = style,
                Power = p.PendingPower,
                Aim = aim,
                KickerPosition = p.Position,
                KickerVelocity = p.Velocity,
                KickerFacing = p.Facing,
                BallPosition = _s.Ball.Position,
                BallVelocity = _s.Ball.Velocity,
                Pressure = taker ? 0f : PressureOn(id),
                IsHeader = header,
                IsThrowIn = throwIn,
            };

            int receiver = -1;
            if (PlayerIntent.IsPassAction(act))
            {
                receiver = ChoosePassTarget(id, aim, p.PendingTarget);
                if (receiver >= 0)
                {
                    req.TargetPoint = PassLeadPoint(id, receiver, act, style, req.Power, throwIn);
                    req.HasTarget = true;
                }
            }
            else if (PlayerIntent.IsShotAction(act))
            {
                float goalZ = sign * _cfg.Pitch.HalfLength;
                float dz = goalZ - _s.Ball.Position.Z;
                if (aim.Z * dz > 1e-3f)
                {
                    float t = dz / aim.Z;
                    req.TargetPoint = new SimVec3(_s.Ball.Position.X + aim.X * t, 0f, goalZ);
                    req.HasTarget = true;
                }
            }

            SimKickResult k = KickSolver.Solve(req, AttributesOf(id), ac, _cfg.Ball, _s.Rng);

            SimVec3 bp = _s.Ball.Position;
            if (throwIn) bp = new SimVec3(bp.X, 1.9f, bp.Z);
            else if (holding) bp = new SimVec3(bp.X, 1.0f, bp.Z);
            _s.Ball.Position = bp;
            _s.Ball.Velocity = k.Velocity;
            _s.Ball.Spin = k.Spin;
            _s.Ball.InNet = 0;
            _s.Ball.ClearKnuckle();
            if (k.Knuckle)
            {
                // Per-strike phase: each knuckle shot wobbles its own way, identically in replays and predictions.
                _s.Ball.KnuckleSeed = 0.05f + 0.95f * _s.Rng.NextFloat();
                _s.Ball.KnuckleOrigin = bp;
            }
            _s.Ball.Grounded = bp.Y <= r + 1e-3f && k.Velocity.Y <= 0.01f;

            _s.Owner = -1;
            _s.KeeperHolding = false;
            _s.LastTouchPlayer = id;
            _s.LastTouchTeam = p.Team;
            _s.KickSerial++;
            p.ControlBlockedUntilTick = _s.Tick + _cfg.SecondsToTicks(ac.KickerControlCooldown);
            if (holding) ProtectKeeperRelease(p.Team, bp);

            if (taker)
            {
                _s.Phase = SimMatchPhase.InPlay;
                _s.PendingRestart = RestartType.None;
                _s.RestartTakerId = -1;
                Emit(restart == RestartType.KickOff ? MatchEventType.KickOff : MatchEventType.RestartTaken,
                    id, -1, p.Team, bp, detail: (int)restart);
            }

            _s.LastKickRestart = restart;
            _s.IndirectRestartLive = restart == RestartType.IndirectFreeKick || restart == RestartType.ThrowIn;
            _s.PenaltyKicker = restart == RestartType.Penalty ? id : -1;
            _s.PenaltyKickTick = _s.Tick;
            EvaluateOffside(id, restart);

            float speed = k.Velocity.Magnitude;
            if (PlayerIntent.IsShotAction(act) && !throwIn)
            {
                float xg = ShotQuality.Estimate(bp, sign, _cfg.Pitch, req.Pressure, header);
                _s.ShotLive = true;
                _s.Shooter = id;
                _s.PassLive = false;
                if (p.Team == SimTeam.Home) _s.HomeShots++; else _s.AwayShots++;
                Emit(MatchEventType.Shot, id, -1, p.Team, bp, speed, xg, header ? 1 : 0, act, style);
            }
            else
            {
                _s.ShotLive = false;
                _s.PassLive = true;
                _s.PassFrom = id;
                _s.PassTarget = receiver;
                _s.PassTeam = p.Team;
                Emit(MatchEventType.Pass, id, receiver, p.Team, bp, speed, 0f, header ? 1 : 0, act, style);
            }
        }

        /// <summary>Explicit valid teammate target, else the best teammate inside the assist cone, else -1.</summary>
        private int ChoosePassTarget(int id, SimVec3 aim, int requested)
        {
            SimTeam team = TeamOf(id);
            if (requested >= 0 && requested < MatchSimState.PlayerCount && requested != id
                && TeamOf(requested) == team && !_s.Players[requested].SentOff)
                return requested;

            float cosCone = (float)System.Math.Cos(_cfg.Actions.PassAssistConeDeg * SimMath.Deg2Rad);
            int best = -1;
            float bestScore = float.MinValue;
            int first = FirstId(team);
            for (int j = first; j < first + MatchSetup.PlayersPerTeam; j++)
            {
                if (j == id || _s.Players[j].SentOff) continue;
                SimVec3 to = (_s.Players[j].Position - _s.Players[id].Position).Flat;
                float d = to.Magnitude;
                if (d < 2f || d > 60f) continue;
                float c = SimVec3.Dot(to / d, aim);
                if (c < cosCone) continue;
                float score = c * 2f - d / 40f;
                if (score > bestScore) { bestScore = score; best = j; }
            }

            return best;
        }

        /// <summary>
        /// Where to play the pass so it meets the receiver: his position after the ball's real flight time, from
        /// <see cref="KickSolver.PassFlightTime"/> (the same launch speed the solver will use), plus the through-ball
        /// lead into his path (also for a lobbed through ball). The meeting time t solves flightTime(|lead(t) - ball|) = t, found with a bracketed
        /// secant (regula falsi, Illinois) so it also converges when the receiver runs away as fast as the ball
        /// slows down. A receiver the ball cannot catch is led by the longest search time.
        /// </summary>
        private SimVec3 PassLeadPoint(int passer, int receiver, IntentAction act, KickStyle style, float power, bool throwIn)
        {
            SimPlayer r = _s.Players[receiver];
            SimVec3 vel = r.Velocity.Flat;
            SimVec3 extra = SimVec3.Zero;
            if (act == IntentAction.ThroughPass || (act == IntentAction.Lob && style == KickStyle.LobbedThrough))
            {
                SimVec3 dir = r.Speed > 1f ? vel.Normalized : new SimVec3(0f, 0f, AttackSign(r.Team));
                extra = dir * _cfg.Actions.ThroughLeadDistance;
            }

            const float maxT = 6f;
            float lo = 0f, glo = LeadResidual(r.Position, vel, extra, 0f, act, style, power, throwIn, out SimVec3 lead);
            if (glo <= 0f || vel.SqrMagnitude < 1e-4f) return lead; // static receiver: the first point is exact

            // Fixed-point guess, then bracket.
            float hi = SimMath.Min(maxT, glo);
            float ghi = LeadResidual(r.Position, vel, extra, hi, act, style, power, throwIn, out lead);
            while (ghi > 0f && hi < maxT)
            {
                lo = hi; glo = ghi;
                hi = SimMath.Min(maxT, hi + SimMath.Max(ghi, 0.5f));
                ghi = LeadResidual(r.Position, vel, extra, hi, act, style, power, throwIn, out lead);
            }

            if (ghi > 0f) return lead; // cannot catch him inside the search window

            int side = 0;
            for (int i = 0; i < 8 && hi - lo > 0.01f; i++)
            {
                float t = (lo * ghi - hi * glo) / (ghi - glo);
                float g = LeadResidual(r.Position, vel, extra, t, act, style, power, throwIn, out lead);
                if (SimMath.Abs(g) < 0.005f) return lead;
                if (g > 0f)
                {
                    lo = t; glo = g;
                    if (side == 1) ghi *= 0.5f;
                    side = 1;
                }
                else
                {
                    hi = t; ghi = g;
                    if (side == -1) glo *= 0.5f;
                    side = -1;
                }
            }

            LeadResidual(r.Position, vel, extra, hi, act, style, power, throwIn, out lead);
            return lead;
        }

        /// <summary>Flight time to the receiver's lead point at time t, minus t (positive: the ball is late).</summary>
        private float LeadResidual(SimVec3 pos, SimVec3 vel, SimVec3 extra, float t, IntentAction act, KickStyle style,
            float power, bool throwIn, out SimVec3 lead)
        {
            SimVec3 p = pos + vel * t + extra;
            float mx = _cfg.Pitch.HalfWidth - 1f;
            float mz = _cfg.Pitch.HalfLength - 1f;
            lead = new SimVec3(SimMath.Clamp(p.X, -mx, mx), 0f, SimMath.Clamp(p.Z, -mz, mz));
            float d = SimMath.FlatDistance(lead, _s.Ball.Position);
            return KickSolver.PassFlightTime(act, style, power, d, throwIn, _cfg.Actions, _cfg.Ball) - t;
        }

        private void StepSlide(ref SimPlayer p, float dt)
        {
            if (_s.Tick >= p.StateEndTick)
            {
                p.ActionState = SimPlayerActionState.Grounded;
                p.PendingAction = IntentAction.None;
                p.StateEndTick = _s.Tick + _cfg.SecondsToTicks(_cfg.Actions.SlideGroundTime);
                p.Velocity = SimVec3.Zero;
                return;
            }

            // Friction slows the slide linearly to about a third of its entry speed.
            float decel = _cfg.Actions.SlideSpeed / (_cfg.Actions.SlideDuration * 1.5f);
            float speed = SimMath.Max(0f, p.Speed - decel * dt);
            p.Velocity = p.PendingAim * speed;
            p.Position = new SimVec3(p.Position.X + p.Velocity.X * dt, 0f, p.Position.Z + p.Velocity.Z * dt);
            ClampToArea(ref p);
        }

        private void TrySlideContact(int id)
        {
            ref SimPlayer t = ref _s.Players[id];
            var ac = _cfg.Actions;
            float ballDist = SimMath.FlatDistance(t.Position, _s.Ball.Position);
            bool ballLow = _s.Ball.Position.Y < 0.6f;
            int carrier = _s.Owner >= 0 && TeamOf(_s.Owner) != t.Team ? _s.Owner : -1;

            if (carrier >= 0)
            {
                float carrierDist = SimMath.FlatDistance(t.Position, _s.Players[carrier].Position);
                if (ballDist <= ac.SlideTackleRange && ballLow)
                {
                    ResolveTackle(id, true);
                    t.PendingAction = IntentAction.None;
                }
                else if (carrierDist < 0.8f)
                {
                    // Took the man, not the ball.
                    t.PendingAction = IntentAction.None;
                    Emit(MatchEventType.Tackle, id, carrier, t.Team, t.Position, detail: 0, action: IntentAction.SlideTackle);
                    CommitFoul(id, carrier, true, FromBehind(id, carrier));
                }

                return;
            }

            if (_s.Owner < 0 && ballLow && ballDist <= ac.SlideTackleRange && _s.Tick >= t.ControlBlockedUntilTick)
            {
                // Loose ball: poke it along the slide.
                t.PendingAction = IntentAction.None;
                SimVec3 v = t.PendingAim * (6f + 4f * _s.Rng.NextFloat());
                SetBallLoose(v, id);
                Emit(MatchEventType.Tackle, id, -1, t.Team, t.Position, detail: 1, action: IntentAction.SlideTackle);
            }
        }

        private bool FromBehind(int tackler, int carrier)
        {
            SimVec3 rel = (_s.Players[tackler].Position - _s.Players[carrier].Position).Flat.Normalized;
            return SimVec3.Dot(_s.Players[carrier].FacingDir, rel) < -0.35f;
        }

        /// <summary>
        /// Law 12: opponents standing on the keeper cannot take a ball he releases from his hands (without this, a
        /// player at his elbow picks up every throw, punt and drop-kick at the release point).
        /// </summary>
        private void ProtectKeeperRelease(SimTeam keeperTeam, SimVec3 releasePoint)
        {
            var ac = _cfg.Actions;
            int until = _s.Tick + _cfg.SecondsToTicks(ac.GkReleaseProtectSeconds);
            SimTeam opp = Opponent(keeperTeam);
            int first = FirstId(opp);
            for (int j = first; j < first + MatchSetup.PlayersPerTeam; j++)
            {
                ref SimPlayer o = ref _s.Players[j];
                if (o.SentOff || SimMath.FlatDistance(o.Position, releasePoint) > ac.GkReleaseProtectRadius) continue;
                if (o.ControlBlockedUntilTick < until) o.ControlBlockedUntilTick = until;
            }
        }

        /// <summary>Standing or sliding tackle resolution against the current carrier.</summary>
        private void ResolveTackle(int id, bool slide)
        {
            ref SimPlayer t = ref _s.Players[id];
            var ac = _cfg.Actions;
            IntentAction act = slide ? IntentAction.SlideTackle : IntentAction.Tackle;
            int carrier = _s.Owner >= 0 && TeamOf(_s.Owner) != t.Team && !_s.KeeperHolding ? _s.Owner : -1;
            float ballDist = SimMath.FlatDistance(t.Position, _s.Ball.Position);
            float range = slide ? ac.SlideTackleRange : ac.StandingTackleRange;

            if (carrier < 0 || ballDist > range || SimMath.FlatDistance(t.Position, _s.Players[carrier].Position) > range + 0.4f)
            {
                if (!slide)
                {
                    t.ActionState = SimPlayerActionState.Recovery;
                    t.StateEndTick = _s.Tick + _cfg.SecondsToTicks(ac.TackleMissStagger);
                }

                Emit(MatchEventType.Tackle, id, carrier, t.Team, t.Position, detail: 0, action: act);
                return;
            }

            SimPlayerAttributes ta = AttributesOf(id);
            SimPlayerAttributes ca = AttributesOf(carrier);
            ref SimPlayer c = ref _s.Players[carrier];
            bool behind = FromBehind(id, carrier);
            float skill = slide ? ta.SlidingTackle : ta.StandingTackle;
            float z = (skill - (0.6f * ca.Dribbling + 0.4f * ca.BallControl)) / ac.TackleSkillScale + 0.3f;
            if (!slide && t.Jockeying) z += ac.JockeyTackleBonus;
            if (c.Shielding) z -= ac.ShieldTackleMalus * SimPlayerAttributes.N(ca.Strength) * 1.5f;
            if (behind) z -= 0.3f;
            z -= 0.8f * (ballDist / range);
            if (slide) z += 0.2f;
            float pWin = SimMath.Logistic(z);
            bool won = _s.Rng.NextFloat() < pWin;
            float aggression = 0.1f * SimPlayerAttributes.N(ta.Aggression);
            float baseFoul = slide ? ac.FoulBaseSlide : ac.FoulBaseStanding;

            if (won)
            {
                float pFoul = baseFoul * 0.5f + (behind ? ac.FoulFromBehind * 0.5f : 0f);
                if (_s.Rng.NextFloat() < pFoul)
                {
                    Emit(MatchEventType.Tackle, id, carrier, t.Team, t.Position, detail: 1, action: act);
                    CommitFoul(id, carrier, slide, behind);
                    return;
                }

                c.ActionState = SimPlayerActionState.Recovery;
                c.StateEndTick = _s.Tick + _cfg.SecondsToTicks(ac.TackledStagger);
                c.ControlBlockedUntilTick = _s.Tick + _cfg.SecondsToTicks(ac.LostTackleControlCooldown);
                Emit(MatchEventType.Tackle, id, carrier, t.Team, t.Position, detail: 1, action: act);

                bool keep = !slide && _s.Rng.NextFloat() < 0.45f + 0.4f * SimPlayerAttributes.N(ta.StandingTackle);
                if (keep)
                {
                    _s.Ball.Position = new SimVec3(t.Position.X, _cfg.Ball.Radius, t.Position.Z) + t.FacingDir * 0.4f;
                    _s.Ball.Velocity = t.Velocity.Flat;
                    _s.Ball.ClearKnuckle();
                    _s.Ball.Spin = BallIntegrator.RollingSpin(_s.Ball.Velocity, _cfg.Ball.Radius, 0f);
                    _s.Ball.Grounded = true;
                    GainPossession(id, false);
                }
                else
                {
                    SimVec3 dir = slide ? t.PendingAim : SimMath.RotateY(t.FacingDir, _s.Rng.Range(-1.2f, 1.2f));
                    SetBallLoose(dir * (3f + 4f * _s.Rng.NextFloat()), id);
                }

                return;
            }

            float pMissFoul = baseFoul + ac.FoulOnMissedBall * 0.5f + (behind ? ac.FoulFromBehind : 0f) + aggression;
            if (_s.Rng.NextFloat() < pMissFoul)
            {
                Emit(MatchEventType.Tackle, id, carrier, t.Team, t.Position, detail: 0, action: act);
                CommitFoul(id, carrier, slide, behind);
                return;
            }

            if (!slide)
            {
                t.ActionState = SimPlayerActionState.Recovery;
                t.StateEndTick = _s.Tick + _cfg.SecondsToTicks(ac.TackleMissStagger);
            }

            Emit(MatchEventType.Tackle, id, carrier, t.Team, t.Position, detail: 0, action: act);
        }

        private void SetBallLoose(SimVec3 flatVelocity, int toucher)
        {
            _s.Ball.Position = new SimVec3(_s.Ball.Position.X, _cfg.Ball.Radius, _s.Ball.Position.Z);
            _s.Ball.Velocity = flatVelocity.Flat;
            _s.Ball.ClearKnuckle();
            _s.Ball.Spin = BallIntegrator.RollingSpin(_s.Ball.Velocity, _cfg.Ball.Radius, 0f);
            _s.Ball.Grounded = true;
            _s.Owner = -1;
            _s.KeeperHolding = false;
            _s.PassLive = false;
            _s.ShotLive = false;
            _s.LastTouchPlayer = toucher;
            _s.LastTouchTeam = TeamOf(toucher);
            _s.Players[toucher].ControlBlockedUntilTick = _s.Tick + _cfg.SecondsToTicks(0.15f);
            NoteTouch(toucher);
            ClearOffside();
        }

        /// <summary>
        /// Bookkeeping for any touch other than a kick: an indirect restart has now been touched, and a penalty in
        /// progress ends unless the toucher is the defending goalkeeper (a parry keeps it alive: it can still go in).
        /// </summary>
        private void NoteTouch(int id)
        {
            _s.IndirectRestartLive = false;
            if (_s.PenaltyKicker >= 0 && id != KeeperId(Opponent(TeamOf(_s.PenaltyKicker)))) _s.PenaltyKicker = -1;
        }

        /// <summary>
        /// Keeps players at least two body radii apart (one deterministic pass in id order). A restart taker stands
        /// still at the ball, so the other player takes the whole correction. Everyone is then re-clamped to the
        /// playing area plus the run-off margin (<see cref="ClampToArea"/>).
        /// </summary>
        private void SeparatePlayers()
        {
            float minD = 2f * _cfg.Actions.PlayerSeparationRadius;
            float minD2 = minD * minD;
            int taker = _s.Phase == SimMatchPhase.AwaitingRestart ? _s.RestartTakerId : -1;
            for (int i = 0; i < MatchSimState.PlayerCount; i++)
            {
                if (_s.Players[i].SentOff) continue;
                for (int j = i + 1; j < MatchSimState.PlayerCount; j++)
                {
                    if (_s.Players[j].SentOff) continue;
                    SimVec3 d = (_s.Players[j].Position - _s.Players[i].Position).Flat;
                    float d2 = d.SqrMagnitude;
                    if (d2 >= minD2) continue;
                    float dist = SimMath.Sqrt(d2);
                    // Coincident players: split along a fixed, team-dependent axis.
                    SimVec3 n = dist > 1e-4f ? d / dist : new SimVec3(i < j ? 1f : -1f, 0f, 0f);
                    float push = minD - dist;
                    float wi = j == taker ? 1f : (i == taker ? 0f : 0.5f);
                    _s.Players[i].Position -= n * (push * wi);
                    _s.Players[j].Position += n * (push * (1f - wi));
                }
            }

            // A push must not carry anyone past the run-off margin around the pitch.
            for (int i = 0; i < MatchSimState.PlayerCount; i++)
                if (!_s.Players[i].SentOff) ClampToArea(ref _s.Players[i]);
        }
    }
}
