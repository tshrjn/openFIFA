using System;

namespace OpenFifa.Core
{
    /// <summary>
    /// Deterministic PCG32 random number generator (O'Neill, pcg-random.org, XSH-RR variant).
    /// The whole state is two ulongs, so it snapshots and restores exactly. Never use System.Random in the sim.
    /// </summary>
    public sealed class SimRng
    {
        private const ulong Multiplier = 6364136223846793005UL;

        private ulong _state;
        private ulong _inc;

        public SimRng(ulong seed, ulong stream = 54UL)
        {
            _state = 0UL;
            _inc = (stream << 1) | 1UL;
            NextUInt();
            _state += seed;
            NextUInt();
        }

        private SimRng(ulong state, ulong inc, bool raw)
        {
            _state = state;
            _inc = inc;
        }

        public ulong State => _state;
        public ulong Increment => _inc;

        /// <summary>Recreates a generator from a captured State/Increment pair.</summary>
        public static SimRng FromRaw(ulong state, ulong increment) => new SimRng(state, increment, true);

        public SimRng Clone() => new SimRng(_state, _inc, true);

        public uint NextUInt()
        {
            ulong old = _state;
            _state = unchecked(old * Multiplier + _inc);
            uint xorShifted = (uint)(((old >> 18) ^ old) >> 27);
            int rot = (int)(old >> 59);
            return (xorShifted >> rot) | (xorShifted << ((-rot) & 31));
        }

        /// <summary>Uniform float in [0, 1). Uses the top 24 bits, so every value is exact in float.</summary>
        public float NextFloat() => (NextUInt() >> 8) * (1f / 16777216f);

        /// <summary>Uniform float in [min, max).</summary>
        public float Range(float min, float max) => min + (max - min) * NextFloat();

        /// <summary>Uniform int in [minInclusive, maxExclusive).</summary>
        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            uint span = (uint)(maxExclusive - minInclusive);
            return minInclusive + (int)(NextUInt() % span);
        }

        /// <summary>True with probability p.</summary>
        public bool Chance(float p) => NextFloat() < p;

        /// <summary>
        /// Approximately normal sample (mean 0, sd 1) from the sum of four uniforms (Irwin-Hall),
        /// clipped to +-3.46 sd. Avoids Math.Log/Math.Cos so results do not depend on libm.
        /// </summary>
        public float NextGaussian()
        {
            float s = NextFloat() + NextFloat() + NextFloat() + NextFloat();
            // Irwin-Hall(4): mean 2, variance 4/12. Scale to unit variance.
            return (s - 2f) * 1.7320508f;
        }
    }
}
