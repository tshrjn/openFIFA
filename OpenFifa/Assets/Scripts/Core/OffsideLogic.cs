using System;
using System.Collections.Generic;

namespace OpenFifa.Core
{
    /// <summary>
    /// Pure C# implementation of Law 11 (Offside).
    ///
    /// Offside position (judged when a team-mate plays or touches the ball):
    /// any part of head, body or feet (modelled as centre + BodyRadius toward the goal line;
    /// arms are not modelled) is in the opponents' half, excluding the halfway line, and is
    /// nearer the opponents' goal line than both the ball and the second-last opponent
    /// (the goalkeeper counts as an opponent like any other). Level is onside.
    ///
    /// Offence: an offside-positioned player becomes involved by touching the ball
    /// (they are the next player of their team to touch it), or by challenging an opponent
    /// within InterferenceRadius of the ball.
    ///
    /// No offence directly from a goal kick, throw-in or corner kick. A deliberate play by an
    /// opponent resets the phase; a deflection, rebound or save does not.
    ///
    /// No Unity dependency — fully testable in EditMode.
    /// </summary>
    public class OffsideLogic : IOffsideDecisionSource
    {
        private readonly OffsideConfigData _config;
        private readonly Dictionary<TeamIdentifier, OffsidePhase> _phases =
            new Dictionary<TeamIdentifier, OffsidePhase>();
        private readonly List<float> _opponentDepths = new List<float>();

        /// <summary>Fired whenever an offside offence is detected.</summary>
        public event Action<OffsideDecision> OnOffence;

        public OffsideConfigData Config => _config;

        /// <summary>Whether offside is currently being applied.</summary>
        public bool IsEnabled { get; private set; }

        public OffsideLogic(OffsideConfigData config, int playersPerSide = 11)
        {
            _config = config ?? new OffsideConfigData();
            IsEnabled = _config.IsEnabledFor(playersPerSide);
        }

        /// <summary>Turn offside on or off at runtime. Turning it off clears live phases.</summary>
        public void SetEnabled(bool enabled)
        {
            IsEnabled = enabled;
            if (!enabled) Reset();
        }

        /// <summary>Clear all live phases (e.g. after a goal, at half time, after a stoppage).</summary>
        public void Reset()
        {
            _phases.Clear();
        }

        /// <summary>+1 if the team attacks the +X goal, -1 if it attacks -X.</summary>
        public float AttackSign(TeamIdentifier team)
        {
            bool aPositive = _config.TeamAAttacksPositiveX;
            bool positive = team == TeamIdentifier.TeamA ? aPositive : !aPositive;
            return positive ? 1f : -1f;
        }

        /// <summary>The live phase for a team, or null.</summary>
        public OffsidePhase GetPhase(TeamIdentifier team)
        {
            return _phases.TryGetValue(team, out var phase) ? phase : null;
        }

        /// <summary>Whether the player is currently flagged as having been in an offside position.</summary>
        public bool IsFlagged(TeamIdentifier team, int playerId)
        {
            var phase = GetPhase(team);
            return phase != null && phase.IsFlagged(playerId);
        }

        /// <summary>
        /// Builds the offside snapshot for <paramref name="attackingTeam"/> at the moment
        /// <paramref name="passerId"/> plays the ball. Pure: does not change state.
        /// </summary>
        public OffsidePhase EvaluatePositions(TeamIdentifier attackingTeam, int passerId,
            float ballX, IReadOnlyList<OffsidePlayerSnapshot> players)
        {
            float sign = AttackSign(attackingTeam);
            float r = _config.BodyRadius;
            float tol = _config.LevelTolerance;

            // Depth = distance travelled toward the opponents' goal line.
            float halfwayDepth = _config.HalfwayLineX * sign;
            float ballDepth = ballX * sign + _config.BallRadius;

            // Each opponent's point nearest their own goal line; take the second-deepest.
            _opponentDepths.Clear();
            if (players != null)
            {
                for (int i = 0; i < players.Count; i++)
                {
                    if (players[i].Team != attackingTeam)
                        _opponentDepths.Add(players[i].X * sign + r);
                }
            }
            _opponentDepths.Sort();
            float secondLastDepth = _opponentDepths.Count >= 2
                ? _opponentDepths[_opponentDepths.Count - 2]
                : float.NegativeInfinity; // fewer than two opponents: only ball/halfway apply

            float defenderLineX = float.IsNegativeInfinity(secondLastDepth)
                ? _config.HalfwayLineX
                : secondLastDepth * sign;
            var phase = new OffsidePhase(attackingTeam, passerId, ballDepth * sign, defenderLineX);

            if (players == null) return phase;

            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                if (p.Team != attackingTeam || p.PlayerId == passerId) continue;

                float front = p.X * sign + r;
                bool inOpponentHalf = front > halfwayDepth;
                bool beyondBall = front > ballDepth + tol;
                bool beyondSecondLast = front > secondLastDepth + tol;

                if (inOpponentHalf && beyondBall && beyondSecondLast)
                    phase.Flag(p.PlayerId, front * sign);
            }

            return phase;
        }

        /// <summary>Convenience: whether one attacker is in an offside position.</summary>
        public bool IsInOffsidePosition(TeamIdentifier attackingTeam, int attackerId, int passerId,
            float ballX, IReadOnlyList<OffsidePlayerSnapshot> players)
        {
            return EvaluatePositions(attackingTeam, passerId, ballX, players).IsFlagged(attackerId);
        }

        /// <summary>
        /// Register a contact with the ball. Positions are the state of play at this instant
        /// (used as the new snapshot for the touching team).
        /// Returns the offside decision (OffsideDecision.None when no offence).
        /// </summary>
        public OffsideDecision RegisterTouch(BallTouch touch, float ballX, float ballZ,
            IReadOnlyList<OffsidePlayerSnapshot> players)
        {
            if (!IsEnabled) return OffsideDecision.None;

            var team = touch.Team;
            var opponent = team == TeamIdentifier.TeamA ? TeamIdentifier.TeamB : TeamIdentifier.TeamA;

            // 1. An offside-positioned player touching the ball commits an offence.
            var ownPhase = GetPhase(team);
            if (ownPhase != null && ownPhase.IsFlagged(touch.PlayerId))
            {
                FindPosition(players, team, touch.PlayerId, ballX, ballZ, out float px, out float pz);
                var decision = new OffsideDecision(OffsideReason.PlayingBall, team, touch.PlayerId,
                    ownPhase.PasserId, px, pz,
                    ownPhase.GetAttackerLineX(touch.PlayerId), ownPhase.DefenderLineX, ownPhase.BallLineX);
                Reset();
                OnOffence?.Invoke(decision);
                return decision;
            }

            // 2. A deliberate play by the opponent resets their offside phase.
            //    Deflections, rebounds and saves do not.
            if (touch.Kind == BallTouchKind.Deliberate)
                _phases.Remove(opponent);

            // 3. This touch starts a new phase for the touching team, unless it is a
            //    restart from which there is no offside offence.
            if (IsExemptRestart(touch.Restart))
                _phases.Remove(team);
            else
                _phases[team] = EvaluatePositions(team, touch.PlayerId, ballX, players);

            return OffsideDecision.None;
        }

        /// <summary>
        /// Register a player challenging an opponent for the ball (e.g. a tackle).
        /// An offside-positioned player doing this within InterferenceRadius of the ball
        /// is interfering with an opponent.
        /// </summary>
        public OffsideDecision RegisterChallenge(TeamIdentifier team, int playerId,
            float playerX, float playerZ, float ballX, float ballZ)
        {
            if (!IsEnabled || _config.InterferenceRadius <= 0f) return OffsideDecision.None;

            var phase = GetPhase(team);
            if (phase == null || !phase.IsFlagged(playerId)) return OffsideDecision.None;

            float dx = playerX - ballX;
            float dz = playerZ - ballZ;
            float r = _config.InterferenceRadius;
            if (dx * dx + dz * dz > r * r) return OffsideDecision.None;

            var decision = new OffsideDecision(OffsideReason.ChallengingOpponent, team, playerId,
                phase.PasserId, playerX, playerZ,
                phase.GetAttackerLineX(playerId), phase.DefenderLineX, phase.BallLineX);
            Reset();
            OnOffence?.Invoke(decision);
            return decision;
        }

        /// <summary>Law 11.3: no offside offence directly from these restarts.</summary>
        public static bool IsExemptRestart(RestartType restart)
        {
            return restart == RestartType.GoalKick
                || restart == RestartType.ThrowIn
                || restart == RestartType.CornerKick;
        }

        private static void FindPosition(IReadOnlyList<OffsidePlayerSnapshot> players,
            TeamIdentifier team, int playerId, float fallbackX, float fallbackZ,
            out float x, out float z)
        {
            if (players != null)
            {
                for (int i = 0; i < players.Count; i++)
                {
                    if (players[i].Team == team && players[i].PlayerId == playerId)
                    {
                        x = players[i].X;
                        z = players[i].Z;
                        return;
                    }
                }
            }
            x = fallbackX;
            z = fallbackZ;
        }
    }
}
