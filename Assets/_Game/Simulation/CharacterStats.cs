using System;

namespace BoingUpSkyTower.Simulation
{
    /// <summary>
    /// The six stats stored per character (docs/29). Everything else a character does is derived from these and the global <see cref="SimConfig"/>.
    /// The defaults are Momo's baseline and equal the SimConfig defaults exactly, so a character without its own stats plays like Momo (golden hashes and bot proofs unchanged).
    /// No percent anywhere: plain numbers, speeds in units per second, accelerations in units per second squared.
    /// </summary>
    public sealed class CharacterStats
    {
        /// <summary>Upward launch speed of a standing jump, u/s.</summary>
        public int JumpLaunch = 19;
        /// <summary>Top sideways speed, u/s.</summary>
        public int MaxSpeed = 9;
        /// <summary>Ground acceleration, u/s2. Air steering, braking and turn-around follow it.</summary>
        public int Control = 30;
        /// <summary>Share of horizontal speed kept after a wall bounce, in hundredths (77 = 0.77).</summary>
        public int WallBoostHundredths = 77;
        /// <summary>Flat extra coins added to the payout of every cleared level.</summary>
        public int CoinFind;
        /// <summary>Flat extra gems added to the reward of every cleared level.</summary>
        public int GemFind;
        /// <summary>Meta bonus paid at the END of every run (win or fail) on top of the level reward: coins, a multiple of 10 from 50 to 100 for a roster hero. 0 = none (the bare default). Never touches the simulation.</summary>
        public int RunBonusCoins;
        /// <summary>Meta bonus paid at the end of every run: gems, 1 to 5 for a roster hero. 0 = none (the bare default).</summary>
        public int RunBonusGems;

        public static readonly CharacterStats Default = new CharacterStats();

        // Limits (docs/29 section 7). The minimums are 10 percent under Momo's baseline (owner 2026-11-02: Capi, Otto, Bonbon and Bao vary by -10..+10 percent). Maximums are proposals pending owner sign-off.
        public const int JumpLaunchMin = 17, JumpLaunchMax = 23;
        public const int MaxSpeedMin = 8, MaxSpeedMax = 12;
        public const int ControlMin = 27, ControlMax = 45;
        public const int WallBoostMin = 69, WallBoostMax = 100;
        public const int CoinFindMax = 50, GemFindMax = 5;
        public const int RunBonusCoinsMin = 50, RunBonusCoinsMax = 100, RunBonusCoinsStep = 10, RunBonusGemsMin = 1, RunBonusGemsMax = 5;

        // Merge-tier run bonuses are ADDED to the Common hero's own bonus (docs/34 section 2b): the ceilings are the Common maximum plus the Legendary maximum (2000 coins, 10 gems).
        public const int TierRunBonusCoinsMax = RunBonusCoinsMax + 2000, TierRunBonusGemsMax = RunBonusGemsMax + 10;

        /// <summary>Null when the stats are inside the limits, else a short reason.</summary>
        public string Validate()
        {
            string p = ValidatePhysics();
            if (p != null) return p;
            if (RunBonusCoins != 0 && (RunBonusCoins < RunBonusCoinsMin || RunBonusCoins > RunBonusCoinsMax || RunBonusCoins % RunBonusCoinsStep != 0)) return "RunBonusCoins " + RunBonusCoins + " must be 0 or a multiple of 10 in " + RunBonusCoinsMin + ".." + RunBonusCoinsMax;
            if (RunBonusGems != 0 && (RunBonusGems < RunBonusGemsMin || RunBonusGems > RunBonusGemsMax)) return "RunBonusGems " + RunBonusGems + " must be 0 or in " + RunBonusGemsMin + ".." + RunBonusGemsMax;
            return null;
        }

        /// <summary>Like <see cref="Validate"/> but for a merge-tier hero: the run bonus may reach the tier ceilings (Common bonus plus the tier's added bonus). The physics limits are the same.</summary>
        public string ValidateTier()
        {
            string p = ValidatePhysics();
            if (p != null) return p;
            if (RunBonusCoins != 0 && (RunBonusCoins < RunBonusCoinsMin || RunBonusCoins > TierRunBonusCoinsMax || RunBonusCoins % RunBonusCoinsStep != 0)) return "RunBonusCoins " + RunBonusCoins + " must be 0 or a multiple of 10 in " + RunBonusCoinsMin + ".." + TierRunBonusCoinsMax;
            if (RunBonusGems != 0 && (RunBonusGems < RunBonusGemsMin || RunBonusGems > TierRunBonusGemsMax)) return "RunBonusGems " + RunBonusGems + " must be 0 or in " + RunBonusGemsMin + ".." + TierRunBonusGemsMax;
            return null;
        }

        /// <summary>Null when the four physics stats and the flat finds are inside the limits (the part that keeps every level solvable), else a short reason.</summary>
        public string ValidatePhysics()
        {
            if (JumpLaunch < JumpLaunchMin || JumpLaunch > JumpLaunchMax) return "JumpLaunch " + JumpLaunch + " outside " + JumpLaunchMin + ".." + JumpLaunchMax;
            if (MaxSpeed < MaxSpeedMin || MaxSpeed > MaxSpeedMax) return "MaxSpeed " + MaxSpeed + " outside " + MaxSpeedMin + ".." + MaxSpeedMax;
            if (Control < ControlMin || Control > ControlMax) return "Control " + Control + " outside " + ControlMin + ".." + ControlMax;
            if (WallBoostHundredths < WallBoostMin || WallBoostHundredths > WallBoostMax) return "WallBoostHundredths " + WallBoostHundredths + " outside " + WallBoostMin + ".." + WallBoostMax;
            if (CoinFind < 0 || CoinFind > CoinFindMax) return "CoinFind " + CoinFind + " outside 0.." + CoinFindMax;
            if (GemFind < 0 || GemFind > GemFindMax) return "GemFind " + GemFind + " outside 0.." + GemFindMax;
            return null;
        }

        /// <summary>Writes the character's physics into a (cloned) run config. Fixed-point ratios keep the baseline bit-identical to the SimConfig defaults.</summary>
        public void ApplyTo(SimConfig cfg)
        {
            cfg.JumpBase = Fx.FromRatio(JumpLaunch, 60);
            cfg.MaxSpeed = Fx.FromRatio(MaxSpeed, 60);
            cfg.Accel = Fx.FromRatio(Control, 3600);
            cfg.WallBoost = Fx.FromRatio(WallBoostHundredths, 100);
        }

        /// <summary>The numbers the character card shows and the advanced view lists, for these stats under the given global config.</summary>
        public StatSheet Sheet(SimConfig globals)
        {
            SimConfig cfg = globals.Clone();
            ApplyTo(cfg);
            double g = Ups2(cfg.Gravity);
            double launch = Ups(cfg.JumpBase), speed = Ups(cfg.MaxSpeed), accel = Ups2(cfg.Accel);
            double factor = Ratio(cfg.JumpSpeedFactor);
            var s = new StatSheet();
            s.Jump = launch * launch / (2.0 * g);
            s.Speed = speed;
            s.Control = accel;
            s.WallBounce = speed * Ratio(cfg.WallBoost);
            s.CoinFind = CoinFind;
            s.GemFind = GemFind;
            s.RunBonusCoins = RunBonusCoins;
            s.RunBonusGems = RunBonusGems;
            s.AirSteering = accel * Ratio(cfg.AirControl);
            s.Braking = accel * Ratio(cfg.BrakeFactor);
            s.TurnAroundGround = accel * 2.0;
            s.TurnAroundAir = s.AirSteering * 2.0;
            s.TimeToStop = speed / s.Braking;
            s.StopDistance = speed * speed / (2.0 * s.Braking);
            s.TimeToTopSpeed = speed / accel;
            s.FullSpeedLaunch = launch + factor * speed;
            s.HalfSpeedJump = Sq(launch + factor * speed * 0.5) / (2.0 * g);
            s.FullSpeedJump = Sq(s.FullSpeedLaunch) / (2.0 * g);
            s.StickyJump = Sq(launch * Ratio(cfg.StickyJumpMultiplier)) / (2.0 * g);
            s.BounceLaunch = launch * Ratio(cfg.BounceMultiplier);
            s.BounceHeight = Sq(s.BounceLaunch) / (2.0 * g);
            s.Gravity = g;
            return s;
        }

        static double Ups(Fx v) { return v.Raw / 65536.0 * SimConfig.TicksPerSecond; }
        static double Ups2(Fx v) { return v.Raw / 65536.0 * SimConfig.TicksPerSecond * SimConfig.TicksPerSecond; }
        static double Ratio(Fx v) { return v.Raw / 65536.0; }
        static double Sq(double v) { return v * v; }
    }

    /// <summary>Main stats first (what the card shows), then the derived advanced values (docs/29 sections 2 and 3). Display only; the sim uses fixed-point.</summary>
    public struct StatSheet
    {
        public double Jump, Speed, Control, WallBounce;
        public int CoinFind, GemFind;
        public int RunBonusCoins, RunBonusGems;
        public double AirSteering, Braking, TurnAroundGround, TurnAroundAir;
        public double TimeToStop, StopDistance, TimeToTopSpeed;
        public double FullSpeedLaunch, HalfSpeedJump, FullSpeedJump, StickyJump;
        public double BounceLaunch, BounceHeight, Gravity;
    }
}
