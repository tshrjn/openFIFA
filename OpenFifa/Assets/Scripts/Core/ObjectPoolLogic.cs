using System.Text;

namespace OpenFifa.Core
{
    /// <summary>
    /// Pure C# object pool logic using index-based tracking.
    /// Pre-allocates all slots on construction. Zero GC during Get/Return.
    /// No Unity dependency — fully testable in EditMode.
    /// </summary>
    public class ObjectPoolLogic
    {
        private readonly int _capacity;
        private readonly int[] _availableStack;
        private int _stackTop;
        private int _activeCount;

        /// <summary>Total pool capacity.</summary>
        public int Capacity => _capacity;

        /// <summary>Number of currently available (inactive) items.</summary>
        public int AvailableCount => _stackTop;

        /// <summary>Number of currently active (in-use) items.</summary>
        public int ActiveCount => _activeCount;

        public ObjectPoolLogic(int capacity)
        {
            _capacity = capacity;
            _availableStack = new int[capacity];
            _stackTop = capacity;
            _activeCount = 0;

            // Pre-fill stack with all indices
            for (int i = 0; i < capacity; i++)
            {
                _availableStack[i] = i;
            }
        }

        /// <summary>
        /// Get an item index from the pool. Returns -1 if exhausted.
        /// </summary>
        public int Get()
        {
            if (_stackTop <= 0) return -1;

            _stackTop--;
            _activeCount++;
            return _availableStack[_stackTop];
        }

        /// <summary>
        /// Return an item index to the pool.
        /// </summary>
        public void Return(int index)
        {
            if (index < 0 || index >= _capacity) return;
            if (_stackTop >= _capacity) return;

            _availableStack[_stackTop] = index;
            _stackTop++;
            _activeCount--;
        }
    }

    /// <summary>
    /// Cached StringBuilder for GC-free string formatting in Update loops.
    /// Reuses a single StringBuilder instance.
    /// </summary>
    public class StringBuilderCache
    {
        private readonly StringBuilder _sb;

        public StringBuilderCache()
        {
            _sb = new StringBuilder(64);
        }

        /// <summary>
        /// Format timer as MM:SS without string allocation (beyond the result).
        /// </summary>
        public string FormatTimer(float remainingSeconds)
        {
            int totalSeconds = (int)remainingSeconds;
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;

            _sb.Clear();
            if (minutes < 10) _sb.Append('0');
            _sb.Append(minutes);
            _sb.Append(':');
            if (seconds < 10) _sb.Append('0');
            _sb.Append(seconds);

            return _sb.ToString();
        }

        /// <summary>
        /// Format score display without string concatenation.
        /// </summary>
        public string FormatScore(string teamA, int scoreA, string teamB, int scoreB)
        {
            _sb.Clear();
            _sb.Append(teamA);
            _sb.Append(' ');
            _sb.Append(scoreA);
            _sb.Append(" - ");
            _sb.Append(scoreB);
            _sb.Append(' ');
            _sb.Append(teamB);

            return _sb.ToString();
        }
    }
}
