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
        public const int SlotCount = 3;
        public const int MaxType = 11;

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

        /// <summary>Input bit that activates the loadout slot (0..2).</summary>
        public static int InputBitForSlot(int slot)
        {
            return SimInput.PowerSlot0 << slot;
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
                case PowerUp.DoubleJump: return s.DoubleJumps > 0 ? s.DoubleJumpTicks : 0;
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

        /// <summary>True when pressing the slot now would start the power-up (stock left, not already active, implemented).</summary>
        public static bool CanActivate(in SimState s, int slot)
        {
            if (s.Dead) return false;
            PowerUp p = s.SlotType(slot);
            if (!IsImplemented(p) || s.SlotUnits(slot) <= 0) return false;
            if (IsActive(in s, p)) return false;
            if (p == PowerUp.Flight && s.RocketTicks > 0) return false;
            if (p == PowerUp.Rocket && s.FlightTicks > 0) return false;
            return true;
        }
    }
}
