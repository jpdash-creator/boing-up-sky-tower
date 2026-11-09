namespace FluffyClimbers.Simulation
{
    /// <summary>
    /// Which side of the run HUD a power button sits on (owner Bug List 6, 2026-11-09). Pure C#, no Unity types.
    /// LEFT group = Flight, Rocket, Double Jump. RIGHT group = Magnet, Shield, Slow Time, Wall Stick.
    /// The rule is by POWER TYPE and holds for bought power-ups and for the free hero powers alike: a free hero power sits in the group of its own type,
    /// merged into the same button as the bought power-up of that type. Left and right swap with the left-handed layout option (done by the HUD placement).
    /// </summary>
    public static class PowerGroups
    {
        public const int MaxLeft = 3, MaxRight = 4;

        // Order inside a group = from the bottom up (the first entry is the lowest button, nearest to the thumb pads).
        static readonly PowerUp[] left = { PowerUp.DoubleJump, PowerUp.Rocket, PowerUp.Flight };
        static readonly PowerUp[] right = { PowerUp.Magnet, PowerUp.Shield, PowerUp.WallStick, PowerUp.SlowTime };

        /// <summary>True when the type belongs to the left group (Flight, Rocket, Double Jump). Everything else implemented belongs to the right group.</summary>
        public static bool IsLeft(PowerUp p)
        {
            return p == PowerUp.Flight || p == PowerUp.Rocket || p == PowerUp.DoubleJump;
        }

        /// <summary>True for the seven types that have a button (the right group is every implemented type that is not on the left).</summary>
        public static bool IsRight(PowerUp p)
        {
            return PowerUps.IsImplemented(p) && !IsLeft(p);
        }

        /// <summary>Types of a group from the bottom up. Do not modify the returned array.</summary>
        public static PowerUp[] Order(bool leftGroup)
        {
            return leftGroup ? left : right;
        }

        /// <summary>
        /// Fills <paramref name="result"/> with the types of a group that are <paramref name="shown"/> (bottom up) and returns how many. shown is indexed by PowerUp id.
        /// </summary>
        public static int Collect(bool leftGroup, bool[] shown, PowerUp[] result)
        {
            PowerUp[] order = Order(leftGroup);
            int n = 0;
            for (int i = 0; i < order.Length; i++)
            {
                int id = (int)order[i];
                if (shown != null && id < shown.Length && shown[id] && n < result.Length) result[n++] = order[i];
            }
            return n;
        }
    }
}
