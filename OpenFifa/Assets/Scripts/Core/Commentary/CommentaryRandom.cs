namespace OpenFifa.Core
{
    /// <summary>Small seeded xorshift32 generator so commentary choices replay identically for a given seed.</summary>
    public sealed class CommentaryRandom
    {
        private uint _state;

        public CommentaryRandom(uint seed)
        {
            _state = seed == 0 ? 0x9E3779B9u : seed;
        }

        public uint NextUInt()
        {
            uint x = _state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _state = x;
            return x;
        }

        /// <summary>Uniform-ish integer in [0, maxExclusive).</summary>
        public int Next(int maxExclusive) => maxExclusive <= 1 ? 0 : (int)(NextUInt() % (uint)maxExclusive);

        /// <summary>Value in [0, 1).</summary>
        public double NextDouble() => (NextUInt() >> 8) * (1.0 / 16777216.0);
    }
}
