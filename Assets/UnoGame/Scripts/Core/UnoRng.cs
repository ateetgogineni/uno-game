namespace UnoGame.Core
{
    /// <summary>mulberry32, bit-for-bit identical to makeRng() in Server/src/engine.js.</summary>
    public sealed class UnoRng
    {
        uint a;

        public UnoRng(int seed)
        {
            a = unchecked((uint)seed);
        }

        public double Next()
        {
            unchecked
            {
                a += 0x6D2B79F5u;
                uint t = (a ^ (a >> 15)) * (1u | a);
                t = (t + ((t ^ (t >> 7)) * (61u | t))) ^ t;
                return (t ^ (t >> 14)) / 4294967296.0;
            }
        }

        public int Range(int n) => (int)System.Math.Floor(Next() * n);

        /// <summary>Internal state, so a saved game continues with exactly the same draws.</summary>
        public uint State { get => a; set => a = value; }
    }
}
