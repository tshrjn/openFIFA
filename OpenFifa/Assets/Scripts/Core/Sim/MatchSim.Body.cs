namespace OpenFifa.Core
{
    /// <summary>
    /// Bodies: player-player contact (separation plus a momentum exchange between the two masses), stumbles and falls
    /// from heavy contact and from tackles.
    /// </summary>
    public sealed partial class MatchSim
    {
        /// <summary>
        /// Keeps players at least two separation radii apart (one deterministic pass in id order). Each overlap is a
        /// contact between two bodies: the overlap is split evenly (or partly by mass, SeparationMassWeight; a restart
        /// taker and a braced kicker do not move at all), and in open play their closing speed along the contact normal is
        /// mostly absorbed as an inelastic impulse shared by mass, so a light winger running into a centre back
        /// bounces off while the centre back barely slows. An opponent whose speed change exceeds what his balance
        /// can take stumbles (off balance, and a carrier spills the ball) or goes down; a player knocked down from
        /// Body contact never gives a foul by itself (only tackles do). A player on the ground does not move: whoever runs
        /// into him takes the whole correction. Everyone is then re-clamped to the playing area plus the run-off margin.
        /// </summary>
        private void SeparatePlayers()
        {
            var ac = _cfg.Actions;
            float minD = 2f * ac.PlayerSeparationRadius;
            float minD2 = minD * minD;
            int taker = _s.Phase == SimMatchPhase.AwaitingRestart ? _s.RestartTakerId : -1;
            bool live = _s.Phase == SimMatchPhase.InPlay;
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

                    bool downI = IsDown(_s.Players[i]), downJ = IsDown(_s.Players[j]);
                    // A kicker in his wind-up is planted on the ball: he holds his spot and his run, so a bump does not
                    // carry him away from the ball and turn the strike into a whiff (the other body gives way).
                    bool bracedI = _s.Players[i].ActionState == SimPlayerActionState.WindUp;
                    bool bracedJ = _s.Players[j].ActionState == SimPlayerActionState.WindUp;
                    float mi = ContactMass(i), mj = ContactMass(j);
                    float wi;
                    if (j == taker) wi = 1f;
                    else if (i == taker) wi = 0f;
                    else if (bracedI && !bracedJ && !downJ) wi = 0f;
                    else if (bracedJ && !bracedI && !downI) wi = 1f;
                    else if (downI && downJ) wi = 0.5f;
                    else if (downI) wi = 0f; // he lies still; j steps around him
                    else if (downJ) wi = 1f;
                    else wi = ac.SeparationMassWeight * (mj / (mi + mj)) + (1f - ac.SeparationMassWeight) * 0.5f;
                    _s.Players[i].Position -= n * (push * wi);
                    _s.Players[j].Position += n * (push * (1f - wi));

                    if (!live || downI || downJ) continue;
                    float closing = -SimVec3.Dot(_s.Players[j].Velocity.Flat - _s.Players[i].Velocity.Flat, n);
                    // Jostling (players leaning on each other tick after tick) is only the position correction above:
                    // bleeding speed on every touching tick slowed every pressed carrier to a crawl. A real collision
                    // (a closing speed of at least BodyContactMinSpeed) exchanges momentum once.
                    if (closing < ac.BodyContactMinSpeed) continue;
                    float impulse = ac.BodyContactAbsorb * closing * (mi * mj / (mi + mj));
                    float dvi = impulse / mi, dvj = impulse / mj;
                    if (bracedI != bracedJ)
                    {
                        // The braced kicker keeps his run; the other body takes the whole speed change.
                        dvi = bracedI ? 0f : ac.BodyContactAbsorb * closing;
                        dvj = bracedJ ? 0f : ac.BodyContactAbsorb * closing;
                    }
                    // A challenge on the ball carrier exchanges no momentum: he and the challenger slide off each other
                    // (position correction only) and his balance judges the hit below, so a strong challenge can still
                    // knock him off the ball. Exchanging momentum there stopped carriers dead and parked the challenger
                    // in front of them, which cut a scripted user's goals against World Class AI by about a third.
                    bool carrierContact = _s.Owner == i || _s.Owner == j;
                    if (!carrierContact)
                    {
                        _s.Players[i].Velocity -= n * dvi;
                        _s.Players[j].Velocity += n * dvj;
                    }

                    if (TeamOf(i) == TeamOf(j)) continue;
                    int knocked = dvi >= dvj ? i : j;
                    int other = knocked == i ? j : i;
                    float dv = knocked == i ? dvi : dvj;
                    int outcome = KnockBody(knocked, other, dv);
                    if (outcome > 0 || closing >= ac.BodyContactEventSpeed)
                        Emit(MatchEventType.BodyContact, knocked, other, TeamOf(knocked), _s.Players[knocked].Position,
                            closing, dv, outcome);
                }
            }

            // A push must not carry anyone past the run-off margin around the pitch.
            for (int i = 0; i < MatchSimState.PlayerCount; i++)
                if (!_s.Players[i].SentOff) ClampToArea(ref _s.Players[i]);
        }

        private static bool IsDown(in SimPlayer p) =>
            p.ActionState == SimPlayerActionState.Grounded || p.ActionState == SimPlayerActionState.Sliding;

        /// <summary>Effective mass for a contact: body mass, more for a strong player bracing to shield the ball.</summary>
        private float ContactMass(int id)
        {
            SimPlayerAttributes a = AttributesOf(id);
            float m = a.BodyMass;
            if (_s.Players[id].Shielding) m *= 1f + _cfg.Actions.ShieldMassBonus * SimPlayerAttributes.N(a.Strength);
            return SimMath.Max(40f, m);
        }

        /// <summary>
        /// Applies a speed change <paramref name="dv"/> (m/s) to <paramref name="id"/>'s balance. Returns 0 held up,
        /// 1 stumbled (off balance; a carrier spills the ball), 2 went down.
        /// </summary>
        private int KnockBody(int id, int by, float dv)
        {
            var ac = _cfg.Actions;
            ref SimPlayer p = ref _s.Players[id];
            SimPlayerAttributes a = AttributesOf(id);
            float steady = 0.7f * SimPlayerAttributes.N(a.Balance) + 0.3f * SimPlayerAttributes.N(a.Strength);
            float limit = ac.StumbleDeltaVMin + ac.StumbleDeltaVRange * steady;
            if (dv <= limit || _s.Tick < p.KnockImmuneUntilTick) return 0;

            if (dv > limit + ac.FallDeltaVOver && !a.IsGoalkeeper)
            {
                KnockDown(id, ac.ContactFallGroundTime);
                return 2;
            }

            p.OffBalanceUntilTick = _s.Tick + _cfg.SecondsToTicks(ac.StumbleSeconds);
            if (_s.Owner == id && !_s.KeeperHolding)
            {
                float over = SimMath.Clamp01((dv - limit) / ac.FallDeltaVOver);
                float pSpill = over * SimMath.Max(0f, ac.SpillControlBase - SimPlayerAttributes.N(a.BallControl));
                if (_s.Rng.NextFloat() < pSpill) SpillBall(id, by);
            }

            return 1;
        }

        /// <summary>Puts a player on the ground: any action in progress is lost, and a carrier loses the ball.</summary>
        private void KnockDown(int id, float seconds)
        {
            ref SimPlayer p = ref _s.Players[id];
            if (_s.Owner == id && !_s.KeeperHolding) SetBallLoose(_s.Ball.Velocity.Flat + p.Velocity.Flat * 0.3f, id);

            p.ActionState = SimPlayerActionState.Grounded;
            p.PendingAction = IntentAction.None;
            p.PendingStyle = KickStyle.Default;
            p.Skill = SkillMoveKind.None;
            p.Jockeying = false;
            p.Shielding = false;
            p.StateEndTick = _s.Tick + _cfg.SecondsToTicks(seconds);
            // Back on his feet, he cannot be bowled straight over again.
            p.KnockImmuneUntilTick = p.StateEndTick + _cfg.SecondsToTicks(_cfg.Actions.GetUpGraceSeconds);
            if (p.ControlBlockedUntilTick < p.StateEndTick) p.ControlBlockedUntilTick = p.StateEndTick;
        }

        /// <summary>A stumbling carrier pushes the ball away from himself; poor ball control spills it further.</summary>
        private void SpillBall(int id, int by)
        {
            ref SimPlayer p = ref _s.Players[id];
            SimPlayerAttributes a = AttributesOf(id);
            SimVec3 away = (p.Position - _s.Players[by].Position).Flat;
            SimVec3 dir = p.Speed > 0.5f ? p.Velocity.Flat.Normalized : p.FacingDir;
            if (away.SqrMagnitude > 1e-4f) dir = (dir + away.Normalized * 0.5f).Normalized;
            float spill = _cfg.Actions.StumbleSpillSpeed * (1.3f - SimPlayerAttributes.N(a.BallControl));
            dir = SimMath.RotateY(dir, _s.Rng.Range(-0.6f, 0.6f));
            SetBallLoose(dir * (p.Speed + spill), id);
            if (p.ActionState == SimPlayerActionState.Skill)
            {
                p.ActionState = SimPlayerActionState.Recovery;
                p.StateEndTick = _s.Tick + _cfg.SecondsToTicks(0.15f);
                p.Skill = SkillMoveKind.None;
                p.NextSkillTick = _s.Tick + _cfg.SecondsToTicks(_cfg.Actions.SkillCooldown);
            }
        }

        /// <summary>
        /// A carrier who just lost a clean tackle may go down: a slide through the ball takes the legs more often than
        /// a standing poke, and pace and poor balance make it likelier.
        /// </summary>
        private void TackleBodyOutcome(int carrier, int tackler, bool slide, SimPlayerAttributes ca)
        {
            var ac = _cfg.Actions;
            if (ca.IsGoalkeeper) return;
            float pace = SimMath.Min(1f, _s.Players[carrier].Speed / 6f);
            float pFall = (slide ? ac.SlideWinFallChance : ac.StandingWinFallChance)
                          * (1.3f - SimPlayerAttributes.N(ca.Balance)) * (0.6f + 0.4f * pace);
            if (_s.Rng.NextFloat() >= pFall) return;
            KnockDown(carrier, ac.TackleFallGroundTime);
            Emit(MatchEventType.BodyContact, carrier, tackler, TeamOf(carrier), _s.Players[carrier].Position,
                _s.Players[carrier].Speed, 0f, 2);
        }
    }
}
