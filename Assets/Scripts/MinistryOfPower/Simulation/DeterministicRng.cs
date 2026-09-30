namespace MinistryOfPower.Simulation
{
    /// <summary>
    /// xorshift32 RNG so day resolution stays reproducible for a given seed + day index.
    /// </summary>
    public struct DeterministicRng
    {
        private uint _state;

        public DeterministicRng(int seed)
        {
            _state = seed == 0 ? 0xA341316Cu : (uint)seed;
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

        public float NextFloat01()
        {
            return (NextUInt() & 0x00FFFFFFu) / 16777215f;
        }

        public float NextRange(float minInclusive, float maxInclusive)
        {
            return minInclusive + (maxInclusive - minInclusive) * NextFloat01();
        }

        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
            {
                return minInclusive;
            }

            uint span = (uint)(maxExclusive - minInclusive);
            return minInclusive + (int)(NextUInt() % span);
        }
    }
}
