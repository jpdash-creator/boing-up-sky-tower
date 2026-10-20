using System;

namespace BoingUpSkyTower.Simulation
{
    /// <summary>
    /// Q16.16 fixed-point number. All simulation and generator math uses this type so results are
    /// identical on every device and on the server (no floats in the sim). Range about +-32768 units.
    /// </summary>
    public readonly struct Fx : IEquatable<Fx>, IComparable<Fx>
    {
        public const int Shift = 16;
        public const int OneRaw = 1 << Shift;

        public readonly int Raw;

        private Fx(int raw) { Raw = raw; }

        public static readonly Fx Zero = new Fx(0);
        public static readonly Fx One = new Fx(OneRaw);
        public static readonly Fx Half = new Fx(OneRaw / 2);

        public static Fx FromRaw(int raw) { return new Fx(raw); }
        public static Fx FromInt(int value) { return new Fx(value << Shift); }
        public static Fx FromRatio(int numerator, int denominator)
        {
            return new Fx((int)(((long)numerator << Shift) / denominator));
        }

        public static Fx operator +(Fx a, Fx b) { return new Fx(a.Raw + b.Raw); }
        public static Fx operator -(Fx a, Fx b) { return new Fx(a.Raw - b.Raw); }
        public static Fx operator -(Fx a) { return new Fx(-a.Raw); }
        public static Fx operator *(Fx a, Fx b) { return new Fx((int)(((long)a.Raw * b.Raw) >> Shift)); }
        public static Fx operator /(Fx a, Fx b) { return new Fx((int)(((long)a.Raw << Shift) / b.Raw)); }
        public static Fx operator *(Fx a, int b) { return new Fx(a.Raw * b); }
        public static Fx operator /(Fx a, int b) { return new Fx(a.Raw / b); }

        public static bool operator <(Fx a, Fx b) { return a.Raw < b.Raw; }
        public static bool operator >(Fx a, Fx b) { return a.Raw > b.Raw; }
        public static bool operator <=(Fx a, Fx b) { return a.Raw <= b.Raw; }
        public static bool operator >=(Fx a, Fx b) { return a.Raw >= b.Raw; }
        public static bool operator ==(Fx a, Fx b) { return a.Raw == b.Raw; }
        public static bool operator !=(Fx a, Fx b) { return a.Raw != b.Raw; }

        public static Fx Abs(Fx a) { return a.Raw < 0 ? new Fx(-a.Raw) : a; }
        public static Fx Min(Fx a, Fx b) { return a.Raw <= b.Raw ? a : b; }
        public static Fx Max(Fx a, Fx b) { return a.Raw >= b.Raw ? a : b; }
        public static Fx Clamp(Fx v, Fx lo, Fx hi) { return v < lo ? lo : (v > hi ? hi : v); }
        public static Fx Lerp(Fx a, Fx b, Fx t) { return a + (b - a) * t; }

        /// <summary>Rounds toward negative infinity.</summary>
        public int FloorToInt() { return Raw >> Shift; }
        public int Sign() { return Raw > 0 ? 1 : (Raw < 0 ? -1 : 0); }

        /// <summary>Integer square root in fixed point; returns zero for non-positive input.</summary>
        public static Fx Sqrt(Fx a)
        {
            if (a.Raw <= 0) return Zero;
            long n = (long)a.Raw << Shift;
            long x = n;
            long y = (x + 1) >> 1;
            while (y < x)
            {
                x = y;
                y = (x + n / x) >> 1;
            }
            return new Fx((int)x);
        }

        public bool Equals(Fx other) { return Raw == other.Raw; }
        public override bool Equals(object obj) { return obj is Fx && Equals((Fx)obj); }
        public override int GetHashCode() { return Raw; }
        public int CompareTo(Fx other) { return Raw.CompareTo(other.Raw); }

        /// <summary>Debug text using integer math only (4 decimals).</summary>
        public override string ToString()
        {
            long raw = Raw;
            string sign = raw < 0 ? "-" : "";
            if (raw < 0) raw = -raw;
            long whole = raw >> Shift;
            long frac = ((raw & (OneRaw - 1)) * 10000) >> Shift;
            return sign + whole + "." + frac.ToString("D4");
        }
    }
}
