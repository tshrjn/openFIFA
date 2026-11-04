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
                StepHeldBall();
                return;
            }

            float bdt = _cfg.BallDt;
            for (int sub = 0; sub < _cfg.BallSubsteps; sub++)
            {
                SimVec3 p0 = _s.Ball.Position;
                BallStepFlags f = BallIntegrator.Step(ref _s.Ball, _cfg.Ball, _cfg.Pitch, bdt);
                if ((f & BallStepFlags.HitPost) != 0)
                    Emit(MatchEventType.PostHit, _s.LastTouchPlayer, -1, _s.LastTouchTeam, _s.Ball.Position, _s.Ball.Velocity.Magnitude);
                if ((f & BallStepFlags.HitCrossbar) != 0)
                    Emit(MatchEventType.CrossbarHit, _s.LastTouchPlayer, -1, _s.LastTouchTeam, _s.Ball.Position, _s.Ball.Velocity.Magnitude);
                if (_s.Ball.InNet != 0) break;
                if (_s.Phase != SimMatchPhase.InPlay) continue;
                CollideBallWithBodies(p0);
                if (_s.Owner < 0) TrySaves();
                if (_s.KeeperHolding) break;
            }
        }

        /// <summary>
        /// The keeper has the ball in his hands. A ball caught at full stretch is gathered from the catch point to the
        /// hold point at <see cref="SimActionConfig.GkGatherSpeed"/> (plus the keeper's own speed), never teleported.
        /// </summary>
        private void StepHeldBall()
        {
            SimPlayer k = _s.Players[_s.Owner];
            float dt = _cfg.Dt;
            SimVec3 hold = new SimVec3(k.Position.X, 1.0f, k.Position.Z) + k.FacingDir * 0.3f;
            SimVec3 from = _s.Ball.Position;
            SimVec3 to = hold - from;
            float dist = to.Magnitude;
            float maxStep = (_cfg.Actions.GkGatherSpeed + k.Speed) * dt;
            SimVec3 pos = dist <= maxStep ? hold : from + to * (maxStep / dist);
            _s.Ball.Position = pos;
            _s.Ball.Velocity = dist <= maxStep ? k.Velocity : (pos - from) / dt;
            _s.Ball.ClearKnuckle();
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
        }

        private bool InOwnPenaltyArea(SimTeam team, SimVec3 pos)
        {
            float depthCoord = -AttackCoord(team, pos); // distance toward own goal from halfway
            return SimMath.Abs(pos.X) <= _cfg.Pitch.PenaltyAreaWidth * 0.5f
                   && depthCoord >= _cfg.Pitch.HalfLength - _cfg.Pitch.PenaltyAreaDepth
                   && depthCoord <= _cfg.Pitch.HalfLength + 0.5f;
        }

        /// <summary>
        /// Keepers get one save attempt per kick, resolved when the ball reaches its closest approach (it crosses the
        /// keeper's plane during the next ball substep). The ball must then be within reach along the dive: the
        /// lateral offset from the keeper perpendicular to the ball's path, plus any height above a standing reach.
        /// A catch leaves the ball at the catch point (it is gathered smoothly afterwards); a parry deflects it from
        /// there; a beaten keeper cannot also grab the ball on the same kick.
        /// </summary>
        private void TrySaves()
        {
            float bdt = _cfg.BallDt;
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
                if (!HeadingAtGoal(team, b)) continue;

                // Closest approach: wait until the ball crosses the keeper's plane within the next substep.
                SimVec3 flatV = b.Velocity.Flat;
                float flatSpeed = flatV.Magnitude;
                SimVec3 dir = flatV / flatSpeed;
                SimVec3 rel = (k.Position - b.Position).Flat;
                float along = SimVec3.Dot(rel, dir);
                if (along > flatSpeed * bdt) continue;
                if (along < -0.5f) continue; // already past him
                float tCa = SimMath.Max(0f, along) / flatSpeed;
                SimVec3 ca = b.Position + b.Velocity * tCa + new SimVec3(0f, -0.5f * _cfg.Ball.Gravity * tCa * tCa, 0f);

                // This is the keeper's one chance at this kick, whether or not it is in reach.
                if (t == 0) _s.HomeSaveSerial = _s.KickSerial; else _s.AwaySaveSerial = _s.KickSerial;
                float reach = _cfg.Actions.GkStandingReach + _cfg.Actions.GkDiveReachRange * SimPlayerAttributes.N(ka.GkDiving);
                float lateral = SimMath.Abs(dir.X * rel.Z - dir.Z * rel.X) + SimMath.Max(0f, ca.Y - 2.0f);
                if (ca.Y > _cfg.Actions.GkMaxReachHeight || lateral > reach) continue;

                float speed = b.Velocity.Magnitude;
                float p = 0.35f + 0.6f * SimPlayerAttributes.N(ka.GkReflexes)
                          - SimMath.Max(0f, speed - 15f) * 0.02f - (lateral / reach) * 0.35f;
                if (lateral <= _cfg.Actions.GkStandingReach) p += 0.2f;
                p = SimMath.Clamp(p, 0.03f, 0.97f);
                if (_s.Rng.NextFloat() >= p)
                {
                    // Beaten: he cannot claim the ball as it goes past (it may still hit his body).
                    k.ControlBlockedUntilTick = _s.Tick + _cfg.SecondsToTicks(0.3f);
                    continue;
                }

                int shooter = _s.ShotLive ? _s.Shooter : _s.LastTouchPlayer;
                float pCatch = SimPlayerAttributes.N(ka.GkHandling) * 0.95f - SimMath.Max(0f, speed - 14f) * 0.035f
                               - (lateral > _cfg.Actions.GkStandingReach ? 0.25f : 0f)
                               + SimMath.Max(0f, 12f - speed) * 0.05f; // slow balls are simply gathered
                bool caught = _s.Rng.NextFloat() < pCatch;
                Emit(MatchEventType.Save, gk, shooter, team, ca, speed, 0f, caught ? 1 : 0);
                _s.PassLive = false;
                _s.LastTouchPlayer = gk;
                _s.LastTouchTeam = team;
                _s.Ball.Position = new SimVec3(ca.X, SimMath.Max(ca.Y, _cfg.Ball.Radius), ca.Z);
                _s.Ball.ClearKnuckle();
                // A save is not a deliberate play: the offside mask stays (Law 11).
                if (caught)
                {
                    _s.ShotLive = false;
                    _s.PenaltyKicker = -1;
                    _s.IndirectRestartLive = false;
                    _s.Ball.Velocity = SimVec3.Zero;
                    _s.Ball.Spin = SimVec3.Zero;
                    SetOwner(gk);
                    _s.KeeperHolding = true;
                    _s.Ball.Grounded = false;
                }
                else
                {
                    // Parry: ShotLive stays, so a parry that still goes in is the shooter's goal.
                    NoteTouch(gk);
                    float side = ca.X >= k.Position.X ? 1f : -1f;
                    float vz = -b.Velocity.Z * _cfg.Actions.GkParrySpeedFactor;
                    float vx = b.Velocity.X * 0.4f + side * (2f + 4f * _s.Rng.NextFloat());
                    _s.Ball.Velocity = new SimVec3(vx, 1f + speed * 0.12f, vz);
                    _s.Ball.Spin = SimVec3.Zero;
                    _s.Ball.Grounded = false;
                    k.ControlBlockedUntilTick = _s.Tick + _cfg.SecondsToTicks(0.4f);
                    _s.KickSerial++;
                    if (t == 0) _s.HomeSaveSerial = _s.KickSerial; else _s.AwaySaveSerial = _s.KickSerial;
                }

                return;
            }
        }

        // ---- Read-only queries for presentation-side prediction (animation timing). They mirror the save and pickup
        // rules above exactly, so a predicted contact lands on the tick the sim resolves it. ----

        /// <summary>The keeper still has a save attempt to come for the current kick (resolved at closest approach).</summary>
        public bool IsSavePending(int keeperId) => keeperId >= 0 && keeperId < MatchSimState.PlayerCount && SavePending(keeperId);

        /// <summary>Whether <paramref name="b"/> is moving toward <paramref name="team"/>'s goal at 3 m/s or more, roughly on target.</summary>
        public bool IsBallHeadingAtGoal(SimTeam team, SimBall b) => HeadingAtGoal(team, b);

        /// <summary>Whether <paramref name="pos"/> is inside <paramref name="team"/>'s own penalty area (keeper hands allowed).</summary>
        public bool IsInOwnPenaltyArea(SimTeam team, SimVec3 pos) => InOwnPenaltyArea(team, pos);

        /// <summary>The ball is moving toward <paramref name="team"/>'s goal at 3 m/s or more, roughly on target.</summary>
        private bool HeadingAtGoal(SimTeam team, SimBall b)
        {
            int goalSign = -AttackSign(team);
            if (b.Velocity.Z * goalSign < 3f) return false;
            float tLine = (goalSign * _cfg.Pitch.HalfLength - b.Position.Z) / b.Velocity.Z;
            if (tLine < 0f) return false;
            float xAtLine = b.Position.X + b.Velocity.X * tLine;
            return SimMath.Abs(xAtLine) <= _cfg.Pitch.GoalWidth * 0.5f + 1.5f;
        }

        /// <summary>The keeper still has a save attempt to come for the current kick (resolved at closest approach).</summary>
        private bool SavePending(int id)
        {
            SimTeam team = TeamOf(id);
            if (id != KeeperId(team) || _s.LastTouchTeam == team) return false;
            int serial = team == SimTeam.Home ? _s.HomeSaveSerial : _s.AwaySaveSerial;
            return serial != _s.KickSerial && HeadingAtGoal(team, _s.Ball);
        }

        /// <summary>
        /// Ball vs body capsules for players who cannot control the ball right now (blocked after a kick or a beaten
        /// save, on the ground, sliding) or when it is above their control height (head up to
        /// <see cref="SimActionConfig.PlayerHeadHeight"/>). Players who can control it are left to
        /// <see cref="UpdatePossession"/> (first touch). Keepers' hands are the save and pickup logic, which reach
        /// higher (<see cref="SimActionConfig.GkMaxReachHeight"/>). Swept over the substep, earliest contact wins.
        /// </summary>
        private void CollideBallWithBodies(SimVec3 p0)
        {
            var ac = _cfg.Actions;
            SimVec3 p1 = _s.Ball.Position;
            float r = _cfg.Ball.Radius;
            float br = ac.PlayerBodyRadius;
            float best = 2f;
            int hitId = -1;
            SimVec3 hitA = SimVec3.Zero, hitB = SimVec3.Zero;
            float hitRadius = 0f;
            for (int i = 0; i < MatchSimState.PlayerCount; i++)
            {
                ref SimPlayer pl = ref _s.Players[i];
                if (pl.SentOff || i == _s.Owner) continue;
                bool blocked = _s.Tick < pl.ControlBlockedUntilTick;
                // The ball leaving the kicker's foot does not hit the kicker.
                if (blocked && i == _s.LastTouchPlayer) continue;
                // Coarse reject: the capsule's flat reach.
                float fx = pl.Position.X - p1.X, fz = pl.Position.Z - p1.Z;
                if (fx * fx + fz * fz > 9f) continue;

                bool down = pl.ActionState == SimPlayerActionState.Grounded || pl.ActionState == SimPlayerActionState.Sliding;
                SimVec3 a, b;
                float rad;
                if (down)
                {
                    // Lying or sliding: a low capsule along the facing.
                    SimVec3 fwd = pl.FacingDir * 0.7f;
                    a = new SimVec3(pl.Position.X, 0.18f, pl.Position.Z) - fwd;
                    b = new SimVec3(pl.Position.X, 0.18f, pl.Position.Z) + fwd;
                    rad = 0.18f + r;
                }
                else
                {
                    bool keeperHands = AttributesOf(i).IsGoalkeeper && InOwnPenaltyArea(pl.Team, pl.Position);
                    float controlTop = keeperHands ? ac.GkMaxReachHeight : ac.ControlMaxHeight;
                    bool canControl = !blocked && p1.Y <= controlTop;
                    if (canControl) continue;
                    a = new SimVec3(pl.Position.X, br, pl.Position.Z);
                    b = new SimVec3(pl.Position.X, SimMath.Max(br, ac.PlayerHeadHeight - br), pl.Position.Z);
                    rad = br + r;
                }

                if (!SimGeometry.SweepSphereCapsule(p0, p1, a, b, rad, out float h) || h >= best) continue;
                // Only an approaching ball bounces (relative to the player's motion).
                SimVec3 pc = p0 + (p1 - p0) * h;
                SimVec3 n = pc - SimGeometry.ClosestOnSegment(pc, a, b);
                if (SimVec3.Dot(_s.Ball.Velocity - pl.Velocity, n) >= 0f) continue;
                best = h;
                hitId = i;
                hitA = a;
                hitB = b;
                hitRadius = rad;
            }

            if (hitId < 0) return;
            ref SimPlayer hp = ref _s.Players[hitId];
            SimVec3 contact = best > 0f ? p0 + (p1 - p0) * best : p1;
            SimVec3 q = SimGeometry.ClosestOnSegment(contact, hitA, hitB);
            SimVec3 d = contact - q;
            float dist = d.Magnitude;
            SimVec3 nrm = dist > 1e-6f ? d / dist : -_s.Ball.Velocity.Normalized;
            SimVec3 vRel = _s.Ball.Velocity - hp.Velocity;
            float vn = SimVec3.Dot(vRel, nrm);
            SimVec3 tangential = vRel - nrm * vn;
            SimVec3 vOut = tangential * ac.BodyTangentialKeep - nrm * (vn * ac.BodyRestitution) + hp.Velocity;

            SimVec3 pos = q + nrm * hitRadius;
            _s.Ball.Position = new SimVec3(pos.X, SimMath.Max(pos.Y, r), pos.Z);
            _s.Ball.Velocity = vOut;
            _s.Ball.ClearKnuckle();
            // Glancing friction at the contact point (-nrm * r from the centre) slows the tangential motion and spins the
            // ball: J_t = -m (1 - keep) v_t, dw = (arm x J_t) / I.
            SimVec3 frictionImpulse = tangential * (-(1f - ac.BodyTangentialKeep) * _cfg.Ball.Mass * ac.BodyDeflectionSpinTransfer);
            SimVec3 spinKick = SimVec3.Cross(nrm * -r, frictionImpulse) / _cfg.Ball.MomentOfInertia;
            _s.Ball.Spin = SimVec3.ClampMagnitude(_s.Ball.Spin * 0.5f + spinKick, _cfg.Ball.MaxSpin);
            _s.Ball.Grounded = _s.Ball.Position.Y <= r + 1e-3f && vOut.Y <= 0.01f;

            int previous = _s.LastTouchPlayer;
            SimTeam previousTeam = _s.LastTouchTeam;
            SimTeam team = hp.Team;
            if (_s.ShotLive && _s.Shooter >= 0 && TeamOf(_s.Shooter) == team) _s.Shooter = hitId;
            if (_s.PassLive && _s.PassTeam != team)
            {
                // Deflected by an opponent: whoever gets it next did not receive a pass (no completion, no assist).
                _s.PassLive = false;
                _s.AssistCandidate = -1;
            }

            _s.LastTouchPlayer = hitId;
            _s.LastTouchTeam = team;
            NoteTouch(hitId);
            Emit(MatchEventType.Deflection, hitId, previous, team, _s.Ball.Position, vOut.Magnitude);

            if (team == previousTeam && _s.Phase == SimMatchPhase.InPlay)
            {
                // Touched by a team-mate (Law 11): a player in an offside position who touches it is penalised;
                // otherwise offside is judged afresh from this touch, as if he had played a new pass.
                if ((_s.OffsideMask & (1 << hitId)) != 0) CallOffside(hitId);
                else EvaluateOffside(hitId, RestartType.None);
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
            // Wait for a dropping or bobbling ball to come (nearly) down: the touch puts it on the turf, so meeting it
            // higher would snap it down visibly.
            if (!DribbleBallLowEnough(_s.Ball.Position.Y)) return;

            SimVec3 dir = p.Speed > 0.5f ? p.Velocity.Flat.Normalized : p.FacingDir;
            float dirYaw = SimMath.Yaw(dir);
            bool due = _s.Tick >= p.NextTouchTick;
            bool sharpTurn = SimMath.Abs(SimMath.DeltaAngle(p.LastTouchYaw, dirYaw)) > ac.RetouchTurnDeg * SimMath.Deg2Rad
                             && _s.Tick >= p.LastTouchTick + _cfg.SecondsToTicks(ac.MinTouchInterval);
            if (!due && !sharpTurn) return;
            if (d > ac.ControlRadius * DribbleCatchUpFactor) return; // must catch up with the ball first

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
            // Average roll speed over the interval sets the speed-proportional (wet/heavy turf) part of the decel.
            float decel = _cfg.Ball.RollingResistance * _cfg.Ball.Gravity
                          + _cfg.Ball.RollingDragPerSpeed * (disp.Magnitude / interval);
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
            _s.Ball.ClearKnuckle();
            _s.LastTouchPlayer = id;
            _s.LastTouchTeam = p.Team;
            p.NextTouchTick = _s.Tick + SimMath.Clamp(_cfg.SecondsToTicks(interval), 1, 10000);
            p.LastTouchYaw = dirYaw;
            p.LastTouchTick = _s.Tick;
            float quality = SimMath.Clamp01(1f - SimMath.Abs(err) * 6f - (sprint ? 0.25f * (1f - drib) : 0f));
            Emit(MatchEventType.DribbleTouch, id, -1, p.Team, _s.Ball.Position, v.Magnitude, quality);
        }

        /// <summary>
        /// Sets the ball after a first touch with flat velocity <paramref name="flatV"/> and touch error
        /// <paramref name="err"/>. A ball met in the air (chest, thigh, a bounce) is cushioned down from where it was
        /// met, with a little backspin so it checks up at the feet; a heavy touch pops it up instead. A ground ball is
        /// killed flat and rolling unless the touch is heavy enough to make it bobble.
        /// </summary>
        private void ApplyFirstTouchHeight(SimVec3 flatV, float err)
        {
            var ac = _cfg.Actions;
            float r = _cfg.Ball.Radius;
            SimVec3 pos = _s.Ball.Position;
            _s.Ball.ClearKnuckle();
            bool aerial = pos.Y > r + 0.05f;
            float pop = aerial
                ? err * ac.FirstTouchPopPerError
                : SimMath.Max(0f, err - ac.FirstTouchGroundPopThreshold) * ac.FirstTouchPopPerError;
            if (!aerial && pop <= 0f)
            {
                _s.Ball.Position = new SimVec3(pos.X, r, pos.Z);
                _s.Ball.Velocity = flatV;
                _s.Ball.Spin = BallIntegrator.RollingSpin(flatV, r, 0f);
                _s.Ball.Grounded = true;
                return;
            }

            float vy = aerial ? pop - ac.FirstTouchCushionDropSpeed : pop;
            _s.Ball.Position = new SimVec3(pos.X, SimMath.Max(pos.Y, r), pos.Z);
            _s.Ball.Velocity = new SimVec3(flatV.X, vy, flatV.Z);
            float flat = flatV.Magnitude;
            SimVec3 right = flat > 1e-4f ? SimVec3.Cross(SimVec3.Up, flatV / flat) : SimVec3.Zero;
            _s.Ball.Spin = right * -ac.FirstTouchCushionBackSpin;
            _s.Ball.Grounded = false;
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

        private const float TieEpsilon = 1e-4f;

        /// <summary>Highest the ball's underside may be (m) for a dribble touch to play it along the ground.</summary>
        public const float DribbleMaxHeightAboveTurf = 0.1f;

        /// <summary>Shared by <see cref="UpdateDribble"/> and <see cref="TryPredictDribbleTouch"/> so prediction matches.</summary>
        private bool DribbleBallLowEnough(float ballY) => ballY <= _cfg.Ball.Radius + DribbleMaxHeightAboveTurf;

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
            int ties = 0;
            float bestD = float.MaxValue;
            for (int i = 0; i < MatchSimState.PlayerCount; i++)
            {
                ref SimPlayer p = ref _s.Players[i];
                if (p.SentOff || i == _s.Owner || _s.Tick < p.ControlBlockedUntilTick) continue;
                if (p.ActionState == SimPlayerActionState.Grounded || p.ActionState == SimPlayerActionState.Sliding) continue;
                float d = SimMath.FlatDistance(p.Position, _s.Ball.Position);
                bool keeperHands = AttributesOf(i).IsGoalkeeper && InOwnPenaltyArea(p.Team, p.Position);
                // A shot on its way to the keeper is resolved by the save at closest approach, not by a pickup.
                if (keeperHands && SavePending(i)) continue;
                float radius = keeperHands ? ac.ControlRadius * 1.5f : ac.ControlRadius;
                float maxH = keeperHands ? ac.GkMaxReachHeight : ac.ControlMaxHeight;
                if (d > radius || h > maxH || d >= ownerDist) continue;
                if (d < bestD - TieEpsilon)
                {
                    bestD = d;
                    best = i;
                    ties = 1;
                }
                else if (d <= bestD + TieEpsilon)
                {
                    // Equidistant: a seeded draw, so ties do not always go to the lower id (Home).
                    ties++;
                    if (_s.Rng.Range(0, ties) == 0) best = i;
                }
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
            // Any controlled touch ends a penalty in progress (held or played) and an untouched indirect restart.
            _s.PenaltyKicker = -1;
            _s.IndirectRestartLive = false;
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
                _s.Ball.ClearKnuckle();
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
            ApplyFirstTouchHeight(v, err);
            p.NextTouchTick = _s.Tick + _cfg.SecondsToTicks(0.25f + (1f - quality) * 0.3f);
            p.LastTouchYaw = SimMath.Yaw(ahead);
            p.LastTouchTick = _s.Tick;
            Emit(MatchEventType.FirstTouch, id, -1, team, _s.Ball.Position, relSpeed, quality);
        }
    }
}
