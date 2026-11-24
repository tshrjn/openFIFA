namespace OpenFifa.Core
{
    /// <summary>
    /// Rolling record of the match sim's player and ball positions, one entry per sim tick seen, so the offside
    /// presentation can freeze on the moment the ball was played instead of the moment the offender touched it (by then
    /// the sim has already awarded the free kick). Each entry also keeps the sim's offside bookkeeping at that tick
    /// (<see cref="MatchSimState.OffsideMask"/>, passer, line positions); the pass moment for a decision is the first
    /// tick of the latest run in which the offender's bit was set by that pass. The sim is only read, never changed.
    /// Fixed capacity (oldest entries are overwritten), allocation-free after construction. Pure C#: EditMode testable.
    /// </summary>
    public sealed class OffsidePassMomentHistory
    {
        /// <summary>Ten seconds of 60 Hz ticks: longer than any pass in flight.</summary>
        public const int DefaultCapacity = 600;

        private readonly int _capacity;
        private readonly int[] _tick;
        private readonly int[] _mask;
        private readonly int[] _passer;
        private readonly float[] _defenderLine;
        private readonly float[] _ballLine;
        private readonly SimVec3[] _players; // _capacity * PlayerCount
        private readonly float[] _facing;    // radians, sim convention
        private readonly bool[] _sentOff;
        private readonly SimVec3[] _ball;
        private int _head = -1; // index of the newest entry
        private int _count;

        public OffsidePassMomentHistory(int capacity = DefaultCapacity)
        {
            _capacity = capacity < 2 ? 2 : capacity;
            _tick = new int[_capacity];
            _mask = new int[_capacity];
            _passer = new int[_capacity];
            _defenderLine = new float[_capacity];
            _ballLine = new float[_capacity];
            _players = new SimVec3[_capacity * MatchSimState.PlayerCount];
            _facing = new float[_capacity * MatchSimState.PlayerCount];
            _sentOff = new bool[_capacity * MatchSimState.PlayerCount];
            _ball = new SimVec3[_capacity];
        }

        public int Count => _count;
        public int Capacity => _capacity;

        public void Clear()
        {
            _head = -1;
            _count = 0;
        }

        /// <summary>Records the state's current tick. A tick already recorded (no sim step since) is ignored.</summary>
        public void Record(MatchSimState s)
        {
            if (s == null) return;
            if (_count > 0 && _tick[_head] == s.Tick) return;
            if (_count > 0 && s.Tick < _tick[_head]) Clear(); // a new match (or a rewind): start over

            _head = (_head + 1) % _capacity;
            if (_count < _capacity) _count++;
            _tick[_head] = s.Tick;
            _mask[_head] = s.OffsideMask;
            _passer[_head] = s.OffsidePasser;
            _defenderLine[_head] = s.OffsideDefenderLineZ;
            _ballLine[_head] = s.OffsideBallLineZ;
            int b = _head * MatchSimState.PlayerCount;
            for (int i = 0; i < MatchSimState.PlayerCount; i++)
            {
                _players[b + i] = s.Players[i].Position;
                _facing[b + i] = s.Players[i].Facing;
                _sentOff[b + i] = s.Players[i].SentOff;
            }
            _ball[_head] = s.Ball.Position;
        }

        /// <summary>
        /// Finds the entry for the moment the ball was played in <paramref name="decision"/>: searching back from the
        /// newest entry, the first tick of the latest run where the offender was flagged by that pass (same passer and the
        /// same defender and ball lines). False when the history holds no such tick (e.g. it started after the pass).
        /// <paramref name="slot"/> is a snapshot handle for <see cref="GetPlayer"/> / <see cref="GetBall"/>.
        /// </summary>
        public bool TryFindPassMoment(OffsideDecision decision, out int slot, out int tick)
        {
            slot = -1;
            tick = -1;
            if (decision == null || !decision.IsOffence || _count == 0) return false;
            int id = decision.OffenderId;
            if (id < 0 || id >= MatchSimState.PlayerCount) return false;

            bool inRun = false;
            for (int k = 0; k < _count; k++)
            {
                int i = (_head - k + _capacity) % _capacity;
                bool match = (_mask[i] & (1 << id)) != 0 && _passer[i] == decision.PasserId
                             && _defenderLine[i] == decision.DefenderLineX && _ballLine[i] == decision.BallLineX;
                if (match)
                {
                    inRun = true;
                    slot = i;
                    tick = _tick[i];
                }
                else if (inRun)
                {
                    break; // the tick before the pass: the run's first entry is the pass moment
                }
            }
            return inRun;
        }

        /// <summary>Tick of the entry at <paramref name="slot"/>.</summary>
        public int TickAt(int slot) => _tick[slot];

        public void GetPlayer(int slot, int playerId, out SimVec3 position, out float facing, out bool sentOff)
        {
            int i = slot * MatchSimState.PlayerCount + playerId;
            position = _players[i];
            facing = _facing[i];
            sentOff = _sentOff[i];
        }

        public SimVec3 GetBall(int slot) => _ball[slot];
    }
}
