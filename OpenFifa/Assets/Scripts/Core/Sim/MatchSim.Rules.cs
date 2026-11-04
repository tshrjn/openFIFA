namespace OpenFifa.Core
{
    /// <summary>Laws of the game: restarts, out of play, goals, fouls and cards, offside, halves.</summary>
    public sealed partial class MatchSim
    {
        private void BeginKickOff(SimTeam team)
        {
            for (int i = 0; i < MatchSimState.PlayerCount; i++)
            {
                ref SimPlayer p = ref _s.Players[i];
                if (p.SentOff) continue;
                SimTeam t = TeamOf(i);
                int slot = i - FirstId(t);
                SimVec3 f = (t == SimTeam.Home ? _setup.HomeFormation : _setup.AwayFormation)[slot];
                int sign = AttackSign(t);
                p.Position = new SimVec3(f.X * sign, 0f, f.Z * sign);
                p.Velocity = SimVec3.Zero;
                p.Facing = sign > 0 ? 0f : SimMath.Pi;
                p.ActionState = SimPlayerActionState.Free;
                p.PendingAction = IntentAction.None;
            }

            _s.PendingRestart = RestartType.KickOff;
            _s.RestartTeam = team;
            _s.RestartSpot = SimVec3.Zero;
            SetupRestart();
        }

        /// <summary>Awards a restart: emits the award event and starts the dead-ball pause.</summary>
        private void AwardRestart(RestartType type, SimTeam team, SimVec3 spot, float delay)
        {
            float mx = _cfg.Pitch.HalfWidth;
            float mz = _cfg.Pitch.HalfLength;
            spot = new SimVec3(SimMath.Clamp(spot.X, -mx, mx), 0f, SimMath.Clamp(spot.Z, -mz, mz));
            _s.Phase = SimMatchPhase.Stoppage;
            _s.PhaseEndTick = _s.Tick + _cfg.SecondsToTicks(delay);
            _s.PendingRestart = type;
            _s.RestartTeam = team;
            _s.RestartSpot = spot;
            _s.Owner = -1;
            _s.KeeperHolding = false;
            _s.PassLive = false;
            _s.ShotLive = false;
            _s.PossessionTeam = SimTeam.None;
            _s.PenaltyKicker = -1;
            _s.IndirectRestartLive = false;
            ClearOffside();

            MatchEventType ev;
            switch (type)
            {
                case RestartType.ThrowIn: ev = MatchEventType.ThrowIn; break;
                case RestartType.GoalKick: ev = MatchEventType.GoalKick; break;
                case RestartType.Corner: ev = MatchEventType.Corner; break;
                case RestartType.Penalty: ev = MatchEventType.Penalty; break;
                case RestartType.DirectFreeKick:
                case RestartType.IndirectFreeKick: ev = MatchEventType.FreeKick; break;
                default: return;
            }

            Emit(ev, -1, -1, team, spot, detail: (int)type);
        }

        /// <summary>Places the ball and the taker; the restart is taken when the taker's kick reaches contact.</summary>
        private void SetupRestart()
        {
            RestartType type = _s.PendingRestart;
            SimTeam team = _s.RestartTeam;
            if (type == RestartType.KickOff && _s.Phase == SimMatchPhase.Stoppage)
            {
                // After a goal: everyone back to shape (BeginKickOff calls back into here).
                _s.Phase = SimMatchPhase.AwaitingRestart;
                BeginKickOff(team);
                return;
            }

            _s.Phase = SimMatchPhase.AwaitingRestart;
            _s.PhaseEndTick = _s.Tick + _cfg.SecondsToTicks(_cfg.RestartAutoTakeSeconds);
            SimVec3 spot = _s.RestartSpot;
            _s.Ball = SimBall.AtRest(spot, _cfg.Ball.Radius);

            int sign = AttackSign(team);
            int taker = ChooseTaker(type, team, spot);
            _s.RestartTakerId = taker;
            ref SimPlayer tp = ref _s.Players[taker];
            SimVec3 forward = new SimVec3(0f, 0f, sign);
            SimVec3 facing = forward;
            if (type == RestartType.ThrowIn) facing = new SimVec3(-SimMath.Sign(spot.X), 0f, 0f);
            else if (type == RestartType.Corner) facing = (new SimVec3(0f, 0f, sign * (_cfg.Pitch.HalfLength - 11f)) - spot).Flat.Normalized;
            tp.Position = (spot - facing * 0.5f).Flat;
            if (type == RestartType.ThrowIn) tp.Position = new SimVec3(spot.X + SimMath.Sign(spot.X) * 0.3f, 0f, spot.Z);
            tp.Velocity = SimVec3.Zero;
            tp.Facing = SimMath.Yaw(facing);
            tp.ActionState = SimPlayerActionState.Free;
            tp.PendingAction = IntentAction.None;
            _s.Owner = taker;
            _s.KeeperHolding = false;
            _s.PossessionTeam = team;
            _s.LastTouchPlayer = taker;
            _s.LastTouchTeam = team;

            if (type == RestartType.Penalty)
            {
                SimTeam def = Opponent(team);
                int gk = KeeperId(def);
                if (!_s.Players[gk].SentOff)
                {
                    _s.Players[gk].Position = new SimVec3(0f, 0f, sign * (_cfg.Pitch.HalfLength - 0.3f));
                    _s.Players[gk].Velocity = SimVec3.Zero;
                }
            }

            EnforceRestartPositions();
        }

        private int ChooseTaker(RestartType type, SimTeam team, SimVec3 spot)
        {
            int first = FirstId(team);
            if (type == RestartType.GoalKick && !_s.Players[first].SentOff) return first;
            if (type == RestartType.KickOff)
            {
                int slot = first + SimMath.Clamp(_setup.KickOffTakerSlot, 1, MatchSetup.PlayersPerTeam - 1);
                if (!_s.Players[slot].SentOff) return slot;
            }

            int best = -1;
            float bestD = float.MaxValue;
            for (int j = first; j < first + MatchSetup.PlayersPerTeam; j++)
            {
                if (_s.Players[j].SentOff) continue;
                if (j == first && type != RestartType.DirectFreeKick && type != RestartType.IndirectFreeKick) continue;
                float d = SimMath.FlatDistance(_s.Players[j].Position, spot);
                if (d < bestD) { bestD = d; best = j; }
            }

            return best >= 0 ? best : first;
        }

        /// <summary>Holds opponents 9.15 m off the ball (2 m at throw-ins), and keeps kick-off / penalty shapes.</summary>
        private void EnforceRestartPositions()
        {
            RestartType type = _s.PendingRestart;
            SimTeam team = _s.RestartTeam;
            SimVec3 ball = _s.Ball.Position.Flat;
            float radius = type == RestartType.ThrowIn ? 2f : _cfg.RestartDistance;
            int sign = AttackSign(team);

            for (int i = 0; i < MatchSimState.PlayerCount; i++)
            {
                ref SimPlayer p = ref _s.Players[i];
                if (p.SentOff || i == _s.RestartTakerId) continue;
                SimVec3 pos = p.Position;

                if (type == RestartType.KickOff)
                {
                    // Own half only.
                    int s = AttackSign(p.Team);
                    if (pos.Z * s > -0.5f) pos = new SimVec3(pos.X, 0f, -0.5f * s);
                }

                if (type == RestartType.Penalty && i != KeeperId(Opponent(team)))
                {
                    float boxLine = _cfg.Pitch.HalfLength - _cfg.Pitch.PenaltyAreaDepth - 1f;
                    if (pos.Z * sign > boxLine) pos = new SimVec3(pos.X, 0f, boxLine * sign);
                }

                bool opponent = p.Team != team;
                if (opponent || type == RestartType.Penalty)
                {
                    SimVec3 rel = (pos - ball).Flat;
                    float d = rel.Magnitude;
                    // Inside the distance, or nudged off the pitch next to it (e.g. by player separation): put back
                    // on the pitch at least the distance away.
                    if (d < radius || (d < radius + 1f && !OnPitch(pos)))
                    {
                        SimVec3 dir = d > 1e-3f ? rel / d : new SimVec3(0f, 0f, -sign);
                        pos = PlaceAtDistance(ball, dir, SimMath.Max(d, radius));
                    }
                }

                if (!pos.Equals(p.Position))
                {
                    p.Position = pos;
                    p.Velocity = SimVec3.Zero;
                }
            }

            // Auto-take a stalled restart with a short pass so headless bot matches never deadlock.
            int taker = _s.RestartTakerId;
            if (taker >= 0 && _s.Tick >= _s.PhaseEndTick && _s.Players[taker].IsFree)
            {
                int mate = NearestTeammate(taker);
                SimVec3 aim = mate >= 0 ? (_s.Players[mate].Position - _s.Ball.Position).Flat.Normalized
                    : new SimVec3(0f, 0f, sign);
                BeginWindUp(ref _s.Players[taker], IntentAction.Pass, 0.4f, aim, mate, _cfg.Actions.PassWindup);
            }
        }

        /// <summary>
        /// A point <paramref name="radius"/> from the ball, as close in direction to <paramref name="dir"/> as the pitch
        /// allows: near a goal line or touchline the direction is rotated in 15 degree steps until the point is on the
        /// pitch (so a wall is never placed off the field), else clamped onto it.
        /// </summary>
        private SimVec3 PlaceAtDistance(SimVec3 ball, SimVec3 dir, float radius)
        {
            SimVec3 cand = ball + dir * radius;
            if (OnPitch(cand)) return cand;
            for (int k = 1; k <= 12; k++)
            {
                float a = k * 15f * SimMath.Deg2Rad;
                cand = ball + SimMath.RotateY(dir, a) * radius;
                if (OnPitch(cand)) return cand;
                cand = ball + SimMath.RotateY(dir, -a) * radius;
                if (OnPitch(cand)) return cand;
            }

            cand = ball + dir * radius;
            return new SimVec3(SimMath.Clamp(cand.X, -_cfg.Pitch.HalfWidth, _cfg.Pitch.HalfWidth), 0f,
                SimMath.Clamp(cand.Z, -_cfg.Pitch.HalfLength, _cfg.Pitch.HalfLength));
        }

        private bool OnPitch(SimVec3 p) =>
            SimMath.Abs(p.X) <= _cfg.Pitch.HalfWidth && SimMath.Abs(p.Z) <= _cfg.Pitch.HalfLength;

        private int NearestTeammate(int id)
        {
            SimTeam team = TeamOf(id);
            int first = FirstId(team);
            int best = -1;
            float bestD = float.MaxValue;
            for (int j = first; j < first + MatchSetup.PlayersPerTeam; j++)
            {
                if (j == id || _s.Players[j].SentOff) continue;
                float d = SimMath.FlatDistance(_s.Players[j].Position, _s.Players[id].Position);
                if (d < bestD) { bestD = d; best = j; }
            }

            return best;
        }

        private void CheckBallOutOfPlay()
        {
            SimBall b = _s.Ball;
            float r = _cfg.Ball.Radius;
            var pitch = _cfg.Pitch;

            SimTeam last = _s.LastTouchTeam == SimTeam.None ? SimTeam.Home : _s.LastTouchTeam;
            if (b.InNet != 0)
            {
                // Law 13 / 15: an indirect free kick or a throw-in cannot score directly.
                if (_s.IndirectRestartLive)
                {
                    Emit(MatchEventType.OutOfPlay, _s.LastTouchPlayer, -1, last,
                        new SimVec3(b.Position.X, 0f, b.InNet * pitch.HalfLength), b.Velocity.Magnitude);
                    AwardGoalLineRestart(b.InNet, b.Position.X, last);
                    return;
                }

                ScoreGoal(b.InNet);
                return;
            }

            if (SimMath.Abs(b.Position.X) > pitch.HalfWidth + r)
            {
                float sx = SimMath.Sign(b.Position.X);
                SimVec3 exit = new SimVec3(sx * pitch.HalfWidth, 0f, b.Position.Z);
                Emit(MatchEventType.OutOfPlay, _s.LastTouchPlayer, -1, last, exit, b.Velocity.Magnitude);
                AwardRestart(RestartType.ThrowIn, Opponent(last), exit, _cfg.OutOfPlayDelay);
                return;
            }

            if (SimMath.Abs(b.Position.Z) > pitch.HalfLength + r)
            {
                int end = b.Position.Z > 0f ? 1 : -1;
                SimVec3 exit = new SimVec3(SimMath.Clamp(b.Position.X, -pitch.HalfWidth, pitch.HalfWidth), 0f, end * pitch.HalfLength);
                Emit(MatchEventType.OutOfPlay, _s.LastTouchPlayer, -1, last, exit, b.Velocity.Magnitude);
                AwardGoalLineRestart(end, b.Position.X, last);
            }
        }

        /// <summary>Ball over the goal line at end <paramref name="end"/> (+1/-1): corner if the defenders put it there, else goal kick.</summary>
        private void AwardGoalLineRestart(int end, float x, SimTeam last)
        {
            var pitch = _cfg.Pitch;
            SimTeam defending = AttackSign(SimTeam.Home) == end ? SimTeam.Away : SimTeam.Home;
            float sx = x >= 0f ? 1f : -1f;
            if (last == defending)
            {
                SimVec3 corner = new SimVec3(sx * (pitch.HalfWidth - 0.5f), 0f, end * (pitch.HalfLength - 0.5f));
                AwardRestart(RestartType.Corner, Opponent(defending), corner, _cfg.OutOfPlayDelay);
            }
            else
            {
                SimVec3 gk = new SimVec3(sx * 5f, 0f, end * (pitch.HalfLength - _cfg.GoalAreaDepth));
                AwardRestart(RestartType.GoalKick, defending, gk, _cfg.OutOfPlayDelay);
            }
        }

        private void ScoreGoal(int netSign)
        {
            SimTeam scoring = AttackSign(SimTeam.Home) == netSign ? SimTeam.Home : SimTeam.Away;
            if (scoring == SimTeam.Home) _s.HomeScore++; else _s.AwayScore++;
            int scorer = _s.LastTouchPlayer;
            bool ownGoal = _s.LastTouchTeam != scoring;
            // A shot that goes in off the keeper (parry) or off a defender is the shooter's goal, not an own goal.
            if (_s.ShotLive && _s.Shooter >= 0 && TeamOf(_s.Shooter) == scoring)
            {
                scorer = _s.Shooter;
                ownGoal = false;
            }

            int assist = !ownGoal && _s.AssistCandidate >= 0 && _s.AssistCandidate != scorer
                         && TeamOf(_s.AssistCandidate) == scoring ? _s.AssistCandidate : -1;
            Emit(MatchEventType.Goal, scorer, assist, scoring, _s.Ball.Position, _s.Ball.Velocity.Magnitude, 0f, ownGoal ? 1 : 0);
            AwardRestart(RestartType.KickOff, Opponent(scoring), SimVec3.Zero, _cfg.GoalDelay);
        }

        /// <summary>Foul by <paramref name="offender"/> on <paramref name="victim"/>: card decision and restart.</summary>
        private void CommitFoul(int offender, int victim, bool slide, bool fromBehind)
        {
            var ac = _cfg.Actions;
            SimTeam offTeam = TeamOf(offender);
            SimVec3 spot = _s.Players[victim].Position;
            int card = 0;
            if (DeniesGoalScoringChance(offender, victim)) card = 2;
            else if (fromBehind && _s.Rng.NextFloat() < (slide ? ac.YellowFromBehindSlide : ac.YellowFromBehindStanding)) card = 1;

            Emit(MatchEventType.Foul, offender, victim, offTeam, spot, detail: card,
                action: slide ? IntentAction.SlideTackle : IntentAction.Tackle);

            ref SimPlayer o = ref _s.Players[offender];
            if (card == 1)
            {
                o.YellowCards++;
                Emit(MatchEventType.YellowCard, offender, victim, offTeam, o.Position, detail: o.YellowCards);
                if (o.YellowCards >= 2) card = 2;
            }

            if (card == 2)
            {
                Emit(MatchEventType.RedCard, offender, victim, offTeam, o.Position);
                o.SentOff = true;
                o.Velocity = SimVec3.Zero;
                o.Position = new SimVec3(SimMath.Sign(o.Position.X) * (_cfg.Pitch.HalfWidth + 3f), 0f, o.Position.Z);
                o.ActionState = SimPlayerActionState.Free;
            }

            SimTeam awarded = Opponent(offTeam);
            if (InOwnPenaltyArea(offTeam, spot))
            {
                int sign = AttackSign(awarded);
                AwardRestart(RestartType.Penalty, awarded,
                    new SimVec3(0f, 0f, sign * (_cfg.Pitch.HalfLength - _cfg.PenaltySpotDistance)), _cfg.FoulDelay);
            }
            else
            {
                AwardRestart(RestartType.DirectFreeKick, awarded, spot, _cfg.FoulDelay);
            }
        }

        /// <summary>
        /// Simplified DOGSO: the victim is within 30 m of goal, fairly central, and no defender other than the
        /// offender (keeper included) is nearer the goal line than him.
        /// </summary>
        private bool DeniesGoalScoringChance(int offender, int victim)
        {
            SimTeam att = TeamOf(victim);
            SimVec3 vp = _s.Players[victim].Position;
            float coord = AttackCoord(att, vp);
            if (coord < _cfg.Pitch.HalfLength - 30f || SimMath.Abs(vp.X) > 20f) return false;
            SimTeam def = Opponent(att);
            int first = FirstId(def);
            for (int j = first; j < first + MatchSetup.PlayersPerTeam; j++)
            {
                if (j == offender || _s.Players[j].SentOff) continue;
                if (AttackCoord(att, _s.Players[j].Position) > coord) return false;
            }

            return true;
        }

        /// <summary>Records which teammates of the kicker are in an offside position at this contact frame.</summary>
        private void EvaluateOffside(int kicker, RestartType restart)
        {
            ClearOffside();
            if (!OffsideRule.AppliesToRestart(restart)) return;
            SimTeam team = TeamOf(kicker);
            SimTeam def = Opponent(team);
            int n = 0;
            int dFirst = FirstId(def);
            for (int j = dFirst; j < dFirst + MatchSetup.PlayersPerTeam; j++)
            {
                if (_s.Players[j].SentOff) continue;
                _scratchCoords[n++] = AttackCoord(team, _s.Players[j].Position);
            }

            float line = OffsideRule.SecondLastDefender(_scratchCoords, n);
            float ballCoord = AttackCoord(team, _s.Ball.Position);
            int first = FirstId(team);
            for (int j = first; j < first + MatchSetup.PlayersPerTeam; j++)
            {
                if (j == kicker || _s.Players[j].SentOff) continue;
                float c = AttackCoord(team, _s.Players[j].Position);
                if (!OffsideRule.IsOffsidePosition(c, ballCoord, line)) continue;
                _s.OffsideMask |= 1 << j;
                _s.OffsidePositions[j] = _s.Players[j].Position;
                _s.OffsideMargins[j] = c - SimMath.Max(line, ballCoord);
            }

            _s.OffsidePasser = _s.OffsideMask != 0 ? kicker : -1;
            int sign = AttackSign(team);
            _s.OffsideDefenderLineZ = line * sign;
            _s.OffsideBallLineZ = ballCoord * sign;
        }

        private void ClearOffside()
        {
            _s.OffsideMask = 0;
            _s.OffsidePasser = -1;
        }

        private void CallOffside(int id)
        {
            SimVec3 at = _s.OffsidePositions[id];
            Emit(MatchEventType.Offside, id, _s.OffsidePasser, TeamOf(id), at, value: _s.OffsideMargins[id],
                defenderLineZ: _s.OffsideDefenderLineZ, ballLineZ: _s.OffsideBallLineZ);
            AwardRestart(RestartType.IndirectFreeKick, Opponent(TeamOf(id)), at, _cfg.FoulDelay);
        }

        private void CheckClock()
        {
            // A penalty is over once the ball stops (or, as a guard, a few seconds after the kick).
            if (_s.PenaltyKicker >= 0 && _s.Phase == SimMatchPhase.InPlay
                && (_s.Ball.Velocity.SqrMagnitude == 0f || _s.Tick > _s.PenaltyKickTick + _cfg.SecondsToTicks(6f)))
                _s.PenaltyKicker = -1;

            if (_s.Clock < _cfg.HalfDurationSeconds) return;
            if (_s.Phase == SimMatchPhase.HalfTime || _s.Phase == SimMatchPhase.FullTime) return;
            // A penalty awarded before the whistle must still be taken, and played out until its outcome.
            if (_s.PendingRestart == RestartType.Penalty || _s.PenaltyKicker >= 0) return;

            _s.Owner = -1;
            _s.KeeperHolding = false;
            _s.PassLive = false;
            _s.ShotLive = false;
            ClearOffside();
            if (_s.Half == 1)
            {
                _s.Phase = SimMatchPhase.HalfTime;
                _s.PhaseEndTick = _s.Tick + _cfg.SecondsToTicks(_cfg.HalfTimeBreakSeconds);
                Emit(MatchEventType.HalfTime, -1, -1, SimTeam.None, _s.Ball.Position, detail: _s.HomeScore * 100 + _s.AwayScore);
            }
            else
            {
                _s.Phase = SimMatchPhase.FullTime;
                Emit(MatchEventType.FullTime, -1, -1, SimTeam.None, _s.Ball.Position, detail: _s.HomeScore * 100 + _s.AwayScore);
            }
        }

        private void StartSecondHalf()
        {
            _s.Half = 2;
            _s.Clock = 0f;
            _s.PossessionTeam = SimTeam.None;
            for (int i = 0; i < MatchSimState.PlayerCount; i++) _s.Players[i].Stamina = SimMath.Clamp01(_s.Players[i].Stamina + 0.15f);
            _s.Phase = SimMatchPhase.AwaitingRestart;
            BeginKickOff(Opponent(_setup.FirstKickOff));
        }
    }
}
