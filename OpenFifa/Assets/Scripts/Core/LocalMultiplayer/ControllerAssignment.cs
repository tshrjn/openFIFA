using System;
using System.Collections.Generic;

namespace OpenFifa.Core
{
    /// <summary>Where a controller sits on the side-select screen.</summary>
    public enum ControllerSide
    {
        /// <summary>Middle of the screen: connected but not playing (spectator / menu control only).</summary>
        Unassigned = 0,
        Home,
        Away,
    }

    /// <summary>Match mode implied by where the controllers are.</summary>
    public enum LocalMatchMode
    {
        /// <summary>Nobody has picked a side yet: cannot start.</summary>
        None = 0,
        /// <summary>One human vs the AI.</summary>
        SoloVsAI,
        /// <summary>Humans on both sides, one each.</summary>
        HeadToHead,
        /// <summary>Two (or more) humans on one side against the AI.</summary>
        CoopVsAI,
        /// <summary>Humans on both sides and at least one side has two.</summary>
        CoopHeadToHead,
    }

    /// <summary>One connected controller and the human slot it drives.</summary>
    public struct ControllerSeat
    {
        /// <summary>Platform device id (Input System device id). -1 while the seat waits for a reconnect.</summary>
        public int DeviceId;
        public ControllerSide Side;
        /// <summary>Stable 0-based slot (P1, P2, ...) that keeps its colour/indicator through reconnects.</summary>
        public int Slot;
        public bool Disconnected => DeviceId < 0;
    }

    /// <summary>
    /// Side-select and hot-plug logic for local multiplayer: controllers join in the middle, push the left stick
    /// left/right to pick Home/Away, and keep their slot (indicator colour, controlled player) if they drop out and
    /// come back mid-match. Pure logic; the Unity layer feeds it device connect/disconnect events and stick pushes.
    /// </summary>
    public sealed class ControllerAssignment
    {
        public const int MaxSeats = 4;
        public const int MaxPerSide = 2;

        private readonly List<ControllerSeat> _seats = new List<ControllerSeat>(MaxSeats);

        /// <summary>True between <see cref="LockForMatch"/> and <see cref="Unlock"/>: sides are fixed and a
        /// disconnect leaves a seat waiting for a controller instead of removing it.</summary>
        public bool InMatch { get; private set; }

        public IReadOnlyList<ControllerSeat> Seats => _seats;

        /// <summary>Raised when a seat in a live match loses its controller (pause the match and show the prompt).</summary>
        public event Action<ControllerSeat> SeatOrphaned;
        /// <summary>Raised when an orphaned seat gets a controller back.</summary>
        public event Action<ControllerSeat> SeatRestored;

        /// <summary>Any seat waiting for a controller: the match should stay paused.</summary>
        public bool AwaitingReconnect
        {
            get
            {
                for (int i = 0; i < _seats.Count; i++) if (_seats[i].Disconnected) return true;
                return false;
            }
        }

        /// <summary>
        /// A controller was connected (or pressed a button for the first time). Out of a match it joins in the middle;
        /// in a match it takes over the lowest orphaned seat, if any. Returns the seat slot, or -1 when it was ignored.
        /// </summary>
        public int Connect(int deviceId)
        {
            int existing = IndexOfDevice(deviceId);
            if (existing >= 0) return _seats[existing].Slot;

            if (InMatch)
            {
                int orphan = -1;
                for (int i = 0; i < _seats.Count; i++)
                    if (_seats[i].Disconnected && (orphan < 0 || _seats[i].Slot < _seats[orphan].Slot)) orphan = i;
                if (orphan < 0) return -1; // spectators cannot join a running match
                var seat = _seats[orphan];
                seat.DeviceId = deviceId;
                _seats[orphan] = seat;
                SeatRestored?.Invoke(seat);
                return seat.Slot;
            }

            if (_seats.Count >= MaxSeats) return -1;
            var s = new ControllerSeat { DeviceId = deviceId, Side = ControllerSide.Unassigned, Slot = FreeSlot() };
            _seats.Add(s);
            return s.Slot;
        }

        /// <summary>A controller was unplugged / lost its battery.</summary>
        public void Disconnect(int deviceId)
        {
            int i = IndexOfDevice(deviceId);
            if (i < 0) return;
            var seat = _seats[i];
            if (InMatch && seat.Side != ControllerSide.Unassigned)
            {
                seat.DeviceId = -1;
                _seats[i] = seat;
                SeatOrphaned?.Invoke(seat);
            }
            else
            {
                _seats.RemoveAt(i);
            }
        }

        /// <summary>
        /// Left stick pushed left (direction &lt; 0) or right (&gt; 0) on the side-select screen. Home is on the left.
        /// Moves one step (Away → middle → Home and back). A side that already has <see cref="MaxPerSide"/> humans
        /// refuses. Returns the new side.
        /// </summary>
        public ControllerSide Push(int deviceId, int direction)
        {
            int i = IndexOfDevice(deviceId);
            if (i < 0 || direction == 0) return ControllerSide.Unassigned;
            var seat = _seats[i];
            if (InMatch) return seat.Side;
            ControllerSide next = seat.Side;
            if (direction < 0)
                next = seat.Side == ControllerSide.Away ? ControllerSide.Unassigned : ControllerSide.Home;
            else
                next = seat.Side == ControllerSide.Home ? ControllerSide.Unassigned : ControllerSide.Away;
            if (next != ControllerSide.Unassigned && next != seat.Side && CountOn(next) >= MaxPerSide) return seat.Side;
            seat.Side = next;
            _seats[i] = seat;
            return next;
        }

        public int CountOn(ControllerSide side)
        {
            int n = 0;
            for (int i = 0; i < _seats.Count; i++) if (_seats[i].Side == side) n++;
            return n;
        }

        public LocalMatchMode Mode
        {
            get
            {
                int h = CountOn(ControllerSide.Home), a = CountOn(ControllerSide.Away);
                if (h == 0 && a == 0) return LocalMatchMode.None;
                if (h > 0 && a > 0) return h > 1 || a > 1 ? LocalMatchMode.CoopHeadToHead : LocalMatchMode.HeadToHead;
                return h + a > 1 ? LocalMatchMode.CoopVsAI : LocalMatchMode.SoloVsAI;
            }
        }

        public bool CanStart => Mode != LocalMatchMode.None;

        /// <summary>Start of the match: unassigned controllers stay connected as spectators but drive nobody.</summary>
        public void LockForMatch()
        {
            if (!CanStart) throw new InvalidOperationException("No controller has picked a side.");
            InMatch = true;
        }

        /// <summary>Back to menus: seats still waiting for a controller are dropped.</summary>
        public void Unlock()
        {
            InMatch = false;
            for (int i = _seats.Count - 1; i >= 0; i--) if (_seats[i].Disconnected) _seats.RemoveAt(i);
        }

        /// <summary>Seats playing for a team, in slot order (P1 before P2), for building the session's human slots.</summary>
        public void SeatsFor(SimTeam team, List<ControllerSeat> into)
        {
            into.Clear();
            var side = team == SimTeam.Home ? ControllerSide.Home : ControllerSide.Away;
            for (int i = 0; i < _seats.Count; i++) if (_seats[i].Side == side) into.Add(_seats[i]);
            into.Sort((x, y) => x.Slot.CompareTo(y.Slot));
        }

        private int IndexOfDevice(int deviceId)
        {
            if (deviceId < 0) return -1;
            for (int i = 0; i < _seats.Count; i++) if (_seats[i].DeviceId == deviceId) return i;
            return -1;
        }

        private int FreeSlot()
        {
            for (int slot = 0; slot < MaxSeats; slot++)
            {
                bool used = false;
                for (int i = 0; i < _seats.Count; i++) if (_seats[i].Slot == slot) { used = true; break; }
                if (!used) return slot;
            }
            return _seats.Count;
        }
    }
}
