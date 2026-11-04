namespace OpenFifa.Core
{
    /// <summary>Ball stepping, dribble touches, gaining possession, first touch and goalkeeper saves.</summary>
    public sealed partial class MatchSim
    {
        private void StepBall()
        {
            if (_s.Phase == SimMatchPhase.AwaitingRestart && _s.Owner >= 0 && _s.Ball.Velocity.SqrMagnitude == 0f) return;

            if (_s.KeeperHolding && _s.Owner >= 0)
            {
                SimPlayer k = _s.Players[_s.Owner];
                _s.Ball.Position = new SimVec3(k.Position.X, 1.0f, k.Position.Z) + k.FacingDir * 0.3f;
                _s.Ball.Velocity = k.Velocity;
                _s.Ball.Spin = SimVec3.Zero;
                _s.Ball.Grounded = false;
                if (!InOwnPenaltyArea(k.Team, k.Position))
                {
                    // Carried out of the box: the keeper must drop the ball to his feet.
                    _s.KeeperHolding = false;
                    _s.Ball.Position = new SimVec3(_s.Ball.Position.X, _cfg.Ball.Radius, _s.Ball.Position.Z);
                    _s.Ball.Velocity = k.Velocity.Flat;
                    _s.Ball.Grounded = true;
                }

                return;
            }

            float bdt = _cfg.BallDt;
            for (int sub = 0; sub < _cfg.BallSubsteps; sub++)
            {
                BallStepFlags f = BallIntegrator.Step(ref _s.Ball, _cfg.Ball, _cfg.Pitch, bdt);
                if ((f & BallStepFlags.HitPost) != 0)
                    Emit(MatchEventType.PostHit, _s.LastTouchPlayer, -1, _s.LastTouchTeam, _s.Ball.Position, _s.Ball.Velocity.Magnitude);
                if ((f & BallStepFlags.HitCrossbar) != 0)
                    Emit(MatchEventType.CrossbarHit, _s.LastTouchPlayer, -1, _s.LastTouchTeam, _s.Ball.Position, _s.Ball.Velocity.Magnitude);
                if (_s.Ball.InNet != 0) break;
                if (_s.Phase == SimMatchPhase.InPlay && _s.Owner < 0) TrySaves();
            }
        }

        private bool InOwnPenaltyArea(SimTeam team, SimVec3 pos)
        {
            float depthCoord = -AttackCoord(team, pos); // distance toward own goal from halfway
            return SimMath.Abs(pos.X) <= _cfg.Pitch.PenaltyAreaWidth * 0.5f
                   && depthCoord >= _cfg.Pitch.HalfLength - _cfg.Pitch.PenaltyAreaDepth
                   && depthCoord <= _cfg.Pitch.HalfLength + 0.5f;
        }

        /// <summary>Keepers get one save attempt per kick when the ball comes within diving reach heading at goal.</summary>
        private void TrySaves()
        {
            for (int t = 0; t < 2; t++)
            {
                SimTeam team = t == 0 ? SimTeam.Home : SimTeam.Away;
                int serial = t == 0 ? _s.HomeSaveSerial : _s.AwaySaveSerial;
                if (serial == _s.KickSerial || _s.LastTouchTeam == team) continue;
                int gk = KeeperId(team);
                ref SimPlayer k = ref _s.Players[gk];
                if (k.SentOff || k.ActionState == SimPlayerActionState.Grounded) continue;
                SimPlayerAttributes ka = AttributesOf(gk);
                if (!ka.IsGoalkeeper || !InOwnPenaltyArea(team, k.Position)) continue;

                SimBall b = _s.Ball;
                int goalSign = -AttackSign(team);
                float speed = b.Velocity.Magnitude;
                if (b.Velocity.Z * goalSign < 3f) continue;
                // Must be heading roughly on target.
                float tLine = (goalSign * _cfg.Pitch.HalfLength - b.Position.Z) / b.Velocity.Z;
                if (tLine < 0f) continue;
                float xAtLine = b.Position.X + b.Velocity.X * tLine;
                if (SimMath.Abs(xAtLine) > _cfg.Pitch.GoalWidth * 0.5f + 1.5f) continue;

                float reach = _cfg.Actions.GkStandingReach + _cfg.Actions.GkDiveReachRange * SimPlayerAttributes.N(ka.GkDiving);
                if (SimMath.FlatDistance(k.Position, b.Position) > reach || b.Position.Y > _cfg.Actions.GkMaxReachHeight) continue;
                // How far the keeper must stretch: his distance from the ball's flat path, plus any height above his head.
                SimVec3 dir = b.Velocity.Flat.Normalized;
                SimVec3 rel = (k.Position - b.Position).Flat;
                float lateral = SimMath.Abs(dir.X * rel.Z - dir.Z * rel.X) + SimMath.Max(0f, b.Position.Y - 2.0f);
                if (lateral > reach) continue;

                if (t == 0) _s.HomeSaveSerial = _s.KickSerial; else _s.AwaySaveSerial = _s.KickSerial;
                float p = 0.35f + 0.6f * SimPlayerAttributes.N(ka.GkReflexes)
                          - SimMath.Max(0f, speed - 15f) * 0.02f - (lateral / reach) * 0.35f;
                if (lateral <= _cfg.Actions.GkStandingReach) p += 0.2f;
                p = SimMath.Clamp(p, 0.03f, 0.97f);
                if (_s.Rng.NextFloat() >= p) continue;

                int shooter = _s.LastTouchPlayer;
                float pCatch = SimPlayerAttributes.N(ka.GkHandling) * 0.95f - SimMath.Max(0f, speed - 14f) * 0.035f
                               - (lateral > _cfg.Actions.GkStandingReach ? 0.25f : 0f);
                bool caught = _s.Rng.NextFloat() < pCatch;
                Emit(MatchEventType.Save, gk, shooter, team, b.Position, speed, 0f, caught ? 1 : 0);
                _s.ShotLive = false;
                _s.PassLive = false;
                _s.LastTouchPlayer = gk;
                _s.LastTouchTeam = team;
                // A save is not a deliberate play: the offside mask stays (Law 11).
                if (caught)
                {
                    _s.Ball.Velocity = SimVec3.Zero;
                    _s.Ball.Spin = SimVec3.Zero;
                    SetOwner(gk);
                    _s.KeeperHolding = true;
                    _s.Ball.Position = new SimVec3(k.Position.X, 1.0f, k.Position.Z);
                    _s.Ball.Grounded = false;
                }
                else
                {
                    float side = b.Position.X >= k.Position.X ? 1f : -1f;
                    float vz = -b.Velocity.Z * _cfg.Actions.GkParrySpeedFactor;
                    float vx = b.Velocity.X * 0.4f + side * (2f + 4f * _s.Rng.NextFloat());
                    _s.Ball.Velocity = new SimVec3(vx, 1f + speed * 0.12f, vz);
                    _s.Ball.Spin = SimVec3.Zero;
                    _s.Ball.Grounded = false;
                    _s.KickSerial++;
                    if (t == 0) _s.HomeSaveSerial = _s.KickSerial; else _s.AwaySaveSerial = _s.KickSerial;
                }

                return;
            }
        }

        private void UpdateDribble()
        {
            int id = _s.Owner;
            if (id < 0 || _s.KeeperHolding) return;
            ref SimPlayer p = ref _s.Players[id];
            var ac = _cfg.Actions;
            float d = SimMath.FlatDistance(p.Position, _s.Ball.Position);
            if (d > ac.LooseRadius || p.SentOff)
            {
                _s.Owner = -1;
                return;
            }

            if (!p.IsFree && p.ActionState != SimPlayerActionState.Recovery) return;
            if (_s.Ball.Position.Y > 0.5f) return;

            SimVec3 dir = p.Speed > 0.5f ? p.Velocity.Flat.Normalized : p.FacingDir;
            float dirYaw = SimMath.Yaw(dir);
            bool due = _s.Tick >= p.NextTouchTick;
            bool sharpTurn = SimMath.Abs(SimMath.DeltaAngle(p.LastTouchYaw, dirYaw)) > ac.RetouchTurnDeg * SimMath.Deg2Rad
                             && _s.Tick >= p.LastTouchTick + _cfg.SecondsToTicks(ac.MinTouchInterval);
            if (!due && !sharpTurn) return;
            if (d > ac.ControlRadius * 1.3f) return; // must catch up with the ball first

            SimPlayerAttributes a = AttributesOf(id);
            float drib = SimPlayerAttributes.N(a.Dribbling);
            bool sprint = p.Sprinting;
            float interval = SimMath.Lerp(ac.TouchIntervalSlow, ac.TouchIntervalFast, drib) * (sprint ? ac.SprintTouchIntervalFactor : 1f);
            float lead = sprint
                ? ac.SprintTouchLead + ac.SprintTouchLeadLowSkill * (1f - drib)
                : ac.CloseTouchLead + 0.25f * (1f - SimPlayerAttributes.N(a.BallControl));

            if (p.Shielding)
            {
                int opp = NearestOpponent(id);
                if (opp >= 0)
                {
                    SimVec3 away = (p.Position - _s.Players[opp].Position).Flat.Normalized;
                    if (away.SqrMagnitude > 0.5f) dir = away;
                }

                lead = 0.4f;
            }

            SimVec3 target = p.Position + p.Velocity.Flat * interval + dir * lead;
            SimVec3 disp = (target - _s.Ball.Position).Flat;
            float decel = _cfg.Ball.RollingResistance * _cfg.Ball.Gravity;
            SimVec3 v = disp / interval + disp.Normalized * (0.5f * decel * interval);
            // Attachment cap: the ball can never be pushed faster than the player can follow (no magnet dribbling).
            v = SimVec3.ClampMagnitude(v, p.Speed + ac.MaxTouchSpeedOverPlayer);

            float errSigma = (1f - drib) * 0.08f * (sprint ? 2f : 1f);
            float err = _s.Rng.NextGaussian() * errSigma;
            v = SimMath.RotateY(v, err);

            _s.Ball.Position = new SimVec3(_s.Ball.Position.X, _cfg.Ball.Radius, _s.Ball.Position.Z);
            _s.Ball.Velocity = v;
            _s.Ball.Spin = BallIntegrator.RollingSpin(v, _cfg.Ball.Radius, 0f);
            _s.Ball.Grounded = true;
            _s.LastTouchPlayer = id;
            _s.LastTouchTeam = p.Team;
            p.NextTouchTick = _s.Tick + SimMath.Clamp(_cfg.SecondsToTicks(interval), 1, 10000);
            p.LastTouchYaw = dirYaw;
            p.LastTouchTick = _s.Tick;
            float quality = SimMath.Clamp01(1f - SimMath.Abs(err) * 6f - (sprint ? 0.25f * (1f - drib) : 0f));
            Emit(MatchEventType.DribbleTouch, id, -1, p.Team, _s.Ball.Position, v.Magnitude, quality);
        }

        private int NearestOpponent(int id)
        {
            SimTeam opp = Opponent(TeamOf(id));
            int best = -1;
            float bestD = float.MaxValue;
            int first = FirstId(opp);
            for (int j = first; j < first + MatchSetup.PlayersPerTeam; j++)
            {
                if (_s.Players[j].SentOff) continue;
                float d = SimMath.FlatDistance(_s.Players[j].Position, _s.Players[id].Position);
                if (d < bestD) { bestD = d; best = j; }
            }

            return best;
        }

        /// <summary>Loose-ball pickup. The owner keeps priority while the ball is within his control radius.</summary>
        private void UpdatePossession()
        {
            var ac = _cfg.Actions;
            if (_s.KeeperHolding) return;
            float ownerDist = float.MaxValue;
            if (_s.Owner >= 0)
            {
                ownerDist = SimMath.FlatDistance(_s.Players[_s.Owner].Position, _s.Ball.Position);
                if (ownerDist <= ac.ControlRadius) return;
            }

            float h = _s.Ball.Position.Y;
            int best = -1;
            float bestD = float.MaxValue;
            for (int i = 0; i < MatchSimState.PlayerCount; i++)
            {
                ref SimPlayer p = ref _s.Players[i];
                if (p.SentOff || i == _s.Owner || _s.Tick < p.ControlBlockedUntilTick) continue;
                if (p.ActionState == SimPlayerActionState.Grounded || p.ActionState == SimPlayerActionState.Sliding) continue;
                float d = SimMath.FlatDistance(p.Position, _s.Ball.Position);
                bool keeperHands = AttributesOf(i).IsGoalkeeper && InOwnPenaltyArea(p.Team, p.Position);
                float radius = keeperHands ? ac.ControlRadius * 1.5f : ac.ControlRadius;
                float maxH = keeperHands ? ac.GkMaxReachHeight : ac.ControlMaxHeight;
                if (d > radius || h > maxH || d >= ownerDist) continue;
                if (d < bestD) { bestD = d; best = i; }
            }

            if (best >= 0) GainPossession(best, true);
        }

        private void SetOwner(int id)
        {
            SimTeam team = TeamOf(id);
            if (_s.PossessionTeam != team)
            {
                _s.AssistCandidate = -1;
                Emit(MatchEventType.PossessionChange, id, _s.Owner, team, _s.Players[id].Position);
            }

            _s.Owner = id;
            _s.PossessionTeam = team;
            _s.LastTouchPlayer = id;
            _s.LastTouchTeam = team;
        }

        private void GainPossession(int id, bool firstTouch)
        {
            if ((_s.OffsideMask & (1 << id)) != 0 && _s.Phase == SimMatchPhase.InPlay)
            {
                CallOffside(id);
                return;
            }

            ClearOffside();
            ref SimPlayer p = ref _s.Players[id];
            SimTeam team = p.Team;
            SimPlayerAttributes a = AttributesOf(id);
            var ac = _cfg.Actions;

            if (_s.PassLive)
            {
                if (_s.PassTeam == team && _s.PassFrom != id)
                {
                    Emit(MatchEventType.PassCompleted, id, _s.PassFrom, team, p.Position);
                    _s.AssistCandidate = _s.PassFrom;
                }
                else if (_s.PassTeam != team)
                {
                    Emit(MatchEventType.Interception, id, _s.PassFrom, team, p.Position);
                }
            }

            bool backPass = _s.PassLive && _s.PassTeam == team && _s.PassFrom != id;
            _s.PassLive = false;
            _s.ShotLive = false;
            SetOwner(id);

            bool keeperHands = a.IsGoalkeeper && InOwnPenaltyArea(team, p.Position) && !backPass;
            if (keeperHands)
            {
                _s.KeeperHolding = true;
                _s.Ball.Velocity = SimVec3.Zero;
                _s.Ball.Spin = SimVec3.Zero;
                Emit(MatchEventType.FirstTouch, id, -1, team, _s.Ball.Position, 0f, 1f);
                return;
            }

            if (!firstTouch) return;

            // First touch: kill the incoming ball; error grows with relative speed beyond what control can absorb.
            SimVec3 rel = _s.Ball.Velocity - p.Velocity;
            float relSpeed = rel.Magnitude;
            float control = SimPlayerAttributes.N(a.BallControl);
            float clean = ac.FirstTouchCleanSpeedMin + ac.FirstTouchCleanSpeedRange * control;
            float pressure = PressureOn(id);
            float err = (SimMath.Max(0f, relSpeed - clean) * ac.FirstTouchErrorPerSpeed
                         + (1f - control) * 0.3f * _s.Rng.NextFloat()) * (1f + 0.5f * pressure);
            float quality = SimMath.Clamp01(1f - err / 2.5f);

            SimVec3 ahead = p.Speed > 0.5f ? p.Velocity.Flat.Normalized : p.FacingDir;
            SimVec3 randomDir = SimMath.FromYaw(_s.Rng.Range(-SimMath.Pi, SimMath.Pi));
            SimVec3 v = p.Velocity.Flat + ahead * 0.6f + randomDir * (err * 1.5f);
            _s.Ball.Position = new SimVec3(_s.Ball.Position.X, _cfg.Ball.Radius, _s.Ball.Position.Z);
            _s.Ball.Velocity = v;
            _s.Ball.Spin = BallIntegrator.RollingSpin(v, _cfg.Ball.Radius, 0f);
            _s.Ball.Grounded = true;
            p.NextTouchTick = _s.Tick + _cfg.SecondsToTicks(0.25f + (1f - quality) * 0.3f);
            p.LastTouchYaw = SimMath.Yaw(ahead);
            p.LastTouchTick = _s.Tick;
            Emit(MatchEventType.FirstTouch, id, -1, team, _s.Ball.Position, relSpeed, quality);
        }
    }
}
