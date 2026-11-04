namespace OpenFifa.Core
{
    /// <summary>
    /// Seeded, allocation-free PRNG (xorshift64*). Same seed, same sequence on every platform.
    /// </summary>
    public sealed class TeamAIRandom
    {
        private ulong _state;

        public TeamAIRandom(ulong seed)
        {
            Reseed(seed);
        }

        public void Reseed(ulong seed)
        {
            // SplitMix64 scramble so small seeds still give good streams; never zero.
            ulong z = seed + 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            z ^= z >> 31;
            _state = z == 0 ? 0x2545F4914F6CDD1DUL : z;
        }

        public ulong NextULong()
        {
            ulong x = _state;
            x ^= x >> 12;
            x ^= x << 25;
            x ^= x >> 27;
            _state = x;
            return x * 0x2545F4914F6CDD1DUL;
        }

        /// <summary>Uniform in [0, 1).</summary>
        public float NextFloat() => (NextULong() >> 40) * (1f / 16777216f);

        /// <summary>Uniform in [-1, 1).</summary>
        public float NextSigned() => NextFloat() * 2f - 1f;

        public float Range(float min, float max) => min + (max - min) * NextFloat();

        /// <summary>Approximate standard normal (Irwin-Hall, 4 draws), clamped to about +-3.5.</summary>
        public float NextGaussian()
        {
            float s = NextFloat() + NextFloat() + NextFloat() + NextFloat();
            return (s - 2f) * 1.7320508f;
        }
    }
}
