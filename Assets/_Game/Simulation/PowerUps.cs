namespace BoingUpSkyTower.Simulation
{
    /// <summary>
    /// Power-up types (docs/15 section 3.2). The numeric ids are stable (saved loadouts and replays use them).
    /// Ids 8 to 11 are reserved by the data format and not implemented yet (PowerUps.IsImplemented).
    /// </summary>
    public enum PowerUp
    {
        None = 0,
        Flight = 1,
        Rocket = 2,
        DoubleJump = 3,
        WallStick = 4,
        Shield = 5,
        Magnet = 6,
        SlowTime = 7,
        SpringBoots = 8,
        CoinDoubler = 9,
        StarSeeker = 10,
        SafetyNet = 11
    }

    /// <summary>Static helpers for power-up ids, the data key mapping (Content/economy/powerups.json) and active-effect queries for UI.</summary>
    public static class PowerUps
    {
        /// <summary>Implemented types are ids 1..7; every owned type is usable in a run (no loadout slots, no carry cap, no stock cap).</summary>
        public const int TypeCount = 7;
        public const int MaxType = 11;

        /// <summary>Saturating add for counts and timers (stock is unlimited, so nothing may overflow).</summary>
        public static int SatAdd(int a, int b)
        {
            long r = (long)a + b;
            return r > int.MaxValue ? int.MaxValue : (r < 0 ? 0 : (int)r);
        }

        public static bool IsImplemented(PowerUp p)
        {
            return p >= PowerUp.Flight && p <= PowerUp.SlowTime;
        }

        /// <summary>Data id used in saves and JSON ("pw_flight"); empty for None.</summary>
        public static string IdOf(PowerUp p)
        {
            switch (p)
            {
                case PowerUp.Flight: return "pw_flight";
                case PowerUp.Rocket: return "pw_rocket";
                case PowerUp.DoubleJump: return "pw_doublejump";
                case PowerUp.WallStick: return "pw_wallstick";
                case PowerUp.Shield: return "pw_shield";
                case PowerUp.Magnet: return "pw_magnet";
                case PowerUp.SlowTime: return "pw_slowtime";
                case PowerUp.SpringBoots: return "pw_springboots";
                case PowerUp.CoinDoubler: return "pw_coindoubler";
                case PowerUp.StarSeeker: return "pw_starseeker";
                case PowerUp.SafetyNet: return "pw_safetynet";
                default: return "";
            }
        }

        public static PowerUp FromId(string id)
        {
            if (string.IsNullOrEmpty(id)) return PowerUp.None;
            for (int i = 1; i <= MaxType; i++)
            {
                if (IdOf((PowerUp)i) == id) return (PowerUp)i;
            }
            return PowerUp.None;
        }

        /// <summary>Input bit that activates the type (Flight 8 ... Slow Time 512); 0 for None and unimplemented types.</summary>
        public static int InputBit(PowerUp p)
        {
            int i = (int)p;
            return i >= (int)PowerUp.Flight && i <= (int)PowerUp.SlowTime ? SimInput.PowerFirst << (i - 1) : 0;
        }

        /// <summary>Input bit that activates the type as a HERO power (Flight 1024 ... Slow Time 65536); 0 for None and unimplemented types.</summary>
        public static int HeroInputBit(PowerUp p)
        {
            int i = (int)p;
            return i >= (int)PowerUp.Flight && i <= (int)PowerUp.SlowTime ? SimInput.HeroFirst << (i - 1) : 0;
        }

        /// <summary>
        /// True when a hero activation of the type takes effect now: the same rules as <see cref="CanActivate"/>, and the effect must not already be running
        /// (a hero power never stacks onto itself; cooldowns are 10 to 30 s, so a long Shield could otherwise be kept up for ever).
        /// </summary>
        public static bool CanActivateHero(in SimState s, PowerUp p)
        {
            return CanActivate(in s, p) && !IsActive(in s, p);
        }

        /// <summary>Full length in ticks of the timed effect (0 for effects that are not timed).</summary>
        public static int TotalTicks(SimConfig cfg, PowerUp p)
        {
            switch (p)
            {
                case PowerUp.Flight: return cfg.FlightTicks;
                case PowerUp.Rocket: return cfg.RocketTicks;
                case PowerUp.DoubleJump: return cfg.DoubleJumpWindowTicks;
                case PowerUp.WallStick: return cfg.WallStickWindowTicks;
                case PowerUp.Shield: return cfg.ShieldTicks;
                case PowerUp.Magnet: return cfg.MagnetTicks;
                case PowerUp.SlowTime: return cfg.SlowTimeTicks;
                default: return 0;
            }
        }

        /// <summary>Remaining ticks of the effect, 0 when it is not active.</summary>
        public static int ActiveTicks(in SimState s, PowerUp p)
        {
            switch (p)
            {
                case PowerUp.Flight: return s.FlightTicks;
                case PowerUp.Rocket: return s.RocketTicks;
                case PowerUp.DoubleJump: return s.DoubleJumpTicks;
                case PowerUp.WallStick: return s.WallStickTicks;
                case PowerUp.Shield: return s.ShieldTicks;
                case PowerUp.Magnet: return s.MagnetTicks;
                case PowerUp.SlowTime: return s.SlowTicks;
                default: return 0;
            }
        }

        public static bool IsActive(in SimState s, PowerUp p)
        {
            return ActiveTicks(in s, p) > 0;
        }

        /// <summary>
        /// True when an activation bit of the type takes effect now: implemented and alive. The owned stock is NOT part of the state: the
        /// caller (RunSession) only sets the bit while units are left and takes one unit per accepted tap. Tapping while the effect is
        /// active is allowed and stacks (adds the base time onto the remaining time). Flight and Rocket cannot run together.
        /// </summary>
        public static bool CanActivate(in SimState s, PowerUp p)
        {
            if (s.Dead) return false;
            if (!IsImplemented(p)) return false;
            if (p == PowerUp.Flight && s.RocketTicks > 0) return false;
            if (p == PowerUp.Rocket && s.FlightTicks > 0) return false;
            return true;
        }
    }
}
