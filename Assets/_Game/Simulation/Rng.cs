namespace SkyHopper.Simulation
{
    /// <summary>
    /// Seeded xoshiro128** generator, integer-only, identical on every platform. State is seeded
    /// from a 32-bit seed through SplitMix32.
    /// </summary>
    public struct Rng
    {
        private uint s0, s1, s2, s3;

        public Rng(int seed)
        {
            uint z = (uint)seed;
            s0 = SplitMix(ref z);
            s1 = SplitMix(ref z);
            s2 = SplitMix(ref z);
            s3 = SplitMix(ref z);
            if ((s0 | s1 | s2 | s3) == 0) s0 = 1;
        }

        private static uint SplitMix(ref uint state)
        {
            state += 0x9E3779B9u;
            uint z = state;
            z = (z ^ (z >> 16)) * 0x85EBCA6Bu;
            z = (z ^ (z >> 13)) * 0xC2B2AE35u;
            return z ^ (z >> 16);
        }

        private static uint Rotl(uint x, int k) { return (x << k) | (x >> (32 - k)); }

        public uint NextUInt()
        {
            uint result = Rotl(s1 * 5, 7) * 9;
            uint t = s1 << 9;
            s2 ^= s0;
            s3 ^= s1;
            s1 ^= s2;
            s0 ^= s3;
            s2 ^= t;
            s3 = Rotl(s3, 11);
            return result;
        }

        /// <summary>Uniform integer in [0, maxExclusive).</summary>
        public int Next(int maxExclusive)
        {
            return (int)(((ulong)NextUInt() * (ulong)maxExclusive) >> 32);
        }

        /// <summary>Uniform integer in [min, maxInclusive].</summary>
        public int Range(int min, int maxInclusive)
        {
            return min + Next(maxInclusive - min + 1);
        }

        /// <summary>Uniform fixed-point value in [lo, hi).</summary>
        public Fx Range(Fx lo, Fx hi)
        {
            Fx t = Fx.FromRaw((int)(NextUInt() >> 16));
            return lo + (hi - lo) * t;
        }
    }
}
