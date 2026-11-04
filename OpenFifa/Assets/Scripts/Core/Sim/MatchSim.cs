using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace OpenFifa.Core
{
    /// <summary>
    /// Deterministic fixed-step 11v11 match simulation. Call <see cref="Step"/> once per tick with one
    /// <see cref="PlayerIntent"/> per player (index = player id; Home 0-10, Away 11-21, slot 0 = keeper).
    /// Same config + setup + seed + intents gives bit-identical state and events on the same runtime.
    /// The presentation layer reads <see cref="State"/> and the event list and never writes back.
    /// Determinism is per runtime: the sim uses IEEE float math plus System.Math (Sqrt, Exp, Sin, Cos, Atan2, Acos),
    /// whose last-bit results can differ between Mono and IL2CPP, between JIT/AOT code paths and between CPU
    /// architectures (x64 vs ARM64). Intent-only replays and lockstep netplay are exact only on the same build and
    /// platform; to share replays across devices store periodic snapshots (<see cref="CaptureSnapshot"/>) and
    /// re-sync from them rather than trusting re-simulation alone. Possession ties are broken with the seeded RNG,
    /// never by player order.
    /// </summary>
    public sealed partial class MatchSim
    {
        private readonly MatchSimConfig _cfg;
        private readonly MatchSetup _setup;
        private readonly List<MatchEvent> _events = new List<MatchEvent>(32);
        private readonly float[] _scratchCoords = new float[MatchSetup.PlayersPerTeam];
        private readonly int[] _scratchContacts = new int[MatchSimState.PlayerCount];
        private MatchSimState _s;

        public MatchSim(MatchSimConfig config, MatchSetup setup)
        {
            _cfg = config ?? throw new ArgumentNullException(nameof(config));
            _setup = setup ?? throw new ArgumentNullException(nameof(setup));
            if (setup.Home.Length != MatchSetup.PlayersPerTeam || setup.Away.Length != MatchSetup.PlayersPerTeam)
                throw new ArgumentException("Each squad needs exactly 11 players.");

            _s = new MatchSimState { Rng = new SimRng(setup.Seed) };
            for (int i = 0; i < MatchSimState.PlayerCount; i++)
            {
                if (AttributesOf(i) == null) throw new ArgumentException($"Missing attributes for player {i}.");
                _s.Players[i] = new SimPlayer
                {
                    Id = i,
                    Team = TeamOf(i),
                    Stamina = 1f,
                    PendingTarget = -1,
                };
            }

            BeginKickOff(setup.FirstKickOff);
        }

        public MatchSimConfig Config => _cfg;
        public MatchSetup Setup => _setup;

        /// <summary>Live state. Treat as read-only outside the sim.</summary>
        public MatchSimState State => _s;

        /// <summary>Events emitted by the most recent <see cref="Step"/>.</summary>
        public IReadOnlyList<MatchEvent> Events => _events;

        public int Tick => _s.Tick;
        public SimMatchPhase Phase => _s.Phase;
        public float Dt => _cfg.Dt;

        public static SimTeam TeamOf(int id) => id < MatchSetup.PlayersPerTeam ? SimTeam.Home : SimTeam.Away;
        public static SimTeam Opponent(SimTeam t) => t == SimTeam.Home ? SimTeam.Away : (t == SimTeam.Away ? SimTeam.Home : SimTeam.None);
        public static int FirstId(SimTeam t) => t == SimTeam.Home ? 0 : MatchSetup.PlayersPerTeam;
        public static int KeeperId(SimTeam t) => FirstId(t);

        public SimPlayerAttributes AttributesOf(int id) =>
            id < MatchSetup.PlayersPerTeam ? _setup.Home[id] : _setup.Away[id - MatchSetup.PlayersPerTeam];

        /// <summary>+1 if the team attacks the +Z goal in the current half, -1 otherwise.</summary>
        public int AttackSign(SimTeam team)
        {
            int s = team == SimTeam.Home ? 1 : -1;
            return _s.Half == 1 ? s : -s;
        }

        /// <summary>Distance along the team's attacking direction (halfway = 0, opponents' goal line = +HalfLength).</summary>
        public float AttackCoord(SimTeam team, SimVec3 p) => p.Z * AttackSign(team);

        /// <summary>Advances the match by one tick.</summary>
        public IReadOnlyList<MatchEvent> Step(PlayerIntent[] intents)
        {
            _events.Clear();
            if (_s.Phase == SimMatchPhase.FullTime) return _events;

            _s.Tick++;
            if (_s.Phase == SimMatchPhase.HalfTime)
            {
                if (_s.Tick >= _s.PhaseEndTick) StartSecondHalf();
                return _events;
            }

            _s.Clock += _cfg.Dt;
            if (_s.Phase == SimMatchPhase.Stoppage && _s.Tick >= _s.PhaseEndTick) SetupRestart();

            UpdatePlayers(intents);
            SeparatePlayers();
            if (_s.Phase == SimMatchPhase.AwaitingRestart) EnforceRestartPositions();
            ResolveContacts();
            if (_s.Phase == SimMatchPhase.InPlay) UpdateDribble();
            StepBall();
            if (_s.Phase == SimMatchPhase.InPlay)
            {
                UpdatePossession();
                CheckBallOutOfPlay();
            }

            if (_s.PossessionTeam == SimTeam.Home) _s.HomePossessionTicks++;
            else if (_s.PossessionTeam == SimTeam.Away) _s.AwayPossessionTicks++;

            CheckClock();
            return _events;
        }

        /// <summary>Deep copy of the full state (for replays, rewinds, rollback).</summary>
        public MatchSimState CaptureSnapshot() => _s.Clone();

        /// <summary>Restores a snapshot taken from a sim with the same config and setup.</summary>
        public void RestoreSnapshot(MatchSimState snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            _s = snapshot.Clone();
            _events.Clear();
        }

        /// <summary>
        /// Ball state after <paramref name="seconds"/> of free flight (no players), for AI interception. Allocation-free.
        /// Ground-pass timing and speed helpers for the AI are in <see cref="BallFlight"/>.
        /// </summary>
        public SimBall PredictBall(float seconds)
        {
            int steps = (int)(seconds / _cfg.BallDt + 0.5f);
            return BallIntegrator.Predict(_s.Ball, _cfg.Ball, _cfg.Pitch, _cfg.BallDt, steps);
        }

        /// <summary>64-bit FNV-1a hash over every field of the state, bit-exact for floats.</summary>
        public ulong ComputeStateHash()
        {
            var h = new StateHasher();
            h.Add(_s.Tick); h.Add(_s.Half); h.Add(_s.Clock); h.Add((int)_s.Phase); h.Add(_s.PhaseEndTick);
            h.Add((int)_s.PendingRestart); h.Add((int)_s.RestartTeam); h.Add(_s.RestartSpot); h.Add(_s.RestartTakerId);
            h.Add((int)_s.LastKickRestart); h.Add(_s.HomeScore); h.Add(_s.AwayScore);
            h.Add(_s.Ball.Position); h.Add(_s.Ball.Velocity); h.Add(_s.Ball.Spin); h.Add(_s.Ball.InNet); h.Add(_s.Ball.Grounded ? 1 : 0);
            h.Add((long)_s.Rng.State); h.Add((long)_s.Rng.Increment);
            h.Add(_s.Owner); h.Add(_s.KeeperHolding ? 1 : 0); h.Add(_s.LastTouchPlayer); h.Add((int)_s.LastTouchTeam);
            h.Add((int)_s.PossessionTeam); h.Add(_s.OffsideMask); h.Add(_s.OffsidePasser); h.Add(_s.OffsideDefenderLineZ); h.Add(_s.OffsideBallLineZ);
            h.Add(_s.PassLive ? 1 : 0); h.Add(_s.PassFrom); h.Add(_s.PassTarget); h.Add((int)_s.PassTeam); h.Add(_s.AssistCandidate);
            h.Add(_s.ShotLive ? 1 : 0); h.Add(_s.Shooter); h.Add(_s.PenaltyKicker); h.Add(_s.PenaltyKickTick);
            h.Add(_s.IndirectRestartLive ? 1 : 0); h.Add(_s.KickSerial); h.Add(_s.HomeSaveSerial); h.Add(_s.AwaySaveSerial);
            h.Add(_s.HomeShots); h.Add(_s.AwayShots); h.Add(_s.HomePossessionTicks); h.Add(_s.AwayPossessionTicks);
            for (int i = 0; i < MatchSimState.PlayerCount; i++)
            {
                ref SimPlayer p = ref _s.Players[i];
                h.Add(p.Position); h.Add(p.Velocity); h.Add(p.Facing); h.Add(p.Stamina); h.Add((int)p.ActionState);
                h.Add((int)p.PendingAction); h.Add(p.PendingPower); h.Add(p.PendingAim); h.Add(p.PendingTarget); h.Add((int)p.PendingStyle);
                h.Add(p.ContactTick); h.Add(p.StateEndTick); h.Add(p.ControlBlockedUntilTick); h.Add(p.NextTouchTick);
                h.Add(p.LastTouchTick); h.Add(p.LastTouchYaw); h.Add(p.Jockeying ? 1 : 0); h.Add(p.Shielding ? 1 : 0); h.Add(p.Sprinting ? 1 : 0);
                h.Add(p.YellowCards); h.Add(p.SentOff ? 1 : 0);
                h.Add(_s.OffsidePositions[i]); h.Add(_s.OffsideMargins[i]);
            }

            return h.Value;
        }

        private void Emit(MatchEventType type, int player, int other, SimTeam team, SimVec3 pos,
            float speed = 0f, float value = 0f, int detail = 0, IntentAction action = IntentAction.None,
            KickStyle style = KickStyle.Default, float defenderLineZ = 0f, float ballLineZ = 0f)
        {
            _events.Add(new MatchEvent
            {
                Type = type,
                Tick = _s.Tick,
                Time = _s.Clock,
                Half = _s.Half,
                PlayerId = player,
                OtherPlayerId = other,
                Team = team,
                Position = pos,
                Speed = speed,
                Value = value,
                Detail = detail,
                Action = action,
                Style = style,
                DefenderLineZ = defenderLineZ,
                BallLineZ = ballLineZ,
            });
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct FloatBits
        {
            [FieldOffset(0)] public float F;
            [FieldOffset(0)] public int I;
        }

        private struct StateHasher
        {
            private ulong _h;
            private bool _init;

            public ulong Value => _init ? _h : 14695981039346656037UL;

            public void Add(long v)
            {
                if (!_init) { _h = 14695981039346656037UL; _init = true; }
                for (int i = 0; i < 8; i++)
                {
                    _h ^= (ulong)((v >> (i * 8)) & 0xFF);
                    _h = unchecked(_h * 1099511628211UL);
                }
            }

            public void Add(int v) => Add((long)v);
            public void Add(float f) => Add(new FloatBits { F = f }.I);
            public void Add(SimVec3 v) { Add(v.X); Add(v.Y); Add(v.Z); }
        }
    }
}
