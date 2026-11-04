using System.Collections.Generic;

namespace OpenFifa.Core
{
    /// <summary>
    /// Which player the user controls. Follows the ball automatically (pass reception, gaining possession, set-piece
    /// taker) and handles manual switches through <see cref="SmartSwitchSelector"/>. The keeper is never selected by a
    /// switch; the user only gets him as a restart taker (goal kicks) or when he holds the ball.
    /// </summary>
    public sealed class HumanPlayerSelector
    {
        /// <summary>A manual switch blocks automatic defensive switching for this long (s).</summary>
        public const float ManualSwitchHold = 1.5f;

        public SimTeam Team { get; }
        public int ControlledId { get; private set; }
        /// <summary>Number of times the controlled player changed (for tests and HUD flashes).</summary>
        public int SwitchCount { get; private set; }

        private readonly List<ControlPlayerInfo> _candidates = new List<ControlPlayerInfo>(11);
        private readonly List<ControlPlayerInfo> _teammates = new List<ControlPlayerInfo>(11);
        private float _lastManualSwitch = -100f;

        public HumanPlayerSelector(SimTeam team, int initialId)
        {
            Team = team;
            ControlledId = initialId;
        }

        /// <summary>Teammates of the controlled player (all 11, outfield and keeper), for aim assist. Refreshed by <see cref="Update"/>.</summary>
        public IReadOnlyList<ControlPlayerInfo> Teammates => _teammates;

        private long _blocked;

        public void Set(int id)
        {
            if (id == ControlledId || id < 0) return;
            if ((_blocked & (1L << id)) != 0) return;
            ControlledId = id;
            SwitchCount++;
        }

        /// <summary>
        /// Call once per sim tick after <see cref="MatchSim.Step"/> with that step's events, and the control intent that
        /// was applied this tick (for manual switches).
        /// </summary>
        /// <param name="otherHumans">Bit mask of players held by other user seats; never switched to.</param>
        public void Update(MatchSim sim, IReadOnlyList<MatchEvent> events, in ControlIntent ci, bool autoSwitchOnReception,
            float autoSwitchDefenceDistance, long otherHumans = 0)
        {
            _blocked = otherHumans;
            var s = sim.State;
            float now = s.Tick * sim.Dt;
            RefreshLists(sim);

            // 1) Our player has the ball (dribble, reception, keeper catch, restart taker): control him.
            if (s.Owner >= 0 && MatchSim.TeamOf(s.Owner) == Team)
            {
                if (autoSwitchOnReception || s.Phase != SimMatchPhase.InPlay || ControlledIsKeeper(sim))
                    Set(s.Owner);
                return;
            }
            if (s.Phase == SimMatchPhase.AwaitingRestart && s.RestartTeam == Team && s.RestartTakerId >= 0)
            {
                Set(s.RestartTakerId);
                return;
            }

            // 2) A pass to our team is in flight: switch to the intended receiver as it is released.
            if (autoSwitchOnReception && events != null)
            {
                for (int i = 0; i < events.Count; i++)
                {
                    var e = events[i];
                    if (e.Type == MatchEventType.Pass && e.Team == Team && e.OtherPlayerId >= 0
                        && MatchSim.TeamOf(e.OtherPlayerId) == Team)
                    {
                        Set(e.OtherPlayerId);
                        return;
                    }
                }
            }

            // 3) Keeper released the ball / we lost it: never leave the user on the keeper.
            if (ControlledIsKeeper(sim))
            {
                Set(SmartSwitchSelector.SelectNearestToBallPath(_candidates, s.Ball.Position, s.Ball.Velocity, -1));
            }

            // 4) Manual switch.
            if (ci.Action == ControlAction.SwitchPlayer)
            {
                int next = -1;
                if (ci.DirectionalSwitch)
                {
                    // Directional switches arrive in pitch space via the session (see SinglePlayerMatchSession).
                    next = SmartSwitchSelector.SelectDirectional(_candidates, s.Players[ControlledId].Position, ControlledId, _lastFlickPitch);
                }
                if (next < 0) next = SmartSwitchSelector.SelectNearestToBallPath(_candidates, s.Ball.Position, s.Ball.Velocity, ControlledId);
                Set(next);
                _lastManualSwitch = now;
                return;
            }

            // 5) Optional assisted defence (low levels): if the user is far from a ball the opponents have, hand him
            //    the best interceptor.
            bool defending = s.Owner >= 0 && MatchSim.TeamOf(s.Owner) != Team;
            if (defending && autoSwitchDefenceDistance > 0f && now - _lastManualSwitch > ManualSwitchHold
                && SimMath.FlatDistance(s.Players[ControlledId].Position, s.Ball.Position) > autoSwitchDefenceDistance)
            {
                Set(SmartSwitchSelector.SelectNearestToBallPath(_candidates, s.Ball.Position, s.Ball.Velocity, ControlledId));
            }
        }

        private SimVec3 _lastFlickPitch;

        /// <summary>Pitch-space direction of the latest right-stick flick / swipe (set by the session before Update).</summary>
        public void SetFlickDirection(SimVec3 pitchDir) => _lastFlickPitch = pitchDir;

        private bool ControlledIsKeeper(MatchSim sim) =>
            ControlledId == MatchSim.KeeperId(Team) && !(sim.State.Owner == ControlledId);

        private void RefreshLists(MatchSim sim)
        {
            _candidates.Clear();
            _teammates.Clear();
            int first = MatchSim.FirstId(Team);
            var cfg = sim.Config;
            for (int id = first; id < first + MatchSetup.PlayersPerTeam; id++)
            {
                ref readonly var p = ref sim.State.Players[id];
                if (p.SentOff) continue;
                var info = new ControlPlayerInfo(id, p.Position,
                    PlayerKinematics.SprintSpeed(sim.AttributesOf(id), cfg.Kinematics, p.Stamina));
                _teammates.Add(info);
                if (id != MatchSim.KeeperId(Team) && (_blocked & (1L << id)) == 0) _candidates.Add(info);
            }
        }

        /// <summary>Best initial player: the kick-off taker if we kick off, else the outfielder nearest the ball.</summary>
        public static int InitialPlayer(MatchSim sim, SimTeam team)
        {
            var s = sim.State;
            if (s.RestartTeam == team && s.RestartTakerId >= 0) return s.RestartTakerId;
            int first = MatchSim.FirstId(team);
            int best = first + 1;
            float bestD = float.MaxValue;
            for (int id = first + 1; id < first + MatchSetup.PlayersPerTeam; id++)
            {
                float d = SimMath.FlatDistance(s.Players[id].Position, s.Ball.Position);
                if (d < bestD) { bestD = d; best = id; }
            }
            return best;
        }
    }
}
