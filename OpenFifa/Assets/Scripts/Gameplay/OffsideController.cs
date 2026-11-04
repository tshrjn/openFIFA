using System;
using System.Collections.Generic;
using UnityEngine;
using OpenFifa.Core;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// Runtime glue for offside: feeds ball touches and challenges into OffsideLogic,
    /// and on an offence stops play (MatchState.Stoppage), runs OffsidePresentation, then
    /// awards an indirect free kick to the defending team where the offence occurred.
    ///
    /// The decision source is swappable (UseDecisionSource) so another rules engine can
    /// emit OffsideDecisions and reuse the same stoppage, presentation and restart.
    /// </summary>
    public class OffsideController : MonoBehaviour
    {
        [SerializeField] private OffsideConfig _config;
        [SerializeField] private OffsidePresentationConfig _presentationConfig;

        private OffsideLogic _logic;
        private IOffsideDecisionSource _source;
        private OffsidePresentation _presentation;
        private MatchStateMachine _stateMachine;
        private BallController _ball;
        private BallOwnership _ownership;
        private BallTouchTracker _tracker;
        private PitchConfigData _pitch;
        private readonly List<PlayerIdentity> _players = new List<PlayerIdentity>();
        private readonly List<OffsidePlayerSnapshot> _snapshot = new List<OffsidePlayerSnapshot>();
        private Color _homeColor = Color.blue;
        private Color _awayColor = Color.red;
        private bool _restartPending;

        /// <summary>Fired when the indirect free kick has been set up.</summary>
        public event Action<OffsideDecision> OnFreeKickAwarded;

        public OffsideLogic Logic => _logic;
        public OffsidePresentation Presentation => _presentation;
        public BallTouchTracker Tracker => _tracker;

        /// <summary>
        /// Add and wire an OffsideController on <paramref name="host"/>.
        /// </summary>
        public static OffsideController Attach(GameObject host,
            OffsideConfig config, OffsidePresentationConfig presentationConfig,
            MatchStateMachine stateMachine, BallController ball,
            IReadOnlyList<GameObject> homePlayers, IReadOnlyList<GameObject> awayPlayers,
            PitchConfigData pitch, BroadcastCameraController broadcastCamera,
            Color homeColor, Color awayColor)
        {
            var controller = host.AddComponent<OffsideController>();
            controller.Initialize(config, presentationConfig, stateMachine, ball,
                homePlayers, awayPlayers, pitch, broadcastCamera, homeColor, awayColor);
            return controller;
        }

        public void Initialize(OffsideConfig config, OffsidePresentationConfig presentationConfig,
            MatchStateMachine stateMachine, BallController ball,
            IReadOnlyList<GameObject> homePlayers, IReadOnlyList<GameObject> awayPlayers,
            PitchConfigData pitch, BroadcastCameraController broadcastCamera,
            Color homeColor, Color awayColor)
        {
            if (config != null) _config = config;
            if (_config == null) _config = OffsideConfig.CreateDefault();
            if (presentationConfig != null) _presentationConfig = presentationConfig;

            _stateMachine = stateMachine;
            _ball = ball;
            _pitch = pitch ?? new PitchConfigData();
            _homeColor = homeColor;
            _awayColor = awayColor;

            CollectPlayers(homePlayers);
            CollectPlayers(awayPlayers);
            int perSide = Mathf.Max(homePlayers != null ? homePlayers.Count : 0,
                                    awayPlayers != null ? awayPlayers.Count : 0);

            // TeamA (home) attacks the +X goal: see MatchOrchestrator.CreateGoalDetectors.
            _logic = new OffsideLogic(_config.ToData(halfwayLineX: 0f, teamAAttacksPositiveX: true), perSide);
            UseDecisionSource(_logic);

            if (_ball != null)
            {
                _ownership = _ball.GetComponent<BallOwnership>();
                _tracker = _ball.GetComponent<BallTouchTracker>();
                if (_tracker == null) _tracker = _ball.gameObject.AddComponent<BallTouchTracker>();
                _tracker.OnBallTouched += HandleBallTouched;
            }

            var presentationGo = new GameObject("OffsidePresentation");
            presentationGo.transform.SetParent(transform, false);
            _presentation = presentationGo.AddComponent<OffsidePresentation>();
            _presentation.Initialize(_presentationConfig, _pitch.HalfWidth, Camera.main, broadcastCamera);

            GoalDetector.OnGoalScored += HandleGoalScored;
            if (_stateMachine != null) _stateMachine.OnStateChanged += HandleStateChanged;
        }

        /// <summary>
        /// Listen to a different offside decision source (e.g. a simulation that emits its own
        /// OffsideDecisions). The built-in logic is disabled while another source is active.
        /// </summary>
        public void UseDecisionSource(IOffsideDecisionSource source)
        {
            if (_source != null) _source.OnOffence -= HandleOffence;
            _source = source;
            if (_source != null) _source.OnOffence += HandleOffence;
            if (_logic != null && !ReferenceEquals(source, _logic)) _logic.SetEnabled(false);
        }

        private void CollectPlayers(IReadOnlyList<GameObject> team)
        {
            if (team == null) return;
            for (int i = 0; i < team.Count; i++)
            {
                if (team[i] == null) continue;
                var identity = team[i].GetComponent<PlayerIdentity>();
                if (identity != null) _players.Add(identity);

                var tackle = team[i].GetComponent<TackleSystem>();
                if (tackle != null)
                {
                    var captured = identity;
                    tackle.OnTackle += _ => HandleChallenge(captured);
                }
            }
        }

        private bool IsLivePlay()
        {
            if (_stateMachine == null) return true;
            var state = _stateMachine.CurrentState;
            return (state == MatchState.FirstHalf || state == MatchState.SecondHalf)
                   && !_restartPending
                   && (_presentation == null || !_presentation.IsPlaying);
        }

        private void HandleBallTouched(PlayerIdentity player, BallTouchKind kind, RestartType restart)
        {
            if (player == null || !_logic.IsEnabled || !IsLivePlay()) return;

            Vector3 ballPos = _ball.transform.position;
            _logic.RegisterTouch(new BallTouch(player.Team, player.PlayerId, kind, restart),
                ballPos.x, ballPos.z, TakeSnapshot());
        }

        private void HandleChallenge(PlayerIdentity challenger)
        {
            if (challenger == null || _ball == null || !_logic.IsEnabled || !IsLivePlay()) return;
            Vector3 p = challenger.transform.position;
            Vector3 b = _ball.transform.position;
            _logic.RegisterChallenge(challenger.Team, challenger.PlayerId, p.x, p.z, b.x, b.z);
        }

        private void Update()
        {
            // Interfering with an opponent: an offside-positioned player closing down the
            // opponent in possession, near the ball.
            if (_logic == null || !_logic.IsEnabled || _ownership == null || !_ownership.IsOwned) return;
            if (!IsLivePlay()) return;

            var carrierTransform = _ownership.CurrentOwnerTransform;
            if (carrierTransform == null) return;
            var carrier = carrierTransform.GetComponent<PlayerIdentity>();
            if (carrier == null) return;

            var attackingTeam = Opponent(carrier.Team);
            if (_logic.GetPhase(attackingTeam) == null) return;

            float challengeRadius = _config.ChallengeRadius;
            for (int i = 0; i < _players.Count; i++)
            {
                var p = _players[i];
                if (p == null || p.Team != attackingTeam || !_logic.IsFlagged(p.Team, p.PlayerId)) continue;
                Vector3 offset = p.transform.position - carrierTransform.position;
                offset.y = 0f;
                if (offset.magnitude <= challengeRadius)
                {
                    HandleChallenge(p);
                    return;
                }
            }
        }

        private List<OffsidePlayerSnapshot> TakeSnapshot()
        {
            _snapshot.Clear();
            for (int i = 0; i < _players.Count; i++)
            {
                var p = _players[i];
                if (p == null || !p.isActiveAndEnabled) continue;
                Vector3 pos = p.transform.position;
                _snapshot.Add(new OffsidePlayerSnapshot(p.Team, p.PlayerId, pos.x, pos.z));
            }
            return _snapshot;
        }

        private void HandleOffence(OffsideDecision decision)
        {
            if (decision == null || !decision.IsOffence || _restartPending) return;
            if (_presentation != null && _presentation.IsPlaying) return;

            _restartPending = true;
            if (_stateMachine != null) _stateMachine.BeginStoppage();

            var offender = FindPlayer(decision.OffenderTeam, decision.OffenderId);
            Debug.Log($"[Offside] {decision.OffenderTeam} #{decision.OffenderId + 1} offside " +
                      $"({decision.Reason}); IFK to {decision.DefendingTeam} at " +
                      $"({decision.FreeKickX:F1}, {decision.FreeKickZ:F1})");

            var info = new OffsideBannerInfo(
                shirtNumber: offender != null ? offender.PlayerId + 1 : decision.OffenderId + 1,
                playerName: offender != null ? offender.PlayerName : $"Player {decision.OffenderId + 1}",
                teamLabel: decision.OffenderTeam == TeamIdentifier.TeamA ? "Home" : "Away",
                teamColor: decision.OffenderTeam == TeamIdentifier.TeamA ? _homeColor : _awayColor);

            if (_presentation == null || !_presentation.Play(decision, info, () => AwardFreeKick(decision)))
                AwardFreeKick(decision);
        }

        /// <summary>
        /// Indirect free kick to the defending team at the offence position.
        /// </summary>
        private void AwardFreeKick(OffsideDecision decision)
        {
            if (this == null) return;

            float hl = _pitch.HalfLength - 0.5f;
            float hw = _pitch.HalfWidth - 0.5f;
            var spot = new Vector3(
                Mathf.Clamp(decision.FreeKickX, -hl, hl), 0.5f,
                Mathf.Clamp(decision.FreeKickZ, -hw, hw));

            if (_tracker != null) _tracker.ClearHistory();
            if (_ownership != null) _ownership.Release();
            if (_ball != null) _ball.ResetToPosition(spot);
            _logic.Reset();

            var defending = decision.DefendingTeam;
            float clearance = _config.FreeKickClearance;
            PlayerIdentity taker = null;
            float bestDist = float.MaxValue;

            for (int i = 0; i < _players.Count; i++)
            {
                var p = _players[i];
                if (p == null) continue;
                Vector3 offset = p.transform.position - spot;
                offset.y = 0f;

                if (p.Team != defending)
                {
                    // Push offending team back to the minimum distance.
                    if (offset.magnitude < clearance)
                    {
                        Vector3 dir = offset.sqrMagnitude > 0.0001f ? offset.normalized : new Vector3(Mathf.Sign(spot.x), 0f, 0f);
                        Teleport(p, spot + dir * clearance);
                    }
                }
                else if (offset.magnitude < bestDist)
                {
                    bestDist = offset.magnitude;
                    taker = p;
                }
            }

            // Nearest defender stands just behind the ball (own-goal side) to take it.
            if (taker != null)
            {
                float defendSign = _logic.AttackSign(defending);
                Teleport(taker, new Vector3(spot.x - defendSign * 0.8f, taker.transform.position.y, spot.z));
            }

            if (_tracker != null) _tracker.SetPendingRestart(RestartType.IndirectFreeKick);
            _restartPending = false;
            if (_stateMachine != null) _stateMachine.EndStoppage();
            OnFreeKickAwarded?.Invoke(decision);
        }

        private static void Teleport(PlayerIdentity player, Vector3 position)
        {
            position.y = player.transform.position.y;
            var rb = player.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.position = position;
            }
            player.transform.position = position;
        }

        private PlayerIdentity FindPlayer(TeamIdentifier team, int id)
        {
            for (int i = 0; i < _players.Count; i++)
            {
                if (_players[i] != null && _players[i].Team == team && _players[i].PlayerId == id)
                    return _players[i];
            }
            return null;
        }

        private static TeamIdentifier Opponent(TeamIdentifier team)
        {
            return team == TeamIdentifier.TeamA ? TeamIdentifier.TeamB : TeamIdentifier.TeamA;
        }

        private void HandleGoalScored(TeamIdentifier scoringTeam)
        {
            _logic?.Reset();
        }

        private void HandleStateChanged(MatchState oldState, MatchState newState)
        {
            if (newState == MatchState.HalfTime || newState == MatchState.FullTime ||
                newState == MatchState.PreKickoff || newState == MatchState.GoalCelebration)
                _logic?.Reset();
        }

        private void OnDestroy()
        {
            GoalDetector.OnGoalScored -= HandleGoalScored;
            if (_stateMachine != null) _stateMachine.OnStateChanged -= HandleStateChanged;
            if (_tracker != null) _tracker.OnBallTouched -= HandleBallTouched;
            if (_source != null) _source.OnOffence -= HandleOffence;
        }
    }
}
