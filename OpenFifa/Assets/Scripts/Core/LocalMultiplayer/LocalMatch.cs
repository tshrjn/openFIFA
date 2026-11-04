using System;
using System.Collections.Generic;

namespace OpenFifa.Core
{
    /// <summary>
    /// A local multiplayer match: builds a <see cref="SinglePlayerMatchSession"/> with one human seat per controller on a
    /// side (<see cref="ControllerAssignment"/>), sets the AI levels for the mode, and handles controllers dropping out
    /// (the seat's player goes to the AI and the match pauses until a controller takes the seat back).
    /// </summary>
    public sealed class LocalMatch
    {
        public readonly SinglePlayerMatchSession Session;
        public readonly ControllerAssignment Controllers;
        public readonly LocalMatchMode Mode;

        private readonly Dictionary<int, HumanSlot> _slotBySeat = new Dictionary<int, HumanSlot>();
        private readonly Dictionary<int, IHumanInputSource> _inputBySeat = new Dictionary<int, IHumanInputSource>();
        private readonly Func<ControllerSeat, IHumanInputSource> _inputFor;

        /// <summary>Builds the session from <paramref name="options"/> (HumanTeam is overwritten with
        /// <see cref="PrimaryTeam"/>) and seats every controller.</summary>
        /// <param name="inputFor">Builds the input for a seat (reads that seat's pad). Called again when a seat is
        /// taken over by another pad.</param>
        public LocalMatch(SinglePlayerMatchOptions options, ControllerAssignment controllers,
            Func<ControllerSeat, IHumanInputSource> inputFor)
            : this(NewSession(options, controllers), controllers, inputFor)
        {
        }

        /// <summary>Seats every controller in an existing session whose HumanTeam is <see cref="PrimaryTeam"/>
        /// (the scene's match controller builds the session; this adds the other seats).</summary>
        public LocalMatch(SinglePlayerMatchSession session, ControllerAssignment controllers,
            Func<ControllerSeat, IHumanInputSource> inputFor)
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
            Controllers = controllers ?? throw new ArgumentNullException(nameof(controllers));
            _inputFor = inputFor ?? throw new ArgumentNullException(nameof(inputFor));
            Mode = controllers.Mode;
            if (Mode == LocalMatchMode.None) throw new InvalidOperationException("No controller has picked a side.");
            if (session.HumanTeam != PrimaryTeam(controllers))
                throw new ArgumentException($"Session HumanTeam must be {PrimaryTeam(controllers)}.", nameof(session));

            var home = new List<ControllerSeat>();
            var away = new List<ControllerSeat>();
            controllers.SeatsFor(SimTeam.Home, home);
            controllers.SeatsFor(SimTeam.Away, away);
            bool homeFirst = session.HumanTeam == SimTeam.Home;
            var primary = homeFirst ? home : away;
            var other = homeFirst ? away : home;
            Bind(primary[0], Session.HumanSlots[0]);
            for (int i = 1; i < primary.Count; i++) Bind(primary[i], Session.AddHumanSlot(session.HumanTeam));
            for (int i = 0; i < other.Count; i++) Bind(other[i], Session.AddHumanSlot(MatchSim.Opponent(session.HumanTeam)));

            if (home.Count > 0 && away.Count > 0) MakeFair(Session, Session.Options);

            controllers.LockForMatch();
            controllers.SeatOrphaned += OnOrphaned;
            controllers.SeatRestored += OnRestored;
        }

        /// <summary>The session's user team: the side of the lowest-numbered seated controller (P1's side).</summary>
        public static SimTeam PrimaryTeam(ControllerAssignment controllers)
        {
            int best = int.MaxValue;
            var team = SimTeam.None;
            foreach (var seat in controllers.Seats)
            {
                if (seat.Side == ControllerSide.Unassigned || seat.Slot >= best) continue;
                best = seat.Slot;
                team = seat.Side == ControllerSide.Home ? SimTeam.Home : SimTeam.Away;
            }
            return team;
        }

        private static SinglePlayerMatchSession NewSession(SinglePlayerMatchOptions options, ControllerAssignment controllers)
        {
            if (controllers == null) throw new ArgumentNullException(nameof(controllers));
            if (controllers.Mode == LocalMatchMode.None) throw new InvalidOperationException("No controller has picked a side.");
            options = options ?? new SinglePlayerMatchOptions();
            options.HumanTeam = PrimaryTeam(controllers);
            return new SinglePlayerMatchSession(options);
        }

        /// <summary>The session seat a controller slot drives.</summary>
        public HumanSlot SlotFor(int seatSlot) => _slotBySeat.TryGetValue(seatSlot, out var s) ? s : null;

        /// <summary>Back to menus: unhook from the controller events and unlock sides.</summary>
        public void End()
        {
            Controllers.SeatOrphaned -= OnOrphaned;
            Controllers.SeatRestored -= OnRestored;
            Controllers.Unlock();
        }

        private void Bind(ControllerSeat seat, HumanSlot slot)
        {
            var input = _inputFor(seat);
            slot.Input = input;
            _slotBySeat[seat.Slot] = slot;
            _inputBySeat[seat.Slot] = input;
        }

        private void OnOrphaned(ControllerSeat seat)
        {
            if (!_slotBySeat.TryGetValue(seat.Slot, out var slot)) return;
            slot.Input = null; // the AI plays this seat's player until someone picks the pad up
            Session.Pause();
        }

        private void OnRestored(ControllerSeat seat)
        {
            if (!_slotBySeat.TryGetValue(seat.Slot, out var slot)) return;
            var input = _inputFor(seat);
            _inputBySeat[seat.Slot] = input;
            slot.Input = input;
            if (!Controllers.AwaitingReconnect) Session.Resume();
        }

        /// <summary>
        /// Humans on both sides: neither team is "the AI opponent", so both teams' AI teammates play at the level's
        /// teammate strength and every player keeps the real ratings (no keeper or outfield difficulty deltas).
        /// </summary>
        public static void MakeFair(SinglePlayerMatchSession session, SinglePlayerMatchOptions options)
        {
            var mate = session.Profile.Ladder.TeammateDifficulty;
            session.Director.Brains[0].SetDifficulty(mate);
            session.Director.Brains[1].SetDifficulty(mate);
            var original = options.Setup ?? MatchSetup.CreateDefault(options.Seed);
            var live = session.Sim.Setup;
            for (int i = 0; i < MatchSetup.PlayersPerTeam; i++)
            {
                // Fresh copies: the sim reads attributes through the setup's arrays, and the caller's objects stay untouched.
                if (original.Home[i] != null) live.Home[i] = original.Home[i].Clone();
                if (original.Away[i] != null) live.Away[i] = original.Away[i].Clone();
            }
            session.AI.RefreshRatings();
        }
    }
}
